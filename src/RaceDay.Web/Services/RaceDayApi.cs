using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

public class RaceDayApi : IRaceDayApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly ILogger<RaceDayApi> _logger;

    public RaceDayApi(HttpClient http, ILogger<RaceDayApi> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new { email = request.Email.Trim(), password = request.Password });

    public async Task<ApiResult> RegisterAsync(RegisterRequest request) =>
        await SendAsync<object>(HttpMethod.Post, "api/auth/register",
            new { fullName = request.FullName.Trim(), email = request.Email.Trim(), password = request.Password, role = request.Role });

    public Task<ApiResult<UserProfile>> GetProfileAsync() => SendAsync<UserProfile>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserProfile>> UpdateProfileAsync(UpdateProfileRequest request) =>
        SendAsync<UserProfile>(HttpMethod.Put, "api/users/me", new { fullName = request.FullName.Trim(), email = request.Email.Trim() });

    public Task<ApiResult<UserProfile>> UploadProfilePictureAsync(IFormFile file) =>
        UploadAsync<UserProfile>("api/users/me/picture", file);

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(EventFilter filter)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        if (filter.Type is not null) query.Add($"type={filter.Type}");
        if (filter.From is not null) query.Add($"from={filter.From.Value:yyyy-MM-dd}");
        if (filter.To is not null) query.Add($"to={filter.To.Value:yyyy-MM-dd}");
        if (filter.UpcomingOnly) query.Add("upcomingOnly=true");

        var path = query.Count == 0 ? "api/events" : "api/events?" + string.Join('&', query);
        return SendAsync<List<EventDto>>(HttpMethod.Get, path);
    }

    public Task<ApiResult<List<EventDto>>> GetMyEventsAsync() => SendAsync<List<EventDto>>(HttpMethod.Get, "api/events/mine");
    public Task<ApiResult<EventDto>> GetEventAsync(int id) => SendAsync<EventDto>(HttpMethod.Get, $"api/events/{id}");
    public Task<ApiResult<EventDto>> CreateEventAsync(EventRequest request) => SendAsync<EventDto>(HttpMethod.Post, "api/events", EventBody(request));
    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventRequest request) => SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", EventBody(request));
    public async Task<ApiResult> DeleteEventAsync(int id) => await SendAsync<object>(HttpMethod.Delete, $"api/events/{id}");
    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) => UploadAsync<EventDto>($"api/events/{eventId}/banner", file);

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryRequest request) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", CategoryBody(request));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryRequest request) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", CategoryBody(request));

    public async Task<ApiResult> DeleteCategoryAsync(int categoryId) => await SendAsync<object>(HttpMethod.Delete, $"api/categories/{categoryId}");

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) => SendAsync<EnrolmentDto>(HttpMethod.Post, "api/enrolments", new { categoryId });
    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() => SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/enrolments/me");
    public async Task<ApiResult> CancelEnrolmentAsync(int enrolmentId) => await SendAsync<object>(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");
    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) => SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult<EnrolmentDto>> SetEnrolmentStatusAsync(int enrolmentId, EnrolmentStatus status) =>
        SendAsync<EnrolmentDto>(HttpMethod.Put, $"api/enrolments/{enrolmentId}/status", new { status = status.ToString() });

    public Task<ApiResult<ResultDto>> RecordResultAsync(ResultRequest request) =>
        SendAsync<ResultDto>(HttpMethod.Post, "api/results", new { enrolmentId = request.EnrolmentId, finishTime = FormatTime(request.FinishTime), position = request.Position });

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultRequest request) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}", new { finishTime = FormatTime(request.FinishTime), position = request.Position });

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() => SendAsync<List<ResultDto>>(HttpMethod.Get, "api/results/me");

    private static string? FormatTime(TimeSpan? time) => time?.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    private static object EventBody(EventRequest r) => new
    {
        name = r.Name.Trim(),
        description = r.Description,
        eventDate = r.EventDate,
        location = r.Location.Trim(),
        distance = r.Distance,
        eventType = r.EventType?.ToString()
    };

    private static object CategoryBody(CategoryRequest r) => new { name = r.Name.Trim(), distance = r.Distance, maxParticipants = r.MaxParticipants };

    private async Task<ApiResult<T>> UploadAsync<T>(string path, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var content = new MultipartFormDataContent();
        var part = new StreamContent(stream);
        part.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(part, "file", file.FileName);

        return await SendAsync<T>(HttpMethod.Post, path, content: content);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (content is not null) request.Content = content;
        else if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        try
        {
            using var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var data = response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object)
                    ? default
                    : await response.Content.ReadFromJsonAsync<T>(Json);
                return new ApiResult<T> { StatusCode = (int)response.StatusCode, Data = data };
            }

            return await FailureAsync<T>(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Calling the RaceDay API failed: {Method} {Path}", method, path);
            return new ApiResult<T>
            {
                StatusCode = 503,
                Error = "We can't reach the RaceDay service right now. Please try again in a moment."
            };
        }
    }

    private async Task<ApiResult<T>> FailureAsync<T>(HttpResponseMessage response)
    {
        string? detail = null;
        var fieldErrors = new Dictionary<string, string[]>();

        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        fieldErrors[field.Name] = field.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                    }
                }
                else if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    detail = d.GetString();
                }
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning("The API returned an unreadable error body with status {Status}.", (int)response.StatusCode);
        }

        var status = (int)response.StatusCode;
        var message = status switch
        {
            400 when fieldErrors.Count > 0 => fieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? "Please check the details you entered.",
            400 => detail ?? "Please check the details you entered.",
            401 => "Your session has expired. Please log in again.",
            403 => "You don't have permission to do that.",
            404 => "We couldn't find what you were looking for.",
            409 => detail ?? "That change conflicts with existing information.",
            >= 500 => detail ?? "Something went wrong on our side. Please try again shortly.",
            _ => detail ?? "Something went wrong. Please try again."
        };

        return new ApiResult<T> { StatusCode = status, Error = message, FieldErrors = fieldErrors };
    }
}
