using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Organiser")]
[Route("organiser/events/{eventId:int}/categories")]
public class OrganiserCategoriesController : BaseController
{
    public OrganiserCategoriesController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await LoadEventAsync(eventId);
        if (ev.Failure is not null) return ev.Failure;

        return View(new CategoriesViewModel { Event = ev.Event! });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(int eventId, [Bind(Prefix = "NewCategory")] CategoryRequest form)
    {
        var ev = await LoadEventAsync(eventId);
        if (ev.Failure is not null) return ev.Failure;

        if (!ModelState.IsValid) return View("Index", new CategoriesViewModel { Event = ev.Event!, NewCategory = form });

        var result = await Api.CreateCategoryAsync(eventId, form);
        if (result.IsUnauthorized || result.IsForbidden || result.IsNotFound) return HandleFailure(result, RedirectToAction("Index", "OrganiserEvents"))!;
        if (!result.Success)
        {
            AddApiErrors(result, "NewCategory");
            return View("Index", new CategoriesViewModel { Event = ev.Event!, NewCategory = form });
        }

        Success($"Category \"{result.Data!.Name}\" was added.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int eventId, int id)
    {
        var ev = await LoadEventAsync(eventId);
        if (ev.Failure is not null) return ev.Failure;

        var category = ev.Event!.Categories.FirstOrDefault(c => c.CategoryId == id);
        if (category is null) return NotFound();

        return View(new CategoryEditViewModel
        {
            Event = ev.Event,
            CategoryId = id,
            Form = new CategoryRequest { Name = category.Name, Distance = category.Distance, MaxParticipants = category.MaxParticipants }
        });
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int eventId, int id, [Bind(Prefix = "Form")] CategoryRequest form)
    {
        var ev = await LoadEventAsync(eventId);
        if (ev.Failure is not null) return ev.Failure;

        var model = new CategoryEditViewModel { Event = ev.Event!, CategoryId = id, Form = form };
        if (!ModelState.IsValid) return View(model);

        var result = await Api.UpdateCategoryAsync(id, form);
        if (result.IsUnauthorized || result.IsForbidden || result.IsNotFound) return HandleFailure(result, RedirectToAction(nameof(Index), new { eventId }))!;
        if (!result.Success)
        {
            AddApiErrors(result, "Form");
            return View(model);
        }

        Success($"Category \"{result.Data!.Name}\" was updated.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var result = await Api.DeleteCategoryAsync(id);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction(nameof(Index), new { eventId }))!;

        if (result.Success) Success("The category was removed.");
        else Failure(result.Error!);

        return RedirectToAction(nameof(Index), new { eventId });
    }

    private async Task<(EventDto? Event, IActionResult? Failure)> LoadEventAsync(int eventId)
    {
        var result = await Api.GetEventAsync(eventId);
        var failure = HandleFailure(result, RedirectToAction("Index", "OrganiserEvents"));
        if (failure is not null) return (null, failure);

        if (result.Data!.OrganiserId != UserSession.UserId)
        {
            Failure("You can only manage categories for events that you created.");
            return (null, RedirectToAction("Index", "OrganiserEvents"));
        }

        return (result.Data, null);
    }
}
