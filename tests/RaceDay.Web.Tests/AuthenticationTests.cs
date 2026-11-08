using System.Net;
using RaceDay.Web.Tests.Infrastructure;

namespace RaceDay.Web.Tests;

public class AuthenticationTests : IClassFixture<WebTestFactory>
{
    private readonly WebTestFactory _factory;

    public AuthenticationTests(WebTestFactory factory)
    {
        _factory = factory;
        _factory.Api.Down = false;
    }

    [Theory]
    [InlineData("/Events/Details/1")]
    [InlineData("/Enrolments")]
    [InlineData("/Results")]
    [InlineData("/Profile")]
    [InlineData("/organiser/dashboard")]
    [InlineData("/organiser/events")]
    [InlineData("/organiser/events/1/enrolments")]
    [InlineData("/organiser/events/1/results")]
    public async Task Visitor_OpeningProtectedPage_IsRedirectedToLogin(string url)
    {
        var browser = _factory.NewBrowser();

        var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Visitor_SeesLandingPage()
    {
        var html = await _factory.NewBrowser().GetStringAsync("/");

        Assert.Contains("Your next", html);
        Assert.Contains("Register", html);
    }

    [Fact]
    public async Task Participant_LogsIn_AndSeesParticipantNavigation()
    {
        var browser = _factory.NewBrowser();
        await browser.LoginAsync(FakeRaceDayApi.ParticipantEmail);

        var html = await browser.GetStringAsync("/");

        Assert.Contains("My Enrolments", html);
        Assert.Contains("My Results", html);
        Assert.DoesNotContain("Manage Events", html);
        Assert.Contains("Pat Participant", html);
    }

    [Fact]
    public async Task Organiser_LogsIn_AndLandsOnDashboardWithOrganiserNavigation()
    {
        var browser = _factory.NewBrowser();
        var login = await browser.PostFormAsync("/Account/Login", "/Account/Login", new()
        {
            ["Email"] = FakeRaceDayApi.OrganiserEmail,
            ["Password"] = FakeRaceDayApi.Password
        });

        Assert.Equal("/organiser/dashboard", login.Headers.Location!.OriginalString);
        var html = await browser.GetStringAsync("/organiser/dashboard");
        Assert.Contains("Manage Events", html);
        Assert.DoesNotContain("My Enrolments", html);
    }

    [Fact]
    public async Task Login_WrongPassword_ShowsFriendlyMessageAndNoSession()
    {
        var browser = _factory.NewBrowser();

        var response = await browser.PostFormAsync("/Account/Login", "/Account/Login", new()
        {
            ["Email"] = FakeRaceDayApi.ParticipantEmail,
            ["Password"] = "WrongPassword1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("don&#x27;t match", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Enrolments")).StatusCode);
    }

    [Fact]
    public async Task Login_ApiUnavailable_ShowsFriendlyMessage()
    {
        _factory.Api.Down = true;
        var browser = _factory.NewBrowser();

        var response = await browser.PostFormAsync("/Account/Login", "/Account/Login", new()
        {
            ["Email"] = FakeRaceDayApi.ParticipantEmail,
            ["Password"] = FakeRaceDayApi.Password
        });

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("can&#x27;t reach the RaceDay service", html);
        Assert.DoesNotContain("Exception", html);
    }

    [Fact]
    public async Task Logout_EndsSession_AndProtectedPagesAreBlockedAgain()
    {
        var browser = _factory.NewBrowser();
        await browser.LoginAsync(FakeRaceDayApi.ParticipantEmail);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Enrolments")).StatusCode);

        var logout = await browser.PostFormAsync("/Enrolments", "/Account/Logout", new());

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.StartsWith("/Account/Login", logout.Headers.Location!.OriginalString);
        var after = await browser.GetAsync("/Enrolments");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("/Account/Login", after.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        var response = await _factory.NewBrowser().PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = FakeRaceDayApi.ParticipantEmail,
            ["Password"] = FakeRaceDayApi.Password
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnUrl_SendsUserBackToRequestedPage()
    {
        var browser = _factory.NewBrowser();
        var token = await browser.GetTokenAsync("/Account/Login?returnUrl=%2FResults");

        var response = await browser.PostAsync("/Account/Login?returnUrl=%2FResults", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = FakeRaceDayApi.ParticipantEmail,
            ["Password"] = FakeRaceDayApi.Password,
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal("/Results", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Login_ExternalReturnUrl_IsIgnored()
    {
        var browser = _factory.NewBrowser();
        var token = await browser.GetTokenAsync("/Account/Login");

        var response = await browser.PostAsync("/Account/Login?returnUrl=https%3A%2F%2Fevil.example", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = FakeRaceDayApi.ParticipantEmail,
            ["Password"] = FakeRaceDayApi.Password,
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Register_ValidDetails_SendsUserToLogin()
    {
        var browser = _factory.NewBrowser();

        var response = await browser.PostFormAsync("/Account/Register", "/Account/Register", new()
        {
            ["FullName"] = "New Runner",
            ["Email"] = "new.runner@test.co.za",
            ["Password"] = "Password123!",
            ["ConfirmPassword"] = "Password123!",
            ["Role"] = "Participant"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.OriginalString);
        Assert.Contains(_factory.Api.Registered, r => r.Email == "new.runner@test.co.za" && r.Role == "Participant");
    }

    [Fact]
    public async Task Register_WeakPasswordAndMismatch_IsRejectedBeforeCallingApi()
    {
        var browser = _factory.NewBrowser();
        var before = _factory.Api.Registered.Count;

        var response = await browser.PostFormAsync("/Account/Register", "/Account/Register", new()
        {
            ["FullName"] = "Weak Password",
            ["Email"] = "weak@test.co.za",
            ["Password"] = "short",
            ["ConfirmPassword"] = "different",
            ["Role"] = "Participant"
        });

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("at least 8 characters", html);
        Assert.Contains("passwords do not match", html);
        Assert.Equal(before, _factory.Api.Registered.Count);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShowsApiMessage()
    {
        var browser = _factory.NewBrowser();

        var response = await browser.PostFormAsync("/Account/Register", "/Account/Register", new()
        {
            ["FullName"] = "Duplicate Person",
            ["Email"] = "taken@test.co.za",
            ["Password"] = "Password123!",
            ["ConfirmPassword"] = "Password123!",
            ["Role"] = "Participant"
        });

        Assert.Contains("already exists", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_UnknownRole_IsRejected()
    {
        var browser = _factory.NewBrowser();
        var before = _factory.Api.Registered.Count;

        var response = await browser.PostFormAsync("/Account/Register", "/Account/Register", new()
        {
            ["FullName"] = "Sneaky Person",
            ["Email"] = "sneaky@test.co.za",
            ["Password"] = "Password123!",
            ["ConfirmPassword"] = "Password123!",
            ["Role"] = "Admin"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, _factory.Api.Registered.Count);
    }
}
