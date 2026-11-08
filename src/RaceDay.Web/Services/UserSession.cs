using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

public interface IUserSession
{
    bool IsAuthenticated { get; }
    string? Token { get; }
    string? Role { get; }
    int UserId { get; }
    string FullName { get; }
    string? ProfilePictureUrl { get; set; }
    bool IsOrganiser { get; }
    bool IsParticipant { get; }

    void SignIn(LoginResponse login);
    void SignOut();
}

/// <summary>
/// The signed-in user's state, kept in server-side ASP.NET Core session. The browser only holds the session cookie;
/// the API token and role never leave the server.
/// </summary>
public class UserSession : IUserSession
{
    private const string TokenKey = "token";
    private const string RoleKey = "role";
    private const string UserIdKey = "userId";
    private const string NameKey = "name";
    private const string PictureKey = "picture";
    private const string ExpiresKey = "expires";

    private readonly IHttpContextAccessor _accessor;

    public UserSession(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ISession? Session => _accessor.HttpContext?.Session;

    public bool IsAuthenticated
    {
        get
        {
            var token = Token;
            if (string.IsNullOrEmpty(token)) return false;
            if (!DateTime.TryParse(Session?.GetString(ExpiresKey), null, System.Globalization.DateTimeStyles.RoundtripKind, out var expires)) return false;
            return expires > DateTime.UtcNow;
        }
    }

    public string? Token => Session?.GetString(TokenKey);
    public string? Role => Session?.GetString(RoleKey);
    public int UserId => Session?.GetInt32(UserIdKey) ?? 0;
    public string FullName => Session?.GetString(NameKey) ?? string.Empty;

    public string? ProfilePictureUrl
    {
        get => Session?.GetString(PictureKey);
        set
        {
            if (value is null) Session?.Remove(PictureKey);
            else Session?.SetString(PictureKey, value);
        }
    }

    public bool IsOrganiser => IsAuthenticated && Role == "Organiser";
    public bool IsParticipant => IsAuthenticated && Role == "Participant";

    public void SignIn(LoginResponse login)
    {
        Session!.Clear();
        Session.SetString(TokenKey, login.Token);
        Session.SetString(RoleKey, login.Role);
        Session.SetInt32(UserIdKey, login.UserId);
        Session.SetString(NameKey, login.FullName);
        Session.SetString(ExpiresKey, login.ExpiresAt.ToUniversalTime().ToString("o"));
    }

    public void SignOut() => Session?.Clear();
}
