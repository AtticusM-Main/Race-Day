using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>
/// The only way the MVC application reads or writes data: every method is a call to the RaceDay REST API.
/// </summary>
public interface IRaceDayApi
{
    Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResult> RegisterAsync(RegisterRequest request);

    Task<ApiResult<UserProfile>> GetProfileAsync();
    Task<ApiResult<UserProfile>> UpdateProfileAsync(UpdateProfileRequest request);
    Task<ApiResult<UserProfile>> UploadProfilePictureAsync(IFormFile file);

    Task<ApiResult<List<EventDto>>> GetEventsAsync(EventFilter filter);
    Task<ApiResult<List<EventDto>>> GetMyEventsAsync();
    Task<ApiResult<EventDto>> GetEventAsync(int id);
    Task<ApiResult<EventDto>> CreateEventAsync(EventRequest request);
    Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventRequest request);
    Task<ApiResult> DeleteEventAsync(int id);
    Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file);

    Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryRequest request);
    Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryRequest request);
    Task<ApiResult> DeleteCategoryAsync(int categoryId);

    Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId);
    Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync();
    Task<ApiResult> CancelEnrolmentAsync(int enrolmentId);
    Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId);
    Task<ApiResult<EnrolmentDto>> SetEnrolmentStatusAsync(int enrolmentId, EnrolmentStatus status);

    Task<ApiResult<ResultDto>> RecordResultAsync(ResultRequest request);
    Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultRequest request);
    Task<ApiResult<List<ResultDto>>> GetMyResultsAsync();
}
