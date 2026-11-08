using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class EventFilterTests : ApiTestBase
{
    public EventFilterTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    private static async Task<EventResponse> CreateAsync(HttpClient organiser, string name, string type, string location, DateTime date)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name,
            description = "Filter test event.",
            eventDate = date,
            location,
            distance = 10m,
            eventType = type
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EventResponse>(Json))!;
    }

    [Fact]
    public async Task Events_FilterByType_ReturnsOnlyThatType()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateAsync(organiser, $"Ride {tag}", "Cycle", "Stellenbosch", DateTime.Now.AddMonths(3));
        await CreateAsync(organiser, $"Run {tag}", "Run", "Stellenbosch", DateTime.Now.AddMonths(3));

        var events = await AnonymousClient().GetFromJsonAsync<List<EventResponse>>($"/api/events?search={tag}&type=Cycle", Json);

        var only = Assert.Single(events!);
        Assert.Equal(EventType.Cycle, only.EventType);
    }

    [Fact]
    public async Task Events_Search_MatchesNameOrLocation()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateAsync(organiser, $"Alpha {tag}", "Run", "Nowhereville", DateTime.Now.AddMonths(3));
        await CreateAsync(organiser, "Beta Event", "Run", $"Place{tag}", DateTime.Now.AddMonths(3));

        var events = await AnonymousClient().GetFromJsonAsync<List<EventResponse>>($"/api/events?search={tag}", Json);

        Assert.Equal(2, events!.Count);
    }

    [Fact]
    public async Task Events_UpcomingOnly_HidesPastEvents()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateAsync(organiser, $"Past {tag}", "Run", "Cape Town", DateTime.Now.AddMonths(-2));
        var future = await CreateAsync(organiser, $"Future {tag}", "Run", "Cape Town", DateTime.Now.AddMonths(2));

        var events = await AnonymousClient().GetFromJsonAsync<List<EventResponse>>($"/api/events?search={tag}&upcomingOnly=true", Json);

        Assert.Equal(future.EventId, Assert.Single(events!).EventId);
    }

    [Fact]
    public async Task Events_DateRange_ReturnsEventsInsideTheRange()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var inside = await CreateAsync(organiser, $"Inside {tag}", "Run", "Paarl", new DateTime(2031, 5, 10, 7, 0, 0));
        await CreateAsync(organiser, $"Outside {tag}", "Run", "Paarl", new DateTime(2031, 8, 10, 7, 0, 0));

        var events = await AnonymousClient().GetFromJsonAsync<List<EventResponse>>(
            $"/api/events?search={tag}&from=2031-05-01&to=2031-05-31", Json);

        Assert.Equal(inside.EventId, Assert.Single(events!).EventId);
    }

    [Fact]
    public async Task MyEvents_ReturnsOnlyEventsOwnedByTheOrganiser()
    {
        var (mine, _) = await NewOrganiserAsync();
        var (other, _) = await NewOrganiserAsync();
        var own = await CreateEventAsync(mine);
        await CreateEventAsync(other);

        var events = await mine.GetFromJsonAsync<List<EventResponse>>("/api/events/mine", Json);

        Assert.Equal(own.EventId, Assert.Single(events!).EventId);
    }

    [Fact]
    public async Task MyEvents_Participant_Returns403()
    {
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyEvents_Anonymous_Returns401()
    {
        var response = await AnonymousClient().GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
