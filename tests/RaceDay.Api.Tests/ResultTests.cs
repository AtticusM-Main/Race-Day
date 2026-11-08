using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>
/// Organisers capturing results, Participants viewing their own, and the public leaderboard.
/// </summary>
public class ResultTests : ApiTestBase
{
    public ResultTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Organiser, HttpClient Participant, EnrolmentResponse Enrolment)> SetUpEnrolmentAsync()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await NewParticipantAsync();
        var enrolment = await EnrolAsync(participant, category.CategoryId);
        return (organiser, participant, enrolment);
    }

    [Fact]
    public async Task Organiser_RecordsResult_AndParticipantSeesItInOwnResults()
    {
        var (organiser, participant, enrolment) = await SetUpEnrolmentAsync();

        var create = await organiser.PostAsJsonAsync("/api/results",
            new { enrolmentId = enrolment.EnrolmentId, finishTime = "00:45:12", position = 2 }, Json);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var mine = await participant.GetFromJsonAsync<List<ResultResponse>>("/api/results/me", Json);
        var result = Assert.Single(mine!);
        Assert.Equal(new TimeSpan(0, 45, 12), result.FinishTime);
        Assert.Equal(2, result.Position);
        Assert.Equal(enrolment.EventId, result.EventId);
    }

    [Fact]
    public async Task Organiser_RecordsSecondResultForSameEnrolment_Returns409()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();
        var body = new { enrolmentId = enrolment.EnrolmentId, finishTime = "00:45:12", position = 1 };
        await organiser.PostAsJsonAsync("/api/results", body, Json);

        var response = await organiser.PostAsJsonAsync("/api/results", body, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_RecordsResultForAnotherOrganisersEvent_Returns403()
    {
        var (_, _, enrolment) = await SetUpEnrolmentAsync();
        var (otherOrganiser, _) = await NewOrganiserAsync();

        var response = await otherOrganiser.PostAsJsonAsync("/api/results",
            new { enrolmentId = enrolment.EnrolmentId, finishTime = "00:45:12", position = 1 }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_RecordsZeroFinishTime_Returns400()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();

        var response = await organiser.PostAsJsonAsync("/api/results",
            new { enrolmentId = enrolment.EnrolmentId, finishTime = "00:00:00", position = 1 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_UpdatesResult_Returns200()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();
        var create = await organiser.PostAsJsonAsync("/api/results",
            new { enrolmentId = enrolment.EnrolmentId, finishTime = "00:45:12", position = 2 }, Json);
        var created = await create.Content.ReadFromJsonAsync<ResultResponse>(Json);

        var response = await organiser.PutAsJsonAsync($"/api/results/{created!.ResultId}",
            new { finishTime = "00:44:59", position = 1 }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ResultResponse>(Json);
        Assert.Equal(1, updated!.Position);
    }

    [Fact]
    public async Task Leaderboard_IsOrderedByPosition()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (slow, _) = await NewParticipantAsync();
        var (fast, _) = await NewParticipantAsync();
        var slowEnrolment = await EnrolAsync(slow, category.CategoryId);
        var fastEnrolment = await EnrolAsync(fast, category.CategoryId);
        await organiser.PostAsJsonAsync("/api/results",
            new { enrolmentId = slowEnrolment.EnrolmentId, finishTime = "01:05:00", position = 2 }, Json);
        await organiser.PostAsJsonAsync("/api/results",
            new { enrolmentId = fastEnrolment.EnrolmentId, finishTime = "00:39:00", position = 1 }, Json);

        var leaderboard = await AnonymousClient().GetFromJsonAsync<List<ResultResponse>>(
            $"/api/categories/{category.CategoryId}/results", Json);

        Assert.NotNull(leaderboard);
        Assert.Equal(new int?[] { 1, 2 }, leaderboard.Select(r => r.Position));
        Assert.Equal(fastEnrolment.EnrolmentId, leaderboard[0].EnrolmentId);
    }
}
