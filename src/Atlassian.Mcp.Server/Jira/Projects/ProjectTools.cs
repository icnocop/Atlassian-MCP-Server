// <copyright file="ProjectTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Projects;

/// <summary>
/// Tools for Jira projects and their roles.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraProjects)]
public sealed class ProjectTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public ProjectTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Lists the projects that the user can see.
    /// </summary>
    /// <param name="keys">An optional comma-separated list of project keys to limit the results to.</param>
    /// <param name="expand">An optional comma-separated list of properties to expand.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of projects.</returns>
    [McpServerTool(Name = "atlassian_jira_list_projects", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists the Jira projects that the user can see, one page at a time.")]
    public async Task<string> List(
        [Description("Optional comma-separated project keys to limit the results to, such as PROJ,OPS.")] string? keys = null,
        [Description("Optional comma-separated properties to expand: description, lead, issueTypes, url, projectKeys, insight.")] string? expand = null,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("project/search")
            .AddEach("keys", JsonArguments.SplitList(keys))
            .Add("expand", expand)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="expand">An optional comma-separated list of properties to expand.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The project.</returns>
    [McpServerTool(Name = "atlassian_jira_get_project", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the details of the specified Jira project: key, name, lead, issue types, components, and versions.")]
    public async Task<string> Get(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        [Description("Optional comma-separated properties to expand: description, lead, issueTypes, url, projectKeys, insight.")] string? expand = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"project/{JiraClient.Segment(projectKey)}").Add("expand", expand).ToString();
        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the issue types that the specified project uses.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issue types.</returns>
    [McpServerTool(Name = "atlassian_jira_get_project_issue_types", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the issue types that can be created in the specified Jira project.")]
    public async Task<string> GetIssueTypes(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        CancellationToken cancellationToken = default)
    {
        JsonNode? project = await this.jira.GetAsync($"project/{JiraClient.Segment(projectKey)}", cancellationToken);
        return ToolResult.Json(project?["issueTypes"] ?? new JsonArray());
    }

    /// <summary>
    /// Gets the roles of the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roles, with their IDs.</returns>
    [McpServerTool(Name = "atlassian_jira_get_project_roles", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the roles of the specified Jira project, with the role IDs to pass to atlassian_jira_get_project_role.")]
    public async Task<string> GetRoles(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        CancellationToken cancellationToken = default)
    {
        JsonNode? roles = await this.jira.GetAsync($"project/{JiraClient.Segment(projectKey)}/role", cancellationToken);

        // The API answers with a map of role names to role URLs; the role ID is the last segment.
        var result = new JsonArray();
        foreach ((string name, JsonNode? url) in roles as JsonObject ?? [])
        {
            string? text = url?.GetValue<string>();
            string? id = text?[(text.LastIndexOf('/') + 1)..];
            result.Add(new JsonObject { ["name"] = name, ["id"] = id });
        }

        return ToolResult.Json(result);
    }

    /// <summary>
    /// Gets the users and groups in a role of the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The role and its actors.</returns>
    [McpServerTool(Name = "atlassian_jira_get_project_role", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the users and groups that hold the specified role in the specified Jira project.")]
    public async Task<string> GetRole(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        [Description("The role ID, from atlassian_jira_get_project_roles.")] string roleId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync(
            $"project/{JiraClient.Segment(projectKey)}/role/{JiraClient.Segment(roleId)}",
            cancellationToken));

    /// <summary>
    /// Creates a project.
    /// </summary>
    /// <param name="key">The project key.</param>
    /// <param name="name">The project name.</param>
    /// <param name="projectTypeKey">The project type.</param>
    /// <param name="description">The description.</param>
    /// <param name="leadAccountId">The account ID of the project lead.</param>
    /// <param name="categoryId">The project category ID.</param>
    /// <param name="assigneeType">The default assignee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new project.</returns>
    [McpServerTool(Name = "atlassian_jira_create_project", OpenWorld = true)]
    [Toolset(Toolsets.JiraAdmin)]
    [Description("Creates a Jira project. Requires the Administer Jira global permission.")]
    public async Task<string> Create(
        [Description("The project key: uppercase letters and digits, starting with a letter, such as PROJ.")] string key,
        [Description("The project name.")] string name,
        [Description("The project type: software, business, or service_desk.")] string projectTypeKey,
        [Description("Optional project description.")] string? description = null,
        [Description("Optional account ID of the project lead. Defaults to the current user.")] string? leadAccountId = null,
        [Description("Optional project category ID.")] string? categoryId = null,
        [Description("Optional default assignee: PROJECT_LEAD or UNASSIGNED.")] string? assigneeType = null,
        CancellationToken cancellationToken = default)
    {
        // Jira Cloud requires a lead; the current user is the natural default.
        if (string.IsNullOrWhiteSpace(leadAccountId))
        {
            JsonNode? myself = await this.jira.GetAsync("myself", cancellationToken);
            leadAccountId = myself?["accountId"]?.GetValue<string>();
        }

        var body = new
        {
            key = key.Trim(),
            name,
            projectTypeKey,
            description,
            leadAccountId,
            categoryId = ParseOptionalId(categoryId, nameof(categoryId)),
            assigneeType,
        };

        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, "project", body, cancellationToken));
    }

    /// <summary>
    /// Updates the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="name">The new name.</param>
    /// <param name="description">The new description.</param>
    /// <param name="leadAccountId">The account ID of the new project lead.</param>
    /// <param name="categoryId">The new project category ID.</param>
    /// <param name="assigneeType">The new default assignee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated project.</returns>
    [McpServerTool(Name = "atlassian_jira_update_project", Idempotent = true, OpenWorld = true)]
    [Toolset(Toolsets.JiraAdmin)]
    [Description("Updates the specified Jira project. Only the values that are passed are changed.")]
    public async Task<string> Update(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        [Description("Optional new project name.")] string? name = null,
        [Description("Optional new project description.")] string? description = null,
        [Description("Optional account ID of the new project lead.")] string? leadAccountId = null,
        [Description("Optional new project category ID.")] string? categoryId = null,
        [Description("Optional new default assignee: PROJECT_LEAD or UNASSIGNED.")] string? assigneeType = null,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            name,
            description,
            leadAccountId,
            categoryId = ParseOptionalId(categoryId, nameof(categoryId)),
            assigneeType,
        };

        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Put, $"project/{JiraClient.Segment(projectKey)}", body, cancellationToken));
    }

    /// <summary>
    /// Deletes the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_project", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Toolset(Toolsets.JiraAdmin)]
    [Description("Moves the specified Jira project to the trash, where an administrator can restore it for 60 days.")]
    public async Task<string> Delete(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        CancellationToken cancellationToken = default)
    {
        await this.jira.SendAsync(HttpMethod.Delete, $"project/{JiraClient.Segment(projectKey)}?enableUndo=true", body: null, cancellationToken);
        return ToolResult.Success($"Moved project {projectKey} to the trash.");
    }

    /// <summary>
    /// Parses an optional numeric ID argument.
    /// </summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <param name="parameterName">The parameter name, for the error message.</param>
    /// <returns>The ID, or <see langword="null"/>.</returns>
    /// <exception cref="McpException">The value is not a number.</exception>
    internal static long? ParseOptionalId(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)
            ? id
            : throw new McpException($"The {parameterName} parameter must be a numeric ID.");
    }
}
