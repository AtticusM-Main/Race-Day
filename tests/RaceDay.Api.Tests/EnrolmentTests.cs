using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.DTOs;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>
/// Participants entering events by choosing a category, and the enrolment being recorded correctly.
/// </summary>
public class EnrolmentTests : ApiTestBase
{
    public EnrolmentTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Participant_EnrolInCategory_Returns201AndRecordsParticipantEventAndCategory()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId, "21km");
        var (participant, participantId) = await NewParticipantAsync();

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentResponse>(Json);
        Assert.Equal(participantId, enrolment!.ParticipantId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal("21km", enrolment.CategoryName);

        var saved = await Factory.QueryDbAsync(db => db.Enrolments
            .Include(e => e.Category)
            .SingleAsync(e => e.EnrolmentId == enrolment.EnrolmentId));
        Assert.Equal(participantId, saved.ParticipantId);
        Assert.Equal(category.CategoryId, saved.CategoryId);
        Assert.Equal(ev.EventId, saved.Category.EventId);
    }

    [Fact]
    public async Task Participant_AfterEnrolling_SeesEnrolmentInOwnList()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/me", Json);

        var only = Assert.Single(mine!);
        Assert.Equal(enrolment.EnrolmentId, only.EnrolmentId);
    }

    [Fact]
    public async Task Organiser_SeesParticipantsEnrolledInOwnEvent()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, participantId) = await NewParticipantAsync();
        await EnrolAsync(participant, category.CategoryId);

        var response = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrolments = await response.Content.ReadFromJsonAsync<List<EnrolmentResponse>>(Json);
        var only = Assert.Single(enrolments!);
        Assert.Equal(participantId, only.ParticipantId);
    }

    [Fact]
    public async Task Participant_EnrolTwiceInSameCategory_Returns409()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        await EnrolAsync(participant, category.CategoryId);

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolInSecondCategoryOfSameEvent_Returns409()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var fiveK = await CreateCategoryAsync(organiser, ev.EventId, "5km");
        var tenK = await CreateCategoryAsync(organiser, ev.EventId, "10km");
        var (participant, _) = await NewParticipantAsync();
        await EnrolAsync(participant, fiveK.CategoryId);

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = tenK.CategoryId }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolInFullCategory_Returns409()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId, maxParticipants: 1);
        var (first, _) = await NewParticipantAsync();
        var (second, _) = await NewParticipantAsync();
        await EnrolAsync(first, category.CategoryId);

        var response = await second.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolInPastEvent_Returns400()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser, DateTime.Now.AddDays(-7));
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolInMissingCategory_Returns404()
    {
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = 999999 }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolWithoutCategory_Returns400()
    {
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_TryingToEnrol_Returns403AndNothingIsRecorded()
    {
        var (organiser, organiserId) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var response = await organiser.PostAsJsonAsync("/api/enrolments", new { categoryId = category.CategoryId }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await Factory.QueryDbAsync(db => db.Enrolments.AnyAsync(e => e.ParticipantId == organiserId)));
    }

    [Fact]
    public async Task Anonymous_TryingToEnrol_Returns401()
    {
        var response = await AnonymousClient().PostAsJsonAsync("/api/enrolments", new { categoryId = 1 }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CancelOwnEnrolment_Returns204AndRemovesIt()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await Factory.QueryDbAsync(db => db.Enrolments.AnyAsync(e => e.EnrolmentId == enrolment.EnrolmentId)));
    }

    [Fact]
    public async Task Participant_CancelSomeoneElsesEnrolment_Returns403()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (owner, _) = await NewParticipantAsync();
        var (otherParticipant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(owner, category.CategoryId);

        var response = await otherParticipant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
