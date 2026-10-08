// <copyright file="UserTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Users;

/// <summary>
/// Tools for Jira users and their permissions.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraUsers)]
public sealed class UserTools
{
    /// <summary>
    /// The permissions checked by <see cref="GetMyPermissions"/> when none are given. Jira Cloud
    /// requires the caller to name the permissions to check.
    /// </summary>
    internal const string DefaultPermissions = "BROWSE_PROJECTS,CREATE_ISSUES,EDIT_ISSUES,TRANSITION_ISSUES,ASSIGN_ISSUES,ADD_COMMENTS,DELETE_ISSUES";

    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public UserTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the user whose API token the server uses.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user.</returns>
    [McpServerTool(Name = "atlassian_jira_get_current_user", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the Jira user that the server signs in as: account ID, display name, email address, and time zone.")]
    public async Task<string> GetCurrent(CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync("myself", cancellationToken));

    /// <summary>
    /// Gets the specified user.
    /// </summary>
    /// <param name="accountId">The account ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user.</returns>
    [McpServerTool(Name = "atlassian_jira_get_user", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the Jira user with the specified account ID.")]
    public async Task<string> Get(
        [Description("The account ID of the user.")] string accountId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync(new QueryString("user").Add("accountId", accountId.Trim()).ToString(), cancellationToken));

    /// <summary>
    /// Gets several users at once.
    /// </summary>
    /// <param name="accountIds">A comma-separated list of account IDs.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of users.</returns>
    [McpServerTool(Name = "atlassian_jira_bulk_get_users", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the Jira users with the specified account IDs, in one request.")]
    public async Task<string> GetMany(
        [Description("Comma-separated account IDs.")] string accountIds,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        List<string> ids = RequireList(accountIds, nameof(accountIds));
        string path = new QueryString("user/bulk")
            .AddEach("accountId", ids)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Searches for users by name or email address.
    /// </summary>
    /// <param name="query">The text to match.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching users.</returns>
    [McpServerTool(Name = "atlassian_jira_search_users", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Searches for Jira users whose display name or email address matches the specified text. Use it to find an account ID.")]
    public async Task<string> Search(
        [Description("The text to match against display names and email addresses.")] string query,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("user/search")
            .Add("query", query)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Finds the users that can be assigned to the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="query">The text to match.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable users.</returns>
    [McpServerTool(Name = "atlassian_jira_find_users_assignable_to_issue", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Finds the Jira users that can be assigned to the specified issue, optionally filtered by name.")]
    public async Task<string> FindAssignableToIssue(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Optional text to match against display names and email addresses.")] string? query = null,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("user/assignable/search")
            .Add("issueKey", issueKey.Trim())
            .Add("query", query)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Finds the users that can be assigned issues in all of the specified projects.
    /// </summary>
    /// <param name="projectKeys">A comma-separated list of project keys.</param>
    /// <param name="query">The text to match.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable users.</returns>
    [McpServerTool(Name = "atlassian_jira_find_users_assignable_to_projects", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Finds the Jira users that can be assigned issues in all of the specified projects, optionally filtered by name.")]
    public async Task<string> FindAssignableToProjects(
        [Description("Comma-separated project keys, such as PROJ,OPS.")] string projectKeys,
        [Description("Optional text to match against display names and email addresses.")] string? query = null,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = RequireList(projectKeys, nameof(projectKeys));
        string path = new QueryString("user/assignable/multiProjectSearch")
            .Add("projectKeys", string.Join(',', keys))
            .Add("query", query)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Finds the users that hold all of the specified permissions.
    /// </summary>
    /// <param name="permissions">A comma-separated list of permission keys.</param>
    /// <param name="projectKey">The project to check the permissions in.</param>
    /// <param name="issueKey">The issue to check the permissions on.</param>
    /// <param name="query">The text to match.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The users.</returns>
    [McpServerTool(Name = "atlassian_jira_find_users_with_permissions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Finds the Jira users that hold all of the specified permissions, in a project, on an issue, or globally.")]
    public async Task<string> FindWithPermissions(
        [Description("Comma-separated permission keys, such as BROWSE_PROJECTS,EDIT_ISSUES.")] string permissions,
        [Description("Optional project key to check the permissions in.")] string? projectKey = null,
        [Description("Optional issue key to check the permissions on.")] string? issueKey = null,
        [Description("Optional text to match against display names and email addresses.")] string? query = null,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = RequireList(permissions, nameof(permissions));
        string path = new QueryString("user/permission/search")
            .Add("permissions", string.Join(',', keys))
            .Add("projectKey", projectKey?.Trim())
            .Add("issueKey", issueKey?.Trim())
            .Add("query", query)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets which of the specified permissions the current user holds.
    /// </summary>
    /// <param name="permissions">A comma-separated list of permission keys.</param>
    /// <param name="projectKey">The project to check the permissions in.</param>
    /// <param name="issueKey">The issue to check the permissions on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The permissions, each with a value indicating whether it is held.</returns>
    [McpServerTool(Name = "atlassian_jira_get_my_permissions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets which of the specified permissions the current Jira user holds, in a project, on an issue, or globally.")]
    public async Task<string> GetMyPermissions(
        [Description("Optional comma-separated permission keys. Defaults to BROWSE_PROJECTS, CREATE_ISSUES, EDIT_ISSUES, TRANSITION_ISSUES, ASSIGN_ISSUES, ADD_COMMENTS, and DELETE_ISSUES.")] string? permissions = null,
        [Description("Optional project key to check the permissions in.")] string? projectKey = null,
        [Description("Optional issue key to check the permissions on.")] string? issueKey = null,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = JsonArguments.SplitList(string.IsNullOrWhiteSpace(permissions) ? DefaultPermissions : permissions);
        string path = new QueryString("mypermissions")
            .Add("permissions", string.Join(',', keys))
            .Add("projectKey", projectKey?.Trim())
            .Add("issueKey", issueKey?.Trim())
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    private static List<string> RequireList(string value, string parameterName)
    {
        List<string> items = JsonArguments.SplitList(value);
        return items.Count > 0 ? items : throw new McpException($"The {parameterName} parameter must list at least one value.");
    }
}
