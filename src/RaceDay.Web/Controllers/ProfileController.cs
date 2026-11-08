using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize]
public class ProfileController : BaseController
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxPictureBytes = 5 * 1024 * 1024;

    public ProfileController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetProfileAsync();
        var failure = HandleFailure(result, RedirectToAction("Index", "Home"));
        if (failure is not null) return failure;

        return View(ToViewModel(result.Data!));
    }

    [HttpPost]
    public async Task<IActionResult> Update(UpdateProfileRequest form)
    {
        if (!ModelState.IsValid) return await ShowFormWithErrorsAsync(form);

        var result = await Api.UpdateProfileAsync(form);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction(nameof(Index)))!;
        if (!result.Success)
        {
            AddApiErrors(result, "Form");
            return await ShowFormWithErrorsAsync(form);
        }

        Success("Your profile has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        var problem = ValidatePicture(picture);
        if (problem is not null)
        {
            Failure(problem);
            return RedirectToAction(nameof(Index));
        }

        var result = await Api.UploadProfilePictureAsync(picture!);
        if (result.IsUnauthorized) return HandleFailure(result, RedirectToAction(nameof(Index)))!;
        if (!result.Success)
        {
            Failure(result.Error!);
            return RedirectToAction(nameof(Index));
        }

        UserSession.ProfilePictureUrl = result.Data!.ProfilePictureUrl;
        Success("Your profile picture has been updated.");
        return RedirectToAction(nameof(Index));
    }

    public static string? ValidatePicture(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Choose an image to upload.";
        if (file.Length > MaxPictureBytes) return "The image must be 5 MB or smaller.";
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) return "Only JPG, PNG or WebP images are allowed.";

        return null;
    }

    private async Task<IActionResult> ShowFormWithErrorsAsync(UpdateProfileRequest form)
    {
        var current = await Api.GetProfileAsync();
        var model = current.Data is null ? new ProfileViewModel() : ToViewModel(current.Data);
        model.Form = form;
        return View(nameof(Index), model);
    }

    private static ProfileViewModel ToViewModel(UserProfile profile) => new()
    {
        Profile = profile,
        Form = new UpdateProfileRequest { FullName = profile.FullName, Email = profile.Email }
    };
}
