using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = "Organiser")]
[Route("organiser/events")]
public class OrganiserEventsController : BaseController
{
    public OrganiserEventsController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction("Login", "Account"))!;
        if (!result.Success) ViewData["LoadError"] = result.Error;

        return View(result.Data ?? new List<EventDto>());
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new EventFormViewModel
    {
        Form = new EventRequest { EventDate = DateTime.Now.AddMonths(1).Date.AddHours(7) }
    });

    [HttpPost("create")]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("Form", model);

        var created = await Api.CreateEventAsync(model.Form);
        if (created.IsUnauthorized) return HandleFailure(created, RedirectToAction(nameof(Index)))!;
        if (!created.Success)
        {
            AddApiErrors(created, "Form");
            return View("Form", model);
        }

        var eventId = created.Data!.EventId;
        if (model.Banner is not null)
        {
            var upload = await Api.UploadBannerAsync(eventId, model.Banner);
            if (!upload.Success)
            {
                Warning($"\"{created.Data.Name}\" was created, but the banner could not be uploaded: {upload.Error} You can try again by editing the event.");
                return RedirectToAction("Index", "OrganiserCategories", new { eventId });
            }
        }

        Success($"\"{created.Data.Name}\" was created. Now add the categories people can enter.");
        return RedirectToAction("Index", "OrganiserCategories", new { eventId });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await Api.GetEventAsync(id);
        var failure = HandleFailure(result, RedirectToAction(nameof(Index)));
        if (failure is not null) return failure;

        var ev = result.Data!;
        if (ev.OrganiserId != UserSession.UserId)
        {
            Failure("You can only edit events that you created.");
            return RedirectToAction(nameof(Index));
        }

        return View("Form", new EventFormViewModel
        {
            EventId = ev.EventId,
            CurrentBannerUrl = ev.BannerImageUrl,
            Form = new EventRequest
            {
                Name = ev.Name,
                Description = ev.Description,
                EventDate = ev.EventDate,
                Location = ev.Location,
                Distance = ev.Distance,
                EventType = ev.EventType
            }
        });
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;
        ValidateBanner(model);
        if (!ModelState.IsValid) return await FormWithCurrentBannerAsync(model);

        var updated = await Api.UpdateEventAsync(id, model.Form);
        if (updated.IsUnauthorized || updated.IsNotFound || updated.IsForbidden) return HandleFailure(updated, RedirectToAction(nameof(Index)))!;
        if (!updated.Success)
        {
            AddApiErrors(updated, "Form");
            return await FormWithCurrentBannerAsync(model);
        }

        if (model.Banner is not null)
        {
            var upload = await Api.UploadBannerAsync(id, model.Banner);
            if (!upload.Success)
            {
                Warning($"Your changes were saved, but the banner could not be uploaded: {upload.Error}");
                return RedirectToAction(nameof(Index));
            }
        }

        Success($"\"{updated.Data!.Name}\" was updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteEventAsync(id);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction(nameof(Index)))!;

        if (result.Success) Success("The event and its categories, enrolments and results were deleted.");
        else Failure(result.Error!);

        return RedirectToAction(nameof(Index));
    }

    private void ValidateBanner(EventFormViewModel model)
    {
        if (model.Banner is null || model.Banner.Length == 0)
        {
            model.Banner = null;
            return;
        }

        var problem = ImageRules.Validate(model.Banner);
        if (problem is not null) ModelState.AddModelError("Banner", problem);
    }

    private async Task<IActionResult> FormWithCurrentBannerAsync(EventFormViewModel model)
    {
        var current = await Api.GetEventAsync(model.EventId!.Value);
        model.CurrentBannerUrl = current.Data?.BannerImageUrl;
        return View("Form", model);
    }
}
