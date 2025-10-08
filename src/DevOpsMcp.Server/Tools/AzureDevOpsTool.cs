using DevOpsMcp.Server.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using static ModelContextProtocol.Protocol.ElicitRequestParams;

namespace DevOpsMcp.Server.Tools;

/// <summary>
/// Provides tools for interacting with Azure DevOps, such as listing projects, repositories, branches, and creating branches.
/// </summary>
[McpServerToolType]
public class AzureDevOpsTool([FromKeyedServices("AzureDevOpsClient")] HttpClient httpClient, ILogger<AzureDevOpsTool> logger)
{
    /// <summary>
    /// Gets all Azure DevOps projects by organization name.
    /// </summary>
    /// <param name="orgName">Organization name in Azure DevOps.</param>
    /// <returns>A JSON string containing the list of projects.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    [McpServerTool, Description("List all Azure DevOps projects by organization name")]
    public async Task<ToolResponse<string>> ListProjects([Description("Organization name in Azure DevOps")] string orgName)
    {
        try
        {
            var response = await httpClient.GetAsync($"{orgName}/_apis/projects");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return ToolResponse<string>.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list projects for organization {OrgName}", orgName);
            return ToolResponse<string>.Fail(ex.Message);
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
    public async Task<ToolResponse<string>> ListRepositories(
        [Description("Organization name in Azure DevOps")] string orgName,
        [Description("Project name for given organization in Azure DevOps")] string projectName)
    {
        try
        {
            var response = await httpClient.GetAsync($"{orgName}/{projectName}/_apis/git/repositories");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return ToolResponse<string>.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list repositories for project {ProjectName} in organization {OrgName}", projectName, orgName);
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
    public async Task<ToolResponse<string>> ListBranches(
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
                            branches.Add(fullName["refs/heads/".Length..]);
                        }
                    }
                }
            }
            var json = JsonSerializer.Serialize(branches);
            return ToolResponse<string>.Ok(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list branches for repository {RepositoryName} in project {ProjectName} of organization {OrgName}", repositoryName, projectName, orgName);
            return ToolResponse<string>.Fail(ex.Message);
        }
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
    [McpServerTool, Description("Create a new branch in a repository.")]
    public async Task<ToolResponse<string>> CreateBranch(
        IMcpServer server,
        [Description("Organization name in Azure DevOps")] string orgName,
        [Description("Project name for given organization in Azure DevOps")] string projectName,
        [Description("Repository name for given project in Azure DevOps")] string repositoryName,
        [Description("Name of the new branch")] string newBranchName,
        [Description("Name of the parent branch from which new branch will be created.")] string parentBranchName = "",
        CancellationToken cancellationToken = default)
    {
        try
        {

            string branchToUse = parentBranchName;
            if (string.IsNullOrEmpty(branchToUse))
            {
                if (server == null)
                    throw new InvalidOperationException("Elicitation required but server is null.");

                var branchesResponse = await ListBranches(orgName, projectName, repositoryName);
                if(!branchesResponse.Success || string.IsNullOrEmpty(branchesResponse.Data))
                    throw new InvalidOperationException("Could not retrieve branches for elicitation: " + branchesResponse.Error);
                var branches = JsonSerializer.Deserialize<string[]>(branchesResponse.Data);
                var enumSchema = new EnumSchema { Enum = branches! };
                var branchSchema = new RequestSchema
                {
                    Properties = { ["ParentBranch"] = enumSchema },
                    Required = ["ParentBranch"]
                };

                var branchResponse = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = $"From which branch do you want to create your new branch in repository {repositoryName}?",
                    RequestedSchema = branchSchema
                }, cancellationToken);

                if (branchResponse.Content == null ||
                    !branchResponse.Content.TryGetValue("ParentBranch", out JsonElement parentBranchElement) ||
                    parentBranchElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException("ParentBranch is required and must be a string.");
                }

                branchToUse = parentBranchElement.GetString();
            }
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var encodedParentBranch = Uri.EscapeDataString(parentBranchName);
            var refUrl = $"{orgName}/{projectName}/_apis/git/repositories/{encodedRepoName}/refs/heads/{encodedParentBranch}";
            var refResponse = await httpClient.GetAsync(refUrl, cancellationToken);
            refResponse.EnsureSuccessStatusCode();
            var refContent = await refResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(refContent);
            var objectId = doc.RootElement.GetProperty("value")[0].GetProperty("objectId").GetString();
            if (string.IsNullOrEmpty(objectId))
                throw new InvalidOperationException($"Could not find objectId for branch {parentBranchName}");

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

            var createdResponse = await httpClient.PostAsync(createBranchUrl, content, cancellationToken);
            createdResponse.EnsureSuccessStatusCode();
            return ToolResponse<string>.Ok($"Branch {newBranchName} created successfully from {parentBranchName}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create branch {NewBranchName} in repository {RepositoryName} of project {ProjectName} in organization {OrgName}", newBranchName, repositoryName, projectName, orgName);
            return ToolResponse<string>.Fail(ex.Message);
        }

    }
}
