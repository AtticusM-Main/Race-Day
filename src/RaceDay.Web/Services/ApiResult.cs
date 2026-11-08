namespace RaceDay.Web.Services;

/// <summary>
/// Outcome of a call to the RaceDay API. Failures carry a message that is safe to show to users.
/// </summary>
public class ApiResult
{
    public int StatusCode { get; init; }
    public string? Error { get; init; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; init; } = new Dictionary<string, string[]>();

    public bool Success => Error is null;
    public bool IsUnauthorized => StatusCode == 401;
    public bool IsForbidden => StatusCode == 403;
    public bool IsNotFound => StatusCode == 404;

    public static ApiResult Ok(int statusCode = 200) => new() { StatusCode = statusCode };
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}
