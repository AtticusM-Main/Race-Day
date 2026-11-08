using Microsoft.AspNetCore.Http;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests.Infrastructure;

/// <summary>
/// An in-memory stand-in for the RaceDay API so the MVC pipeline (session, filters, views) can be tested without a server.
/// </summary>
public class FakeRaceDayApi : IRaceDayApi
{
    public const string OrganiserEmail = "organiser@test.co.za";
    public const string ParticipantEmail = "participant@test.co.za";
    public const string Password = "Password123!";

    public bool Down { get; set; }
    public List<RegisterRequest> Registered { get; } = new();
    public List<int> EnrolledCategoryIds { get; } = new();

    public List<EventDto> Events { get; } = new()
    {
        new EventDto
        {
            EventId = 1, Name = "Test Spring Run", Location = "Cape Town", Distance = 10, EventType = EventType.Run,
            EventDate = DateTime.Now.AddMonths(1), OrganiserId = 1, OrganiserName = "Olivia Organiser",
            Categories = { new CategoryDto { CategoryId = 11, EventId = 1, Name = "10km", Distance = 10, MaxParticipants = 100, EnrolledCount = 2 } }
        },
        new EventDto
        {
            EventId = 2, Name = "Test Beach Walk", Location = "Durban", Distance = 5, EventType = EventType.Walk,
            EventDate = DateTime.Now.AddMonths(2), OrganiserId = 1, OrganiserName = "Olivia Organiser"
        },
        new EventDto
        {
            EventId = 3, Name = "Test Open Event", Location = "Paarl", Distance = 21, EventType = EventType.Run,
            EventDate = DateTime.Now.AddMonths(3), OrganiserId = 1, OrganiserName = "Olivia Organiser",
            Categories =
            {
                new CategoryDto { CategoryId = 31, EventId = 3, Name = "21km", Distance = 21, MaxParticipants = 50, EnrolledCount = 10 },
                new CategoryDto { CategoryId = 32, EventId = 3, Name = "Full Category", MaxParticipants = 5, EnrolledCount = 5 }
            }
        }
    };

    private ApiResult<T> Offline<T>() => new() { StatusCode = 503, Error = "We can't reach the RaceDay service right now. Please try again in a moment." };

    public Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        if (Down) return Task.FromResult(Offline<LoginResponse>());

        var isOrganiser = request.Email == OrganiserEmail;
        var isParticipant = request.Email == ParticipantEmail;
        if ((!isOrganiser && !isParticipant) || request.Password != Password)
        {
            return Task.FromResult(new ApiResult<LoginResponse> { StatusCode = 401, Error = "Unauthorized" });
        }

