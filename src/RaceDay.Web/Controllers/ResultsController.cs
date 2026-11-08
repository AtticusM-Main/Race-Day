using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Participant")]
public class ResultsController : BaseController
{
    public ResultsController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyResultsAsync();
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction("Index", "Home"))!;
        if (!result.Success) ViewData["LoadError"] = result.Error;

        return View(result.Data ?? new List<ResultDto>());
    }
}
