using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Client;
using System.Security.Claims;

namespace DevOpsMcp.Server.Services;

/// <summary>
/// Provides methods to acquire Azure DevOps access tokens using On-Behalf-Of flow for authenticated users.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TokenService"/> class.
/// </remarks>
/// <param name="httpContextAccessor">The HTTP context accessor.</param>
/// <param name="settings">The application settings.</param>
public class TokenService(IHttpContextAccessor httpContextAccessor, Settings settings)
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly Settings _settings = settings;
    private readonly IConfidentialClientApplication _confidentialClient = ConfidentialClientApplicationBuilder.Create(settings.AzureAd.ClientId)
            .WithClientSecret(settings.AzureAd.ClientSecret)
            .WithAuthority(settings.AzureAd.Authority)
            .Build();
    private const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

    /// <summary>
    /// Acquires an Azure DevOps access token using the On-Behalf-Of flow for the current authenticated user.
    /// </summary>
    /// <returns>The Azure DevOps access token.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the HTTP context or access token is not available.</exception>
    public async Task<string> GetAccessTokenAsync()
    {
        var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is not available.");
        var userAccessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(userAccessToken))
        {
            throw new InvalidOperationException("Access token is not available.");
        }
        var assertion = userAccessToken;
        var oboResult = await _confidentialClient.AcquireTokenOnBehalfOf([AzureDevOpsScope], new UserAssertion(assertion)).ExecuteAsync();
        return oboResult.AccessToken;
    }
}
