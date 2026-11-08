using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Organiser")]
[Route("organiser")]
public class OrganiserController : BaseController
{
    public OrganiserController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var result = await Api.GetMyEventsAsync();
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction("Login", "Account"))!;
        if (!result.Success) ViewData["LoadError"] = result.Error;

        return View(new OrganiserDashboardViewModel { Events = result.Data ?? new List<Models.Api.EventDto>() });
    }
}
