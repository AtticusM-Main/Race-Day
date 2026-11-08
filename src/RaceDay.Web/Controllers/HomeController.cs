using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : BaseController
{
    public HomeController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    public async Task<IActionResult> Index([FromQuery] EventFilterModel? filter)
    {
        if (UserSession.IsOrganiser) return RedirectToAction("Dashboard", "Organiser");
        if (!UserSession.IsParticipant) return View("Landing");

        filter ??= new EventFilterModel();
        if (filter.From > filter.To)
        {
            Warning("The start date was after the end date, so the dates were ignored.");
            filter.From = null;
            filter.To = null;
        }

        var result = await Api.GetEventsAsync(filter.ToApiFilter());
        if (result.IsUnauthorized)
        {
            UserSession.SignOut();
            Warning("Your session has expired. Please log in again.");
            return RedirectToAction("Login", "Account");
        }

        if (!result.Success) ViewData["LoadError"] = result.Error;

        return View("Events", new EventListViewModel { Filter = filter, Events = result.Data ?? new List<EventDto>() });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [Route("Home/StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        return View(code == 404 ? "NotFound" : "Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
