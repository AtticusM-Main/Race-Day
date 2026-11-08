using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Event entries. Participants enrol themselves into an event by choosing one of its categories;
/// Organisers can see who has entered their events.
/// </summary>
[ApiController]
[Produces("application/json")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<Enrolment> EnrolmentsWithDetails() => _db.Enrolments
        .Include(e => e.Participant)
        .Include(e => e.Result)
        .Include(e => e.Category).ThenInclude(c => c.Event);

    /// <summary>
    /// Enrol the logged-in Participant in an event by selecting one of its categories.
    /// </summary>
    /// <remarks>
    /// Records the link between the Participant, the event and the chosen category.
    /// A Participant can only hold one enrolment per event, and cannot enter a full category or an event that has already happened.
    /// </remarks>
    /// <response code="201">Enrolled. Returns the new enrolment.</response>
    /// <response code="400">Invalid data, or the event has already taken place.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not a Participant.</response>
    /// <response code="404">No category with this id.</response>
    /// <response code="409">Already enrolled in this event, or the category is full.</response>
    [HttpPost("api/enrolments")]
    [Authorize(Roles = Role.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(EnrolmentRequest request)
    {
        var participantId = User.GetUserId();
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments)
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null) return NotFound();

        if (category.Event.EventDate < DateTime.Now)
        {
            ModelState.AddModelError(nameof(request.CategoryId), "This event has already taken place.");
            return ValidationProblem(ModelState);
        }

        var alreadyEnrolled = await _db.Enrolments.AnyAsync(e =>
            e.ParticipantId == participantId && e.Category.EventId == category.EventId);
        if (alreadyEnrolled)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Already enrolled",
                detail: "You are already enrolled in this event.");
        }

        if (category.MaxParticipants is not null && category.Enrolments.Count >= category.MaxParticipants)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Category full",
                detail: "This category has reached its maximum number of participants.");
        }

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            CategoryId = category.CategoryId,
            EnrolmentDate = DateTime.Now
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var created = await EnrolmentsWithDetails().AsNoTracking().FirstAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        return Created("/api/enrolments/me", created.ToResponse());
    }

    /// <summary>
    /// Get the logged-in Participant's own enrolments.
    /// </summary>
    /// <response code="200">Array of the Participant's enrolments, soonest event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not a Participant.</response>
    [HttpGet("api/enrolments/me")]
    [Authorize(Roles = Role.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMyEnrolments()
    {
        var participantId = User.GetUserId();
        var enrolments = await EnrolmentsWithDetails().AsNoTracking()
            .Where(e => e.ParticipantId == participantId)
            .OrderBy(e => e.Category.Event.EventDate)
            .ToListAsync();

        return Ok(enrolments.Select(e => e.ToResponse()));
    }

    /// <summary>
    /// Cancel one of the logged-in Participant's enrolments.
    /// </summary>
    /// <param name="id">The enrolment id.</param>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or the enrolment belongs to someone else.</response>
    /// <response code="404">No enrolment with this id.</response>
    /// <response code="409">A result has already been recorded for this enrolment.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [Authorize(Roles = Role.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await _db.Enrolments.Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.ParticipantId != User.GetUserId()) return Forbid();

        if (enrolment.Result is not null)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Result recorded",
                detail: "This enrolment already has a result and can't be cancelled.");
        }

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Confirm (or revert to pending) an enrolment for an event owned by the logged-in Organiser.
    /// </summary>
    /// <param name="id">The enrolment id.</param>
    /// <param name="request">The new status.</param>
    /// <response code="200">The updated enrolment.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns the event.</response>
    /// <response code="404">No enrolment with this id.</response>
    [HttpPut("api/enrolments/{id:int}/status")]
    [Authorize(Roles = Role.Organiser)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentResponse>> UpdateStatus(int id, UpdateEnrolmentStatusRequest request)
    {
        var enrolment = await EnrolmentsWithDetails().FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.Category.Event.OrganiserId != User.GetUserId()) return Forbid();

        enrolment.Status = request.Status!.Value;
        await _db.SaveChangesAsync();

        return Ok(enrolment.ToResponse());
    }

    /// <summary>
    /// List all enrolments for an event owned by the logged-in Organiser.
    /// </summary>
    /// <param name="eventId">The event id.</param>
    /// <response code="200">Array of enrolments with participant and category details.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">No event with this id.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [Authorize(Roles = Role.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        var enrolments = await EnrolmentsWithDetails().AsNoTracking()
            .Where(e => e.Category.EventId == eventId)
            .OrderBy(e => e.Category.Name).ThenBy(e => e.Participant.FullName)
            .ToListAsync();

        return Ok(enrolments.Select(e => e.ToResponse()));
    }
}
