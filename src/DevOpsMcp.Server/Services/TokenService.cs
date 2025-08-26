using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Client;
using System.Security.Claims;

namespace DevOpsMcp.Server.Services;

/// <summary>
/// Provides methods to acquire Azure DevOps access tokens using On-Behalf-Of flow for authenticated users.
/// </summary>
public class TokenService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Settings _settings;
    private readonly IConfidentialClientApplication _confidentialClient;
    private const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenService"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="settings">The application settings.</param>
    public TokenService(IHttpContextAccessor httpContextAccessor, Settings settings)
    {
        _httpContextAccessor = httpContextAccessor;
        _settings = settings;
        _confidentialClient = ConfidentialClientApplicationBuilder.Create(settings.AzureAd.ClientId)
            .WithClientSecret(settings.AzureAd.ClientSecret)
            .WithAuthority(settings.AzureAd.Authority)
            .Build();
    }

    /// <summary>
    /// Acquires an Azure DevOps access token using the On-Behalf-Of flow for the current authenticated user.
    /// </summary>
    /// <returns>The Azure DevOps access token.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the HTTP context or access token is not available.</exception>
    public async Task<string> GetAccessTokenAsync()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            throw new InvalidOperationException("HttpContext is not available.");
        }
        var userAccessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(userAccessToken))
        {
            throw new InvalidOperationException("Access token is not available.");
        }
        var user = context.User;
        var assertion = userAccessToken;
        var oboResult = await _confidentialClient.AcquireTokenOnBehalfOf(new[] { AzureDevOpsScope }, new UserAssertion(assertion)).ExecuteAsync();
        return oboResult.AccessToken;
    }
}
