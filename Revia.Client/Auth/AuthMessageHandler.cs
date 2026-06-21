using System.Net.Http.Headers;

namespace Revia.Client.Auth;

/// <summary>
/// Attaches the current bearer token to outgoing API requests (refreshing it when
/// near expiry). Registered on the "api" HttpClient used by all data clients.
/// </summary>
public sealed class AuthMessageHandler(AuthService auth) : DelegatingHandler
{
    private readonly AuthService _auth = auth;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
