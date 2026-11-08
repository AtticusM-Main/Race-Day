using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : BaseController
{
    public AccountController(IRaceDayApi api, IUserSession userSession) : base(api, userSession)
    {
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (UserSession.IsAuthenticated) return RedirectToHome();

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginRequest());
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await Api.LoginAsync(model);
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.IsUnauthorized
                ? "That email and password don't match. Please try again."
                : result.Error ?? "We couldn't log you in. Please try again.");
            model.Password = string.Empty;
            return View(model);
        }

        UserSession.SignIn(result.Data);
        await LoadProfilePictureAsync();
        Success($"Welcome back, {result.Data.FullName.Split(' ')[0]}!");

        return IsSafeLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToHome();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (UserSession.IsAuthenticated) return RedirectToHome();

        return View(new RegisterRequest());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterRequest model)
    {
        if (model.Role is not ("Participant" or "Organiser"))
        {
            ModelState.AddModelError(nameof(model.Role), "Choose Participant or Organiser.");
        }
        if (!ModelState.IsValid) return ClearPasswords(model);

        var result = await Api.RegisterAsync(model);
        if (!result.Success)
        {
            AddApiErrors(result);
            return ClearPasswords(model);
        }

        Success("Your account is ready. Log in to get started.");
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    public IActionResult Logout()
    {
        UserSession.SignOut();
        Success("You have been logged out. See you at the start line!");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        if (!UserSession.IsAuthenticated) return RedirectToAction(nameof(Login));

        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectToHome() =>
        UserSession.IsOrganiser
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");

    private IActionResult ClearPasswords(RegisterRequest model)
    {
        model.Password = string.Empty;
        model.ConfirmPassword = string.Empty;
        return View(model);
    }

    private async Task LoadProfilePictureAsync()
    {
        var profile = await Api.GetProfileAsync();
        UserSession.ProfilePictureUrl = profile.Data?.ProfilePictureUrl;
    }
}
