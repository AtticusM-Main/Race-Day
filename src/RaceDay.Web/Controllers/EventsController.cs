using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Participant")]
public class EventsController : BaseController
{
    public EventsController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var eventResult = await Api.GetEventAsync(id);
        var failure = HandleFailure(eventResult, RedirectToAction("Index", "Home"));
        if (failure is not null) return failure;

        var enrolments = await Api.GetMyEnrolmentsAsync();
        if (enrolments.IsUnauthorized) return HandleFailure(enrolments, RedirectToAction("Index", "Home"))!;

        return View(new EventDetailsViewModel
        {
            Event = eventResult.Data!,
            MyEnrolment = enrolments.Data?.FirstOrDefault(e => e.EventId == id)
        });
    }
}
