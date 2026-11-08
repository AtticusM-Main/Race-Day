using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Organiser")]
[Route("organiser/events/{eventId:int}/enrolments")]
public class OrganiserEnrolmentsController : BaseController
{
    public OrganiserEnrolmentsController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var model = await LoadAsync(eventId);
        if (model.Failure is not null) return model.Failure;

        return View(model.Model);
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int eventId, int id, EnrolmentStatus status)
    {
        var result = await Api.SetEnrolmentStatusAsync(id, status);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction(nameof(Index), new { eventId }))!;

        if (result.Success)
        {
            Success(status == EnrolmentStatus.Confirmed
                ? $"{result.Data!.ParticipantName}'s place is confirmed."
                : $"{result.Data!.ParticipantName}'s entry is back to pending.");
        }
        else
        {
            Failure(result.Error!);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    private async Task<(EventEnrolmentsViewModel? Model, IActionResult? Failure)> LoadAsync(int eventId)
    {
        var ev = await Api.GetEventAsync(eventId);
        var failure = HandleFailure(ev, RedirectToAction("Index", "OrganiserEvents"));
        if (failure is not null) return (null, failure);

        if (ev.Data!.OrganiserId != UserSession.UserId)
        {
            Failure("You can only view enrolments for events that you created.");
            return (null, RedirectToAction("Index", "OrganiserEvents"));
        }

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        failure = HandleFailure(enrolments, RedirectToAction("Index", "OrganiserEvents"));
        if (failure is not null) return (null, failure);

        return (new EventEnrolmentsViewModel { Event = ev.Data, Enrolments = enrolments.Data ?? new List<EnrolmentDto>() }, null);
    }
}
