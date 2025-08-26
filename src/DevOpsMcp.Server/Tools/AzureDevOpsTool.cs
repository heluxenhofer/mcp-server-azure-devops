using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static ModelContextProtocol.Protocol.ElicitRequestParams;

namespace DevOpsMcp.Server.Tools;

/// <summary>
/// Provides tools for interacting with Azure DevOps, such as listing projects, repositories, branches, and creating branches.
/// </summary>
[McpServerToolType]
public class AzureDevOpsTool([FromKeyedServices("AzureDevOpsClient")] HttpClient httpClient)
{
    /// <summary>
    /// Gets all Azure DevOps projects by organization name.
    /// </summary>
    /// <param name="orgName">Organization name in Azure DevOps.</param>
    /// <returns>A JSON string containing the list of projects.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    [McpServerTool, Description("List all Azure DevOps projects by organization name")]
    public async Task<string> ListProjects([Description("Organization name in Azure DevOps")] string orgName)
    {

        try
        {
            var response = await httpClient.GetAsync($"{orgName}/_apis/projects");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return result;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Request failed: {ex.Message}");
            throw;
        }
    }
    /// <summary>
    /// Lists repositories for a specific project in an Azure DevOps organization.
    /// </summary>
    /// <param name="orgName">Organization name in Azure DevOps.</param>
    /// <param name="projectName">Project name for the given organization in Azure DevOps.</param>
    /// <returns>A JSON string containing the list of repositories.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    [McpServerTool, Description("List repositories for a specific project in an Azure Devops organization")]
    public async Task<string> ListRepositories(
        [Description("Organization name in Azure DevOps")] string orgName,
        [Description("Project name for given organization in Azure DevOps")] string projectName)
    {
        try
        {
            var response = await httpClient.GetAsync($"{orgName}/{projectName}/_apis/git/repositories");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return result;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Request failed: {ex.Message}");
            throw;
        }
    }
    /// <summary>
    /// Lists branches for a given project and repository in an Azure DevOps organization.
    /// </summary>
    /// <param name="orgName">Organization name in Azure DevOps.</param>
    /// <param name="projectName">Project name for the given organization in Azure DevOps.</param>
    /// <param name="repositoryName">Repository name for the given project in Azure DevOps.</param>
    /// <returns>An array of branch names.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    [McpServerTool, Description("List branches for a given project and repository in an Azure Devops organization")]
    public async Task<string[]> ListBranches(
        [Description("Organization name in Azure DevOps")] string orgName,
        [Description("Project name for given organization in Azure DevOps")] string projectName,
        [Description("Repository name for given project in Azure DevOps")] string repositoryName)
    {
        var branches = new List<string>();
        var encodedRepoName = Uri.EscapeDataString(repositoryName);
        var url = $"{orgName}/{projectName}/_apis/git/repositories/{encodedRepoName}/refs?filter=heads/";
        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in valueElement.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                    {
                        var fullName = nameElement.GetString();
                        if (fullName != null && fullName.StartsWith("refs/heads/"))
                        {
                            branches.Add(fullName.Substring("refs/heads/".Length));
                        }
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Request failed: {ex.Message}");
            throw;
        }

        return branches.ToArray();
    }
    /// <summary>
    /// Creates a new branch in a repository. If parent branch is not provided, user will be asked.
    /// </summary>
    /// <param name="server">The MCP server instance for user interaction.</param>
    /// <param name="orgName">Organization name in Azure DevOps.</param>
    /// <param name="projectName">Project name for the given organization in Azure DevOps.</param>
    /// <param name="repositoryName">Repository name for the given project in Azure DevOps.</param>
    /// <param name="newBranchName">Name of the new branch to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the branch was created successfully; otherwise, false.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required information is missing or invalid.</exception>
    [McpServerTool, Description("Create a new branch in a repository. If parent branch is not provided, user will be asked.")]
    public async Task<bool> CreateBranch(
        IMcpServer server,
        [Description("Organization name in Azure DevOps")] string orgName,
        [Description("Project name for given organization in Azure DevOps")] string projectName,
        [Description("Repository name for given project in Azure DevOps")] string repositoryName,
        [Description("Name of the new branch")] string newBranchName,
        CancellationToken cancellationToken)
    {
        var branches = await ListBranches(orgName, projectName, repositoryName);
        var enumSchema = new EnumSchema
        {
            Enum = branches
        };
        // Ask the user for parent branch
        var branchSchema = new RequestSchema
        {
            Properties =
            {
                ["ParentBranch"] = enumSchema
            }
        };

        var branchResponse = await server.ElicitAsync(new ElicitRequestParams
        {
            Message = $"From which branch do you want to create your new branch in repository {repositoryName}?",
            RequestedSchema = branchSchema

        }, cancellationToken);

        if (branchResponse.Content == null)
        {
            throw new InvalidOperationException("Branch response content is null.");
        }
        if (!branchResponse.Content.TryGetValue("ParentBranch", out JsonElement parentBranchElement) || parentBranchElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("ParentBranch is required and must be a string.");
        }
        var parentBranchName = parentBranchElement.GetString();

        try
        {
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var encodedParentBranch = Uri.EscapeDataString(parentBranchName!);
            // Step 1: Get the latest commit SHA of the parent branch
            var refUrl = $"{orgName}/{projectName}/_apis/git/repositories/{encodedRepoName}/refs/heads/{encodedParentBranch}";
            var refResponse = await httpClient.GetAsync(refUrl, cancellationToken);
            refResponse.EnsureSuccessStatusCode();
            var refContent = await refResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(refContent);
            var objectId = doc.RootElement.GetProperty("value")[0].GetProperty("objectId").GetString();
            if (string.IsNullOrEmpty(objectId))
                throw new InvalidOperationException($"Could not find objectId for branch {parentBranchName}");

            // Step 2: Create the new branch (correct endpoint and JSON)
            var createBranchUrl = $"{orgName}/{projectName}/_apis/git/repositories/{encodedRepoName}/refs?api-version=7.1";
            var bodyArray = new[]
            {
                new {
                    name = $"refs/heads/{newBranchName}",
                    oldObjectId = "0000000000000000000000000000000000000000",
                    newObjectId = objectId
                }
            };
            var bodyJson = JsonSerializer.Serialize(bodyArray);
            var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var createResponse = await httpClient.PostAsync(createBranchUrl, content, cancellationToken);
            createResponse.EnsureSuccessStatusCode();

            return true;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Request failed: {ex.Message}");
            throw;
        }

    }
}
