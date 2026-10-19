using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>
/// Organisers managing events and categories, and everyone being able to view them.
/// </summary>
public class EventManagementTests : ApiTestBase
{
    public EventManagementTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Organiser_CreateEvent_Returns201AndSavesEventWithOrganiserAsOwner()
    {
        var (organiser, organiserId) = await NewOrganiserAsync();

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Pretoria Jacaranda Walk",
            description = "Community walk.",
            eventDate = DateTime.Now.AddMonths(1),
            location = "Pretoria, South Africa",
            distance = 8.5m,
            eventType = "Walk"
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>(Json);
        Assert.Equal("Pretoria Jacaranda Walk", created!.Name);
        Assert.Equal(EventType.Walk, created.EventType);
        Assert.Equal(8.5m, created.Distance);
        Assert.Equal(organiserId, created.OrganiserId);

        var saved = await Factory.QueryDbAsync(db => db.Events.SingleAsync(e => e.EventId == created.EventId));
        Assert.Equal(organiserId, saved.OrganiserId);
        Assert.Equal("Pretoria, South Africa", saved.Location);
    }

    [Theory]
    [InlineData("Run")]
    [InlineData("Walk")]
    [InlineData("Cycle")]
    public async Task Organiser_CreateEvent_AcceptsEveryEventType(string eventType)
    {
        var (organiser, _) = await NewOrganiserAsync();

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = $"{eventType} Event",
            eventDate = DateTime.Now.AddMonths(1),
            location = "Durban",
            distance = 10m,
            eventType
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithMissingFields_Returns400()
    {
        var (organiser, _) = await NewOrganiserAsync();

        var response = await organiser.PostAsJsonAsync("/api/events", new { description = "No name, date, location..." }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithUnknownEventType_Returns400()
    {
        var (organiser, _) = await NewOrganiserAsync();

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Swim Gala",
            eventDate = DateTime.Now.AddMonths(1),
            location = "Durban",
            distance = 1m,
            eventType = "Swim"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CreateEvent_Returns401()
    {
        var response = await AnonymousClient().PostAsJsonAsync("/api/events", ValidEventBody(), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdateOwnEvent_Returns200AndSavesChanges()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Renamed Event",
            description = "Updated",
            eventDate = ev.EventDate.AddDays(7),
            location = "Stellenbosch, South Africa",
            distance = 21.1m,
            eventType = "Cycle"
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await Factory.QueryDbAsync(db => db.Events.SingleAsync(e => e.EventId == ev.EventId));
        Assert.Equal("Renamed Event", saved.Name);
        Assert.Equal("Stellenbosch, South Africa", saved.Location);
        Assert.Equal(EventType.Cycle, saved.EventType);
    }

    [Fact]
    public async Task Organiser_UpdateAnotherOrganisersEvent_Returns403AndLeavesEventUnchanged()
    {
        var (owner, _) = await NewOrganiserAsync();
        var (otherOrganiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(owner);

        var response = await otherOrganiser.PutAsJsonAsync($"/api/events/{ev.EventId}", ValidEventBody("Hijacked"), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var saved = await Factory.QueryDbAsync(db => db.Events.SingleAsync(e => e.EventId == ev.EventId));
        Assert.Equal(ev.Name, saved.Name);
    }

    [Fact]
    public async Task Organiser_UpdateMissingEvent_Returns404()
    {
        var (organiser, _) = await NewOrganiserAsync();

        var response = await organiser.PutAsJsonAsync("/api/events/999999", ValidEventBody(), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeleteOwnEvent_Returns204AndEventIsGone()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, ev.EventId);

        var response = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var lookup = await AnonymousClient().GetAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await NewOrganiserAsync();
        var (otherOrganiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(owner);

        var response = await otherOrganiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await Factory.QueryDbAsync(db => db.Events.AnyAsync(e => e.EventId == ev.EventId)));
    }

    [Fact]
    public async Task Organiser_DeleteMissingEvent_Returns404()
    {
        var (organiser, _) = await NewOrganiserAsync();

        var response = await organiser.DeleteAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnyoneCanViewEvents_IncludingSeededEvents()
    {
        var response = await AnonymousClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<EventResponse>>(Json);
        Assert.Contains(events!, e => e.Name == "Cape Town Spring Run");
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task BothRoles_CanViewASingleEvent(string role)
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var (viewer, _) = await NewUserClientAsync(role);

        var response = await viewer.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMissingEvent_Returns404()
    {
        var response = await AnonymousClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_AddsAgeAndDistanceCategories_AndParticipantCanViewThem()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var ageCategory = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Under 20" }, Json);
        var distanceCategory = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "21km", distance = 21.1m, maxParticipants = 150 }, Json);

        Assert.Equal(HttpStatusCode.Created, ageCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Created, distanceCategory.StatusCode);

        var (participant, _) = await NewParticipantAsync();
        var categories = await participant.GetFromJsonAsync<List<CategoryResponse>>(
            $"/api/events/{ev.EventId}/categories", Json);
        Assert.Equal(new[] { "Under 20", "21km" }, categories!.Select(c => c.Name));
    }

    [Fact]
    public async Task Organiser_AddCategoryToAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await NewOrganiserAsync();
        var (otherOrganiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(owner);

        var response = await otherOrganiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Senior" }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_AddDuplicateCategoryName_Returns409()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, ev.EventId, "Senior");

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "senior" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdateAndDeleteOwnCategory_Succeeds()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var update = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new { name = "10km Open", distance = 10m, maxParticipants = 50 }, Json);
        var delete = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task GetCategoriesForMissingEvent_Returns404()
    {
        var response = await AnonymousClient().GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
