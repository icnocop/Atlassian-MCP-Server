// <copyright file="GroupTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Users;

/// <summary>
/// Tools for Jira groups and their members.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraUsers)]
public sealed class GroupTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public GroupTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Searches for groups by name.
    /// </summary>
    /// <param name="query">The text that group names must contain.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching groups.</returns>
    [McpServerTool(Name = "atlassian_jira_search_groups", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Searches for Jira groups whose names contain the specified text, or lists groups when no text is given.")]
    public async Task<string> Search(
        [Description("Optional text that group names must contain.")] string? query = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("groups/picker").Add("query", query).Add("maxResults", maxResults).ToString();
        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the members of the specified group.
    /// </summary>
    /// <param name="groupName">The group name.</param>
    /// <param name="includeInactiveUsers">A value indicating whether inactive users are included.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of members.</returns>
    [McpServerTool(Name = "atlassian_jira_get_group_members", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the members of the specified Jira group, one page at a time.")]
    public async Task<string> GetMembers(
        [Description("The group name.")] string groupName,
        [Description("Optional flag that includes inactive users.")] bool? includeInactiveUsers = null,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("group/member")
            .Add("groupname", groupName)
            .Add("includeInactiveUsers", includeInactiveUsers)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Adds a user to the specified group.
    /// </summary>
    /// <param name="groupName">The group name.</param>
    /// <param name="accountId">The account ID of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The group.</returns>
    [McpServerTool(Name = "atlassian_jira_add_user_to_group", Idempotent = true, OpenWorld = true)]
    [Toolset(Toolsets.JiraAdmin)]
    [Description("Adds the specified user to the specified Jira group. Requires site administration permission.")]
    public async Task<string> AddMember(
        [Description("The group name.")] string groupName,
        [Description("The account ID of the user to add.")] string accountId,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("group/user").Add("groupname", groupName).ToString();
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, path, new { accountId = accountId.Trim() }, cancellationToken));
    }

    /// <summary>
    /// Removes a user from the specified group.
    /// </summary>
    /// <param name="groupName">The group name.</param>
    /// <param name="accountId">The account ID of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_remove_user_from_group", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Toolset(Toolsets.JiraAdmin)]
    [Description("Removes the specified user from the specified Jira group. Requires site administration permission.")]
    public async Task<string> RemoveMember(
        [Description("The group name.")] string groupName,
        [Description("The account ID of the user to remove.")] string accountId,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("group/user").Add("groupname", groupName).Add("accountId", accountId.Trim()).ToString();
        await this.jira.SendAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        return ToolResult.Success($"Removed user {accountId} from group {groupName}.");
    }
}
