using System.Net;
using RaceDay.Web.Services;
using RaceDay.Web.Tests.Infrastructure;

namespace RaceDay.Web.Tests;

public class ParticipantPageTests : IClassFixture<WebTestFactory>
{
    private readonly WebTestFactory _factory;

    public ParticipantPageTests(WebTestFactory factory)
    {
        _factory = factory;
        _factory.Api.Down = false;
    }

    private async Task<HttpClient> ParticipantAsync()
    {
        var browser = _factory.NewBrowser();
        await browser.LoginAsync(FakeRaceDayApi.ParticipantEmail);
        return browser;
    }

    [Fact]
    public async Task Home_ListsUpcomingEvents()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/");

        Assert.Contains("Test Spring Run", html);
        Assert.Contains("Test Beach Walk", html);
    }

    [Fact]
    public async Task Home_FilterByType_NarrowsTheList()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/?Type=Walk");

        Assert.Contains("Test Beach Walk", html);
        Assert.DoesNotContain("Test Spring Run", html);
    }

    [Fact]
    public async Task Home_FilterWithNoMatches_ShowsEmptyState()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/?Search=zzzz");

        Assert.Contains("No events found", html);
    }

    [Fact]
    public async Task Home_ApiUnavailable_ShowsFriendlyMessage()
    {
        var browser = await ParticipantAsync();
        _factory.Api.Down = true;

        var response = await browser.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("can&#x27;t reach the RaceDay service", html);
        Assert.DoesNotContain("Exception", html);
    }

    [Fact]
    public async Task EventDetails_ShowsCategoriesAndEntryForm()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/Events/Details/3");

        Assert.Contains("Test Open Event", html);
        Assert.Contains("21km", html);
        Assert.Contains("Enter this event", html);
        Assert.Contains("disabled", html);
    }

    [Fact]
    public async Task EventDetails_AlreadyEntered_ShowsCurrentEnrolmentInsteadOfForm()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/Events/Details/1");

        Assert.Contains("You are entered in", html);
        Assert.DoesNotContain("Enter this event", html);
    }

    [Fact]
    public async Task EventDetails_UnknownEvent_ShowsNotFoundPage()
    {
        var response = await (await ParticipantAsync()).GetAsync("/Events/Details/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Off the course", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Enrol_SendsChosenCategoryToApiAndShowsConfirmation()
    {
        var browser = await ParticipantAsync();

        var response = await browser.PostFormAsync("/Events/Details/1", "/Enrolments/Create", new()
        {
            ["eventId"] = "1",
            ["categoryId"] = "11"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(11, _factory.Api.EnrolledCategoryIds);
        var page = await browser.GetStringAsync("/Enrolments");
        Assert.Contains("You&#x27;re in!", page);
    }

    [Fact]
    public async Task Enrol_WithoutCategory_DoesNotCallApi()
    {
        var browser = await ParticipantAsync();
        var before = _factory.Api.EnrolledCategoryIds.Count;

        var response = await browser.PostFormAsync("/Events/Details/1", "/Enrolments/Create", new() { ["eventId"] = "1" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(before, _factory.Api.EnrolledCategoryIds.Count);
    }

    [Fact]
    public async Task MyEnrolments_ShowColourCodedStatuses()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/Enrolments");

        Assert.Contains("rd-status-pending", html);
        Assert.Contains("rd-status-confirmed", html);
    }

    [Fact]
    public async Task MyResults_ShowsPositionOutOfTotalFinishers()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/Results");

        Assert.Contains("47th", html);
        Assert.Contains("out of 312 finishers", html);
        Assert.Contains("00:47:30", html);
        Assert.Contains("2nd", html);
    }

    [Fact]
    public async Task MyResults_HidesTotalWhenFewerResultsRecordedThanPosition()
    {
        var html = await (await ParticipantAsync()).GetStringAsync("/Results");

        Assert.Contains("12th", html);
        Assert.DoesNotContain("out of 1 finisher", html);
    }

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(47, "47th")]
    [InlineData(101, "101st")]
    [InlineData(112, "112th")]
    public void Ordinal_FormatsPositions(int number, string expected)
    {
        Assert.Equal(expected, Formatting.Ordinal(number));
    }
}
