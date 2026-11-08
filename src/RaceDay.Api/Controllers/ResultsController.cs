using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Extensions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Race results. Organisers capture finish times and positions for their events;
/// Participants view their own results; anyone can view a category leaderboard.
/// </summary>
[ApiController]
[Produces("application/json")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<Result> ResultsWithDetails() => _db.Results
        .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
        .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event);

    /// <summary>
    /// Record a Participant's finish time and position for an enrolment.
    /// </summary>
    /// <remarks>Only the Organiser who owns the event can record results, and each enrolment can have only one result.</remarks>
    /// <response code="201">Result recorded. Returns the new result.</response>
    /// <response code="400">Invalid data (e.g. a finish time of zero).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns the event.</response>
    /// <response code="404">No enrolment with this id.</response>
    /// <response code="409">A result already exists for this enrolment, or the position is already taken in this category.</response>
    [HttpPost("api/results")]
    [Authorize(Roles = Role.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CreateResult(CreateResultRequest request)
    {
        if (request.FinishTime <= TimeSpan.Zero || request.FinishTime >= TimeSpan.FromDays(1))
        {
            ModelState.AddModelError(nameof(request.FinishTime), "Finish time must be between 00:00:01 and 23:59:59.");
            return ValidationProblem(ModelState);
        }

        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId);
        if (enrolment is null) return NotFound();
        if (enrolment.Category.Event.OrganiserId != User.GetUserId()) return Forbid();

        if (enrolment.Result is not null)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Result exists",
                detail: "A result has already been recorded for this enrolment. Use PUT /api/results/{id} to change it.");
        }

        if (await PositionTaken(enrolment.CategoryId, request.Position, excludeResultId: null))
        {
            return PositionConflict(request.Position!.Value);
        }

        var result = new Result
        {
            EnrolmentId = enrolment.EnrolmentId,
            FinishTime = request.FinishTime!.Value,
            Position = request.Position,
            RecordedAt = DateTime.Now
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var created = await ResultsWithDetails().AsNoTracking().FirstAsync(r => r.ResultId == result.ResultId);
        return Created("/api/results/me", created.ToResponse(await CountFinishers(created.Enrolment.CategoryId)));
    }

    /// <summary>
    /// Update a recorded result.
    /// </summary>
    /// <param name="id">The result id.</param>
    /// <param name="request">The corrected finish time and position.</param>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns the event.</response>
    /// <response code="404">No result with this id.</response>
    /// <response code="409">The position is already taken in this category.</response>
    [HttpPut("api/results/{id:int}")]
    [Authorize(Roles = Role.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> UpdateResult(int id, UpdateResultRequest request)
    {
        if (request.FinishTime <= TimeSpan.Zero || request.FinishTime >= TimeSpan.FromDays(1))
        {
            ModelState.AddModelError(nameof(request.FinishTime), "Finish time must be between 00:00:01 and 23:59:59.");
            return ValidationProblem(ModelState);
        }

        var result = await ResultsWithDetails().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFound();
        if (result.Enrolment.Category.Event.OrganiserId != User.GetUserId()) return Forbid();

        if (await PositionTaken(result.Enrolment.CategoryId, request.Position, excludeResultId: id))
        {
            return PositionConflict(request.Position!.Value);
        }

        result.FinishTime = request.FinishTime!.Value;
        result.Position = request.Position;
        result.RecordedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(result.ToResponse(await CountFinishers(result.Enrolment.CategoryId)));
    }

    /// <summary>
    /// Get the logged-in Participant's own results.
    /// </summary>
    /// <response code="200">Array of the Participant's results with event and category details, newest event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not a Participant.</response>
    [HttpGet("api/results/me")]
    [Authorize(Roles = Role.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMyResults()
    {
        var participantId = User.GetUserId();
        var results = await ResultsWithDetails().AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .ToListAsync();

        var totals = await FinisherCounts(results.Select(r => r.Enrolment.CategoryId));
        return Ok(results.Select(r => r.ToResponse(totals[r.Enrolment.CategoryId])));
    }

    /// <summary>
    /// Get the public leaderboard for a category, ordered by finishing position.
    /// </summary>
    /// <remarks>Results without a position are listed last, ordered by finish time.</remarks>
    /// <param name="id">The category id.</param>
    /// <response code="200">Ordered array of results.</response>
    /// <response code="404">No category with this id.</response>
    [HttpGet("api/categories/{id:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetLeaderboard(int id)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == id)) return NotFound();

        var results = await ResultsWithDetails().AsNoTracking()
            .Where(r => r.Enrolment.CategoryId == id)
            .OrderBy(r => r.Position == null)
            .ThenBy(r => r.Position)
            .ThenBy(r => r.FinishTime)
            .ToListAsync();

        return Ok(results.Select(r => r.ToResponse(results.Count)));
    }

    private Task<int> CountFinishers(int categoryId) =>
        _db.Results.CountAsync(r => r.Enrolment.CategoryId == categoryId);

    private async Task<Dictionary<int, int>> FinisherCounts(IEnumerable<int> categoryIds)
    {
        var ids = categoryIds.Distinct().ToList();
        return await _db.Results
            .Where(r => ids.Contains(r.Enrolment.CategoryId))
            .GroupBy(r => r.Enrolment.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
    }

    private Task<bool> PositionTaken(int categoryId, int? position, int? excludeResultId) =>
        position is null
            ? Task.FromResult(false)
            : _db.Results.AnyAsync(r => r.Enrolment.CategoryId == categoryId &&
                                        r.Position == position &&
                                        r.ResultId != excludeResultId);

    private ObjectResult PositionConflict(int position) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Position taken",
            detail: $"Position {position} has already been given to another participant in this category.");
}
