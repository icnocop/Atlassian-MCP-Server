// <copyright file="WatcherTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Issues;

/// <summary>
/// Tools for the watchers and votes of Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraIssues)]
public sealed class WatcherTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatcherTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public WatcherTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the watchers of the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The watchers.</returns>
    [McpServerTool(Name = "atlassian_jira_get_watchers", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the users watching the specified Jira issue.")]
    public async Task<string> GetAll(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync($"issue/{JiraClient.Segment(issueKey)}/watchers", cancellationToken));

    /// <summary>
    /// Adds a watcher to the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="accountId">The account ID of the watcher.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_watch_issue", Idempotent = true, OpenWorld = true)]
    [Description("Adds a watcher to the specified Jira issue: you, or another user.")]
    public async Task<string> Add(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Optional account ID of the user to add. Defaults to you.")] string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        string watcher = await this.AccountIdOrMeAsync(accountId, cancellationToken);

        // The body is the account ID as a bare JSON string.
        await this.jira.SendAsync(HttpMethod.Post, $"issue/{JiraClient.Segment(issueKey)}/watchers", JsonValue.Create(watcher), cancellationToken);
        return ToolResult.Success($"Added watcher {watcher} to {issueKey}.");
    }

    /// <summary>
    /// Removes a watcher from the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="accountId">The account ID of the watcher.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_unwatch_issue", Idempotent = true, OpenWorld = true)]
    [Description("Removes a watcher from the specified Jira issue: you, or another user.")]
    public async Task<string> Remove(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Optional account ID of the user to remove. Defaults to you.")] string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        string watcher = await this.AccountIdOrMeAsync(accountId, cancellationToken);
        string path = new QueryString($"issue/{JiraClient.Segment(issueKey)}/watchers").Add("accountId", watcher).ToString();
        await this.jira.SendAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        return ToolResult.Success($"Removed watcher {watcher} from {issueKey}.");
    }

    /// <summary>
    /// Votes for the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_vote_issue", Idempotent = true, OpenWorld = true)]
    [Description("Adds your vote to the specified Jira issue.")]
    public async Task<string> Vote(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        CancellationToken cancellationToken = default)
    {
        await this.jira.SendAsync(HttpMethod.Post, $"issue/{JiraClient.Segment(issueKey)}/votes", body: null, cancellationToken);
        return ToolResult.Success($"Voted for {issueKey}.");
    }

    /// <summary>
    /// Removes the vote for the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_unvote_issue", Idempotent = true, OpenWorld = true)]
    [Description("Removes your vote from the specified Jira issue.")]
    public async Task<string> Unvote(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        CancellationToken cancellationToken = default)
    {
        await this.jira.SendAsync(HttpMethod.Delete, $"issue/{JiraClient.Segment(issueKey)}/votes", body: null, cancellationToken);
        return ToolResult.Success($"Removed your vote from {issueKey}.");
    }

    private async Task<string> AccountIdOrMeAsync(string? accountId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            return accountId.Trim();
        }

        JsonNode? me = await this.jira.GetAsync("myself", cancellationToken);
        return me?["accountId"]?.GetValue<string>() ?? throw new InvalidOperationException("Jira did not return the account ID of the current user.");
    }
}
