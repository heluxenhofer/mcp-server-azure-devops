using DevOpsMcp.Server.Services;

namespace DevOpsMcp.Server;

/// <summary>
/// DelegatingHandler that injects an Azure DevOps access token into outgoing HTTP requests.
/// </summary>
public class DevOpsTokenHandler(TokenService tokenService) : DelegatingHandler
{
    /// <summary>
    /// Sends an HTTP request with an Azure DevOps access token injected into the Authorization header.
    /// </summary>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenService.GetAccessTokenAsync();
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
