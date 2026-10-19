using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Registration and login. These are the only public endpoints that change data.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthController(RaceDayDbContext db, IPasswordHasher<User> passwordHasher, ITokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Register a new account as an Organiser or a Participant.
    /// </summary>
    /// <remarks>
    /// The role must be "Organiser" or "Participant" (not case sensitive) and must exist in the Role table.
    /// The password is hashed (PBKDF2) before it is saved and is never stored as plain text.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user's id and role.</response>
    /// <response code="400">Invalid data (missing fields, weak password or unknown role).</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request)
    {
        var role = (await _db.Roles.ToListAsync())
            .FirstOrDefault(r => string.Equals(r.RoleName, request.Role.Trim(), StringComparison.OrdinalIgnoreCase));

        if (role is null)
        {
            ModelState.AddModelError(nameof(request.Role), $"Role must be '{Role.Organiser}' or '{Role.Participant}'.");
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered",
                detail: "An account with this email address already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            RoleId = role.RoleId,
            CreatedAt = DateTime.Now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = new RegisterResponse
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = role.RoleName
        };
        return CreatedAtAction(nameof(UsersController.GetMyProfile), "Users", null, response);
    }

    /// <summary>
    /// Log in and receive a JWT bearer token (the session token).
    /// </summary>
    /// <remarks>
    /// Copy the returned token into Swagger's **Authorize** button (or send it as
    /// `Authorization: Bearer {token}`) to call the protected endpoints. The token holds the
    /// user's id and role, so the API knows who they are on every later request until it expires.
    ///
    /// Seeded accounts (password `Password123!`): naledi.khumalo@raceday.co.za (Organiser),
    /// thandiwe.nkosi@example.com (Participant).
    /// </remarks>
    /// <response code="200">Login worked. Returns the token, its expiry and the user's role.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">Email or password is wrong.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        // Same message for "no such user" and "wrong password" so emails can't be probed.
        if (user is null ||
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login failed",
                detail: "Invalid email or password.");
        }

        var (token, expiresAt) = _tokenService.CreateToken(user, user.Role.RoleName);
        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.UserId,
            FullName = user.FullName,
            Role = user.Role.RoleName
        });
    }
}
