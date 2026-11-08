using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public abstract class BaseController : Controller
{
    protected readonly IRaceDayApi Api;
    protected readonly IUserSession UserSession;

    protected BaseController(IRaceDayApi api, IUserSession userSession)
    {
        Api = api;
        UserSession = userSession;
    }

    protected void Success(string message) => TempData["Success"] = message;
    protected void Warning(string message) => TempData["Warning"] = message;
    protected void Failure(string message) => TempData["Error"] = message;

    /// <summary>
    /// Handles API failures that no page can recover from: an expired token ends the session,
    /// anything else becomes a friendly message and a redirect to <paramref name="fallback"/>.
    /// </summary>
    protected IActionResult? HandleFailure(ApiResult result, IActionResult fallback)
    {
        if (result.Success) return null;

        if (result.IsUnauthorized)
        {
            UserSession.SignOut();
            Warning("Your session has expired. Please log in again.");
            return RedirectToAction("Login", "Account");
        }

        if (result.IsNotFound)
        {
            return NotFound();
        }

        Failure(result.Error!);
        return fallback;
    }

    /// <summary>Copies API validation messages onto the form so they show next to the matching fields.</summary>
    protected void AddApiErrors(ApiResult result)
    {
        if (result.FieldErrors.Count > 0)
        {
            foreach (var (field, messages) in result.FieldErrors)
            {
                var key = field.Length > 0 ? char.ToUpperInvariant(field[0]) + field[1..] : string.Empty;
                foreach (var message in messages) ModelState.AddModelError(key, message);
            }
        }
        else if (result.Error is not null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
        }
    }

    protected static bool IsSafeLocalUrl(string? url) => !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
}
