using DevOpsMcp.Server;
using DevOpsMcp.Server.Services;
using DevOpsMcp.Server.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Determine the port and base URLs for the server and resource metadata.
//
// This section allows the application to run both locally (for development) and in a containerized/cloud environment.
// - By default, it uses localhost and a default port (7071) for local development.
// - If the MCP_RESOURCE_FQDN environment variable is set (e.g., in Azure Container Apps),
//   it configures the resource URL to use the public FQDN and binds the server to all interfaces (0.0.0.0) for container networking.
// This ensures the app is accessible both locally and when deployed to the cloud, and that resource metadata URLs are correct for OAuth flows.
var port = Environment.GetEnvironmentVariable("PORT") ?? "7071";
var serverUrl = $"http://localhost:{port}/";
var resourceUrl = serverUrl;
var mcpFqdn = Environment.GetEnvironmentVariable("MCP_RESOURCE_FQDN");

if (!string.IsNullOrEmpty(mcpFqdn))
{
    resourceUrl = $"https://{mcpFqdn}/";
    serverUrl = $"http://0.0.0.0:{port}/";
}

var scopeName = "mcp.tools";

var settings = new Settings();
builder.Configuration.Bind(settings);
builder.Services.AddSingleton(settings);

if (settings.AzureAd == null ||
string.IsNullOrEmpty(settings.AzureAd.ClientId) ||
string.IsNullOrEmpty(settings.AzureAd.TenantId) ||
string.IsNullOrEmpty(settings.AzureAd.Authority))
{
    throw new InvalidOperationException($"AzureAd settings are not configured. Please check your configuration.");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = settings.AzureAd.Authority;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudiences = [$"api://{settings.AzureAd.ClientId}"],
        ValidIssuers = [$"https://sts.windows.net/{settings.AzureAd.TenantId}/"],
        NameClaimType = "name",
        RoleClaimType = "roles",
    };
}).AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        Resource = new Uri(resourceUrl),
        AuthorizationServers = { new Uri(settings.AzureAd.Authority) },
        ScopesSupported = [$"api://{settings.AzureAd.ClientId}/{scopeName}"],
    };
});
builder.Services.AddSingleton<TokenService>();
builder.Services.AddTransient<DevOpsTokenHandler>();
builder.Services.AddHttpClient("AzureDevOpsClient", client =>
{
    client.BaseAddress = new Uri("https://dev.azure.com/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
})
    .AddHttpMessageHandler<DevOpsTokenHandler>()
    .AddAsKeyed();

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpServer()
    .WithTools<AzureDevOpsTool>()
    .WithHttpTransport();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Use the default MCP policy name that we've configured
// <see link href="https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/ProtectedMcpServer/Program.cs">
app.MapMcp().RequireAuthorization();

Console.WriteLine($"Starting MCP server with authorization at {serverUrl}");
Console.WriteLine($"Protected Resource Metadata URL: {resourceUrl}.well-known/oauth-protected-resource");
Console.WriteLine("Press Ctrl+C to stop the server");

app.Run(serverUrl);

