using System.ComponentModel;
using System.Net.Http;
using Azure.Identity;
using DevOpsMcp.Server;
using DevOpsMcp.Server.Services;
using DevOpsMcp.Server.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

var serverUrl = "http://localhost:7071/";

var scopeName = "mcp.tools";

var settings = new Settings();
builder.Configuration.Bind(settings);
builder.Services.AddSingleton(settings);

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
        Resource = new Uri(serverUrl),
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

Console.WriteLine($"Protected Resource Metadata URL: {serverUrl}.well-known/oauth-protected-resource");
Console.WriteLine("Press Ctrl+C to stop the server");

app.Run(serverUrl);

