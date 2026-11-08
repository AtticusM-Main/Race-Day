using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class ImageUploadTests : ApiTestBase
{
    public ImageUploadTests(RaceDayApiFactory factory) : base(factory)
    {
    }

    private static MultipartFormDataContent ImageForm(string fileName = "banner.png", string contentType = "image/png", int size = 2048)
    {
        var file = new ByteArrayContent(new byte[size]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Organiser_UploadsBanner_UrlIsSavedAndReturned()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>(Json);
        Assert.StartsWith("https://fake.blob.local/raceday-images/events/", updated!.BannerImageUrl);
        Assert.True(Factory.Images.Stored.ContainsKey(updated.BannerImageUrl!));

        var fetched = await AnonymousClient().GetFromJsonAsync<EventResponse>($"/api/events/{ev.EventId}", Json);
        Assert.Equal(updated.BannerImageUrl, fetched!.BannerImageUrl);
    }

    [Fact]
    public async Task Organiser_ReplacesBanner_OldImageIsDeleted()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var first = await (await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm()))
            .Content.ReadFromJsonAsync<EventResponse>(Json);

        await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm("new.jpg", "image/jpeg"));

        Assert.Contains(first!.BannerImageUrl!, Factory.Images.Deleted);
    }

    [Fact]
    public async Task Participant_CannotUploadEventBanner()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent()
    {
        var (owner, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(owner);
        var (other, _) = await NewOrganiserAsync();

        var response = await other.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_UnknownEvent_Returns404()
    {
        var (organiser, _) = await NewOrganiserAsync();

        var response = await organiser.PostAsync("/api/events/999999/banner", ImageForm());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("script.exe", "application/octet-stream")]
    [InlineData("fake.png", "text/plain")]
    public async Task UploadBanner_UnsupportedFile_Returns400(string fileName, string contentType)
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm(fileName, contentType));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_FileOverFiveMegabytes_Returns400()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm(size: 5 * 1024 * 1024 + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NoFile_Returns400()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_StorageDown_Returns503WithoutChangingEvent()
    {
        var (organiser, _) = await NewOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        Factory.Images.Unavailable = true;
        try
        {
            var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            Factory.Images.Unavailable = false;
        }

        var fetched = await organiser.GetFromJsonAsync<EventResponse>($"/api/events/{ev.EventId}", Json);
        Assert.Null(fetched!.BannerImageUrl);
    }

    [Fact]
    public async Task User_UploadsProfilePicture_UrlAppearsOnProfile()
    {
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.PostAsync("/api/users/me/picture", ImageForm("me.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await participant.GetFromJsonAsync<UserProfileResponse>("/api/users/me", Json);
        Assert.StartsWith("https://fake.blob.local/raceday-images/profiles/", profile!.ProfilePictureUrl);
    }

    [Fact]
    public async Task UploadProfilePicture_WithoutToken_Returns401()
    {
        var response = await AnonymousClient().PostAsync("/api/users/me/picture", ImageForm());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
