using System.Net.Http.Headers;

namespace RaceDay.Web.Services;

/// <summary>
/// Adds the signed-in user's API token (held in server-side session) to every outgoing API call.
/// </summary>
public class SessionTokenHandler : DelegatingHandler
{
    private readonly IUserSession _session;

    public SessionTokenHandler(IUserSession session)
    {
        _session = session;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_session.IsAuthenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
