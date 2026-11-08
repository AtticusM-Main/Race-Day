using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class ResultTotalsTests : ApiTestBase
{
    public ResultTotalsTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task MyResults_IncludePositionAndTotalFinishersInCategory()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (first, _) = await NewParticipantAsync();
        var (second, _) = await NewParticipantAsync();
        var (third, _) = await NewParticipantAsync();
        var e1 = await EnrolAsync(first, category.CategoryId);
        var e2 = await EnrolAsync(second, category.CategoryId);
        await EnrolAsync(third, category.CategoryId);

        await organiser.PostAsJsonAsync("/api/results", new { enrolmentId = e1.EnrolmentId, finishTime = "00:40:00", position = 1 }, Json);
        await organiser.PostAsJsonAsync("/api/results", new { enrolmentId = e2.EnrolmentId, finishTime = "00:42:30", position = 2 }, Json);

        var mine = await second.GetFromJsonAsync<List<ResultResponse>>("/api/results/me", Json);

        var result = Assert.Single(mine!);
        Assert.Equal(2, result.Position);
        Assert.Equal(2, result.TotalFinishers);
    }

    [Fact]
    public async Task Leaderboard_ReportsTotalFinishersOnEveryRow()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (p1, _) = await NewParticipantAsync();
        var (p2, _) = await NewParticipantAsync();
        var e1 = await EnrolAsync(p1, category.CategoryId);
        var e2 = await EnrolAsync(p2, category.CategoryId);
        await organiser.PostAsJsonAsync("/api/results", new { enrolmentId = e1.EnrolmentId, finishTime = "00:40:00", position = 1 }, Json);
        await organiser.PostAsJsonAsync("/api/results", new { enrolmentId = e2.EnrolmentId, finishTime = "00:41:00", position = 2 }, Json);

        var board = await AnonymousClient().GetFromJsonAsync<List<ResultResponse>>($"/api/categories/{category.CategoryId}/results", Json);

        Assert.All(board!, r => Assert.Equal(2, r.TotalFinishers));
    }

    [Fact]
    public async Task EventEnrolments_ShowRecordedResultForEachParticipant()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (p1, _) = await NewParticipantAsync();
        var (p2, _) = await NewParticipantAsync();
        var withResult = await EnrolAsync(p1, category.CategoryId);
        var withoutResult = await EnrolAsync(p2, category.CategoryId);
        await organiser.PostAsJsonAsync("/api/results", new { enrolmentId = withResult.EnrolmentId, finishTime = "00:39:15", position = 1 }, Json);

        var enrolments = await organiser.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.EventId}/enrolments", Json);

        var recorded = enrolments!.Single(e => e.EnrolmentId == withResult.EnrolmentId);
        Assert.Equal(new TimeSpan(0, 39, 15), recorded.FinishTime);
        Assert.Equal(1, recorded.Position);
        Assert.NotNull(recorded.ResultId);
        Assert.Null(enrolments!.Single(e => e.EnrolmentId == withoutResult.EnrolmentId).ResultId);
    }
}
