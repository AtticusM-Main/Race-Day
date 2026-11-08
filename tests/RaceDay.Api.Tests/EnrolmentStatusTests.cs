using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class EnrolmentStatusTests : ApiTestBase
{
    public EnrolmentStatusTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task NewEnrolment_StartsAsPending()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();

        var enrolment = await EnrolAsync(participant, category.CategoryId);

        Assert.Equal(EnrolmentStatus.Pending, enrolment.Status);
    }

    [Fact]
    public async Task Organiser_ConfirmsEnrolment_StatusIsSaved()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new { status = "Confirmed" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/me", Json);
        Assert.Equal(EnrolmentStatus.Confirmed, Assert.Single(mine!).Status);
    }

    [Fact]
    public async Task Organiser_CannotChangeStatusOfAnotherOrganisersEvent()
    {
        var (owner, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(owner);
        var category = await CreateCategoryAsync(owner, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(participant, category.CategoryId);
        var (other, _) = await NewOrganiserAsync();

        var response = await other.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new { status = "Confirmed" }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotChangeEnrolmentStatus()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await participant.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new { status = "Confirmed" }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_UnknownEnrolment_Returns404()
    {
        var (organiser, _) = await NewOrganiserAsync();

        var response = await organiser.PutAsJsonAsync("/api/enrolments/999999/status", new { status = "Confirmed" }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_UnknownStatusValue_Returns400()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new { status = "Banana" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