        return Task.FromResult(new ApiResult<LoginResponse>
        {
            StatusCode = 200,
            Data = new LoginResponse
            {
                Token = "fake-token",
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                UserId = isOrganiser ? 1 : 2,
                FullName = isOrganiser ? "Olivia Organiser" : "Pat Participant",
                Role = isOrganiser ? "Organiser" : "Participant"
            }
        });
    }

    public Task<ApiResult> RegisterAsync(RegisterRequest request)
    {
        if (Down) return Task.FromResult<ApiResult>(Offline<object>());

        if (request.Email == "taken@test.co.za")
        {
            return Task.FromResult<ApiResult>(new ApiResult { StatusCode = 409, Error = "An account with this email already exists." });
        }

        Registered.Add(request);
        return Task.FromResult(ApiResult.Ok(201));
    }

    public Task<ApiResult<UserProfile>> GetProfileAsync() =>
        Task.FromResult(Down ? Offline<UserProfile>() : new ApiResult<UserProfile>
        {
            StatusCode = 200,
            Data = new UserProfile { UserId = 2, FullName = "Pat Participant", Email = ParticipantEmail, Role = "Participant" }
        });

    public Task<ApiResult<UserProfile>> UpdateProfileAsync(UpdateProfileRequest request) => Task.FromResult(Offline<UserProfile>());
    public Task<ApiResult<UserProfile>> UploadProfilePictureAsync(IFormFile file) => Task.FromResult(Offline<UserProfile>());

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(EventFilter filter)
    {
        if (Down) return Task.FromResult(Offline<List<EventDto>>());

        var events = Events.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter.Search)) events = events.Where(e => e.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        if (filter.Type is not null) events = events.Where(e => e.EventType == filter.Type);

        return Task.FromResult(new ApiResult<List<EventDto>> { StatusCode = 200, Data = events.ToList() });
    }

    public Task<ApiResult<List<EventDto>>> GetMyEventsAsync() =>
        Task.FromResult(Down ? Offline<List<EventDto>>() : new ApiResult<List<EventDto>> { StatusCode = 200, Data = Events.Where(e => e.OrganiserId == 1).ToList() });

    public Task<ApiResult<EventDto>> GetEventAsync(int id)
    {
        if (Down) return Task.FromResult(Offline<EventDto>());

        var ev = Events.FirstOrDefault(e => e.EventId == id);
        return Task.FromResult(ev is null
            ? new ApiResult<EventDto> { StatusCode = 404, Error = "We couldn't find what you were looking for." }
            : new ApiResult<EventDto> { StatusCode = 200, Data = ev });
    }

    public Task<ApiResult<EventDto>> CreateEventAsync(EventRequest request) => Task.FromResult(Offline<EventDto>());
    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventRequest request) => Task.FromResult(Offline<EventDto>());
    public Task<ApiResult> DeleteEventAsync(int id) => Task.FromResult<ApiResult>(Offline<object>());
    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) => Task.FromResult(Offline<EventDto>());
    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryRequest request) => Task.FromResult(Offline<CategoryDto>());
    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryRequest request) => Task.FromResult(Offline<CategoryDto>());
    public Task<ApiResult> DeleteCategoryAsync(int categoryId) => Task.FromResult<ApiResult>(Offline<object>());

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId)
    {
        EnrolledCategoryIds.Add(categoryId);
        return Task.FromResult(new ApiResult<EnrolmentDto>
        {
            StatusCode = 201,
            Data = new EnrolmentDto { EnrolmentId = 1, EventName = "Test Spring Run", CategoryName = "10km", Status = EnrolmentStatus.Pending }
        });
    }

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        Task.FromResult(Down ? Offline<List<EnrolmentDto>>() : new ApiResult<List<EnrolmentDto>>
        {
            StatusCode = 200,
            Data = new List<EnrolmentDto>
            {
                new() { EnrolmentId = 1, EventId = 1, EventName = "Test Spring Run", EventDate = DateTime.Now.AddMonths(1), CategoryName = "10km", Status = EnrolmentStatus.Pending },
                new() { EnrolmentId = 2, EventId = 2, EventName = "Test Beach Walk", EventDate = DateTime.Now.AddMonths(2), CategoryName = "5km", Status = EnrolmentStatus.Confirmed }
            }
        });

    public Task<ApiResult> CancelEnrolmentAsync(int enrolmentId) => Task.FromResult(ApiResult.Ok(204));
    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) => Task.FromResult(new ApiResult<List<EnrolmentDto>> { StatusCode = 200, Data = new() });
    public Task<ApiResult<EnrolmentDto>> SetEnrolmentStatusAsync(int enrolmentId, EnrolmentStatus status) => Task.FromResult(Offline<EnrolmentDto>());
    public Task<ApiResult<ResultDto>> RecordResultAsync(ResultRequest request) => Task.FromResult(Offline<ResultDto>());
    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultRequest request) => Task.FromResult(Offline<ResultDto>());

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        Task.FromResult(new ApiResult<List<ResultDto>>
        {
            StatusCode = 200,
            Data = new List<ResultDto>
            {
                new() { ResultId = 1, EventName = "Test Autumn 10K", EventDate = DateTime.Now.AddMonths(-2), CategoryName = "10km", FinishTime = new TimeSpan(0, 47, 30), Position = 47, TotalFinishers = 312 },
                new() { ResultId = 2, EventName = "Test Winter 5K", EventDate = DateTime.Now.AddMonths(-4), CategoryName = "5km", FinishTime = new TimeSpan(0, 24, 5), Position = 2, TotalFinishers = 80 },
                new() { ResultId = 3, EventName = "Test Spring 21K", EventDate = DateTime.Now.AddMonths(-6), CategoryName = "21km", FinishTime = new TimeSpan(1, 35, 20), Position = 12, TotalFinishers = 1 }
            }
        });
}
