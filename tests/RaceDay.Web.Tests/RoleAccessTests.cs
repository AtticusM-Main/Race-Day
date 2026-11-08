using System.Net;
using RaceDay.Web.Tests.Infrastructure;

namespace RaceDay.Web.Tests;

public class RoleAccessTests : IClassFixture<WebTestFactory>
{
    private readonly WebTestFactory _factory;

    public RoleAccessTests(WebTestFactory factory)
    {
        _factory = factory;
        _factory.Api.Down = false;
    }

    [Theory]
    [InlineData("/organiser/dashboard")]
    [InlineData("/organiser/events")]
    [InlineData("/organiser/events/create")]
    [InlineData("/organiser/events/1/categories")]
    [InlineData("/organiser/events/1/enrolments")]
    [InlineData("/organiser/events/1/results")]
    public async Task Participant_CannotOpenOrganiserPages(string url)
    {
        var browser = _factory.NewBrowser();
        await browser.LoginAsync(FakeRaceDayApi.ParticipantEmail);

        var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/Enrolments")]
    [InlineData("/Results")]
    [InlineData("/Events/Details/1")]
    public async Task Organiser_CannotOpenParticipantPages(string url)
    {
        var browser = _factory.NewBrowser();
        await browser.LoginAsync(FakeRaceDayApi.OrganiserEmail);

        var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Organiser_VisitingHome_IsSentToDashboard()
    {
        var browser = _factory.NewBrowser();
        await browser.LoginAsync(FakeRaceDayApi.OrganiserEmail);

        var response = await browser.GetAsync("/");

        Assert.Equal("/organiser/dashboard", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Participant_CannotPostOrganiserActions()
    {
        var browser = _factory.NewBrowser();
        await browser.LoginAsync(FakeRaceDayApi.ParticipantEmail);

        var response = await browser.PostFormAsync("/Enrolments", "/organiser/events/1/delete", new());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }
}
