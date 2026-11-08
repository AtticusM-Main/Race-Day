using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Events. Anyone can view them; only Organisers can create them, and only the owning Organiser can change or delete one.
/// </summary>
[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _images;

    public EventsController(RaceDayDbContext db, IImageStorage images)
    {
        _db = db;
        _images = images;
    }

    private IQueryable<Event> EventsWithDetails() => _db.Events
        .Include(e => e.Organiser)
        .Include(e => e.Categories).ThenInclude(c => c.Enrolments);

    /// <summary>
    /// List all events, soonest first.
    /// </summary>
    /// <response code="200">Array of events, each with its categories.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetEvents()
    {
        var events = await EventsWithDetails().AsNoTracking().OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToResponse()));
    }

    /// <summary>
    /// Get one event by id.
    /// </summary>
    /// <param name="id">The event id.</param>
    /// <response code="200">The event, with its categories.</response>
    /// <response code="404">No event with this id.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetEvent(int id)
    {
        var ev = await EventsWithDetails().AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();

        return Ok(ev.ToResponse());
    }

    /// <summary>
    /// Create a new event. The logged-in Organiser becomes its owner.
    /// </summary>
    /// <remarks>EventType must be "Run", "Walk" or "Cycle". Distance is in km.</remarks>
    /// <response code="201">Event created. Returns the new event.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not an Organiser.</response>
    [HttpPost]
    [Authorize(Roles = Role.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> CreateEvent(EventRequest request)
    {
        var ev = new Event
        {
            OrganiserId = User.GetUserId(),
            CreatedAt = DateTime.Now
        };
        Apply(request, ev);

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var created = await EventsWithDetails().AsNoTracking().FirstAsync(e => e.EventId == ev.EventId);
        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, created.ToResponse());
    }

    /// <summary>
    /// Update an event owned by the logged-in Organiser.
    /// </summary>
    /// <param name="id">The event id.</param>
    /// <param name="request">The event's new details. All fields are replaced.</param>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">No event with this id.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Role.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> UpdateEvent(int id, EventRequest request)
    {
        var ev = await EventsWithDetails().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        Apply(request, ev);
        await _db.SaveChangesAsync();

        return Ok(ev.ToResponse());
    }

    /// <summary>
    /// Delete an event owned by the logged-in Organiser.
    /// </summary>
    /// <remarks>Its categories, enrolments and results are deleted with it.</remarks>
    /// <param name="id">The event id.</param>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">No event with this id.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Role.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        var bannerUrl = ev.BannerImageUrl;
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        await _images.DeleteAsync(bannerUrl);

        return NoContent();
    }

    /// <summary>
    /// Upload (or replace) the banner image of an event owned by the logged-in Organiser.
    /// </summary>
    /// <remarks>Send the image as multipart/form-data in a field named "file". JPG, PNG or WebP, up to 5 MB. The file is stored in Azure Blob Storage.</remarks>
    /// <param name="id">The event id.</param>
    /// <param name="file">The banner image.</param>
    /// <response code="200">The updated event, including the banner image URL.</response>
    /// <response code="400">Missing, too large or unsupported image.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">No event with this id.</response>
    /// <response code="503">Cloud storage is not available.</response>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = Role.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageUploadValidator.MaxBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EventResponse>> UploadBanner(int id, IFormFile? file)
    {
        var ev = await EventsWithDetails().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        var error = ImageUploadValidator.Validate(file, out var extension, out var contentType);
        if (error is not null)
        {
            ModelState.AddModelError(nameof(file), error);
            return ValidationProblem(ModelState);
        }

        string url;
        try
        {
            await using var stream = file!.OpenReadStream();
            url = await _images.UploadAsync(stream, "events", extension, contentType);
        }
        catch (ImageStorageUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Image storage unavailable", detail: ex.Message);
        }

        var oldUrl = ev.BannerImageUrl;
        ev.BannerImageUrl = url;
        await _db.SaveChangesAsync();
        await _images.DeleteAsync(oldUrl);

        return Ok(ev.ToResponse());
    }

    private static void Apply(EventRequest request, Event ev)
    {
        ev.Name = request.Name.Trim();
        ev.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.Distance = request.Distance!.Value;
        ev.EventType = request.EventType!.Value;
    }
}
