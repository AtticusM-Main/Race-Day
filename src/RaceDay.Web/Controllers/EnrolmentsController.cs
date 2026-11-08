using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Participant")]
public class EnrolmentsController : BaseController
{
    public EnrolmentsController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEnrolmentsAsync();
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction("Index", "Home"))!;
        if (!result.Success) ViewData["LoadError"] = result.Error;

        return View(result.Data ?? new List<Models.Api.EnrolmentDto>());
    }

    [HttpPost]
    public async Task<IActionResult> Create(int eventId, int? categoryId)
    {
        if (categoryId is null)
        {
            Failure("Choose a category before entering.");
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        var result = await Api.EnrolAsync(categoryId.Value);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction("Index", "Home"))!;

        if (!result.Success)
        {
            Failure(result.Error!);
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        Success($"You're in! You have entered {result.Data!.EventName} ({result.Data.CategoryName}). The organiser will confirm your place.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await Api.CancelEnrolmentAsync(id);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction(nameof(Index)))!;

        if (result.Success) Success("Your enrolment has been cancelled.");
        else Failure(result.Error!);

        return RedirectToAction(nameof(Index));
    }
}
