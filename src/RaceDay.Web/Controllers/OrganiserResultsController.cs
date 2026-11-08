using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Organiser")]
[Route("organiser/events/{eventId:int}/results")]
public class OrganiserResultsController : BaseController
{
    public OrganiserResultsController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var model = await LoadAsync(eventId);
        if (model.Failure is not null) return model.Failure;

        return View(model.Model);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(int eventId, ResultRequest form)
    {
        if (form.FinishTime is null || form.FinishTime <= TimeSpan.Zero || form.FinishTime >= TimeSpan.FromDays(1))
        {
            ModelState.AddModelError(nameof(form.FinishTime), "Enter a finish time between 00:00:01 and 23:59:59, for example 00:48:30.");
        }

        if (!ModelState.IsValid)
        {
            Failure(ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                    ?? "Please check the finish time and position you entered.");
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var result = form.ResultId is int resultId
            ? await Api.UpdateResultAsync(resultId, form)
            : await Api.RecordResultAsync(form);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction(nameof(Index), new { eventId }))!;

        if (result.Success)
        {
            Success($"Result published for {result.Data!.ParticipantName}: {Formatting.FinishTime(result.Data.FinishTime)}" +
                    (result.Data.Position is int p ? $", {Formatting.Ordinal(p)} place." : "."));
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
            Failure("You can only enter results for events that you created.");
            return (null, RedirectToAction("Index", "OrganiserEvents"));
        }

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        failure = HandleFailure(enrolments, RedirectToAction("Index", "OrganiserEvents"));
        if (failure is not null) return (null, failure);

        return (new EventEnrolmentsViewModel { Event = ev.Data, Enrolments = enrolments.Data ?? new List<EnrolmentDto>() }, null);
    }
}
