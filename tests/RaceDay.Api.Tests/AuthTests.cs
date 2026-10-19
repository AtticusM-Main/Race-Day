using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.DTOs;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>
/// User registration, login, password hashing and access to protected endpoints with/without a token.
/// </summary>
public class AuthTests : ApiTestBase
{
    public AuthTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    [InlineData("organiser")] // role is not case sensitive
    public async Task Register_WithValidDetails_Returns201WithUserIdAndRole(string role)
    {
        var email = UniqueEmail("reg");

        var response = await RegisterAsync(email, role);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(Json);
        Assert.True(body!.UserId > 0);
        Assert.Equal(email, body.Email);
        Assert.Equal(role, body.Role, ignoreCase: true);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var email = UniqueEmail("hash");

        await RegisterAsync(email, "Participant");

        var stored = await Factory.QueryDbAsync(db => db.Users.SingleAsync(u => u.Email == email));
        Assert.NotEqual(Password, stored.PasswordHash);
        Assert.DoesNotContain(Password, stored.PasswordHash);
        Assert.True(stored.PasswordHash.Length > 40);
    }

    [Fact]
    public async Task Register_WithExistingEmail_Returns409()
    {
        var email = UniqueEmail("dup");
        await RegisterAsync(email, "Participant");

        var response = await RegisterAsync(email, "Organiser");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("")]
    public async Task Register_WithInvalidRole_Returns400(string role)
    {
        var response = await RegisterAsync(UniqueEmail("role"), role);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("short1A")]       // too short
    [InlineData("alllowercase1")] // no uppercase
    [InlineData("NoDigitsHere")]  // no digit
    public async Task Register_WithWeakPassword_Returns400(string password)
    {
        var response = await RegisterAsync(UniqueEmail("weak"), "Participant", password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmailAndMissingName_Returns400()
    {
        var response = await AnonymousClient().PostAsJsonAsync("/api/auth/register",
            new { email = "not-an-email", password = Password, role = "Participant" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokenAndRole()
    {
        var email = UniqueEmail("login");
        await RegisterAsync(email, "Organiser");

        var response = await LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Organiser", body.Role);
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithSeededAccount_Works()
    {
        var response = await LoginAsync("naledi.khumalo@raceday.co.za");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = UniqueEmail("wrongpw");
        await RegisterAsync(email, "Participant");

        var response = await LoginAsync(email, "WrongPassword1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var response = await LoginAsync(UniqueEmail("nobody"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await AnonymousClient().GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real.token");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTokenFromLogin_ReturnsOwnProfile()
    {
        var (client, userId) = await NewParticipantAsync();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(Json);
        Assert.Equal(userId, profile!.UserId);
        Assert.Equal("Participant", profile.Role);
    }

    [Fact]
    public async Task PublicEndpoint_WithoutToken_Returns200()
    {
        var response = await AnonymousClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task UpdateProfile_BothRoles_CanUpdateOwnProfile(string role)
    {
        var (client, _) = await NewUserClientAsync(role);
        var newEmail = UniqueEmail("updated");

        var response = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Updated Name", email = newEmail }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(Json);
        Assert.Equal("Updated Name", profile!.FullName);
        Assert.Equal(newEmail, profile.Email);
    }

    [Fact]
    public async Task UpdateProfile_WithInvalidData_Returns400()
    {
        var (client, _) = await NewParticipantAsync();

        var response = await client.PutAsJsonAsync("/api/users/me", new { fullName = "", email = "bad" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithoutToken_Returns401()
    {
        var response = await AnonymousClient().PutAsJsonAsync("/api/users/me",
            new { fullName = "Someone", email = UniqueEmail("x") }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
