using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Extensions;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// The logged-in user's own profile. Available to both roles.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _images;

    public UsersController(RaceDayDbContext db, IImageStorage images)
    {
        _db = db;
        _images = images;
    }

    /// <summary>
    /// Get the logged-in user's profile.
    /// </summary>
    /// <response code="200">The user's profile details.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetMyProfile()
    {
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == User.GetUserId());

        // The token is valid but the account no longer exists.
        if (user is null) return Unauthorized();

        return Ok(user.ToProfile());
    }

    /// <summary>
    /// Update the logged-in user's name and email.
    /// </summary>
    /// <remarks>The role cannot be changed here.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="409">Another account already uses this email.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> UpdateMyProfile(UpdateProfileRequest request)
    {
        var userId = User.GetUserId();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null) return Unauthorized();

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.UserId != userId))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered",
                detail: "Another account already uses this email address.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        await _db.SaveChangesAsync();

        return Ok(user.ToProfile());
    }

    /// <summary>
    /// Upload (or replace) the logged-in user's profile picture.
    /// </summary>
    /// <remarks>Send the image as multipart/form-data in a field named "file". JPG, PNG or WebP, up to 5 MB. The file is stored in Azure Blob Storage.</remarks>
    /// <param name="file">The profile picture.</param>
    /// <response code="200">The updated profile, including the picture URL.</response>
    /// <response code="400">Missing, too large or unsupported image.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="503">Cloud storage is not available.</response>
    [HttpPost("me/picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageUploadValidator.MaxBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserProfileResponse>> UploadProfilePicture(IFormFile? file)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == User.GetUserId());
        if (user is null) return Unauthorized();

        var error = ImageUploadValidator.Validate(file, out var extension, out var contentType);
        if (error is not null)
        {
            ModelState.AddModelError(nameof(file), error);
            return ValidationProblem(ModelState);
        }

        string url;
        try
        {
            await using var stream = file!.OpenReadStream();
            url = await _images.UploadAsync(stream, "profiles", extension, contentType);
        }
        catch (ImageStorageUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Image storage unavailable", detail: ex.Message);
        }

        var oldUrl = user.ProfilePictureUrl;
        user.ProfilePictureUrl = url;
        await _db.SaveChangesAsync();
        await _images.DeleteAsync(oldUrl);

        return Ok(user.ToProfile());
    }
}
