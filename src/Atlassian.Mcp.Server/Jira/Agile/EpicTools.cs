// <copyright file="EpicTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Agile;

/// <summary>
/// Tools for Jira Software epics.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraAgile)]
public sealed class EpicTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpicTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public EpicTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the epics of the specified board.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="done">Whether to return only done or only not-done epics.</param>
    /// <param name="startAt">The index of the first epic to return.</param>
    /// <param name="maxResults">The largest number of epics to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The epics.</returns>
    [McpServerTool(Name = "atlassian_jira_list_epics", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the epics of the specified Jira Software board.")]
    public async Task<string> GetAll(
        [Description("The board ID.")] int boardId,
        [Description("true for only done epics, false for only epics that are not done. Defaults to both.")] bool? done = null,
        [Description("The index of the first epic to return, starting at 0.")] int? startAt = null,
        [Description("The largest number of epics to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"board/{boardId}/epic")
            .Add("done", done)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAgileAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the specified epic.
    /// </summary>
    /// <param name="epicIdOrKey">The epic ID or key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The epic.</returns>
    [McpServerTool(Name = "atlassian_jira_get_epic", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified Jira Software epic: key, name, summary, and whether it is done.")]
    public async Task<string> Get(
        [Description("The epic key, such as PROJ-10, or its ID.")] string epicIdOrKey,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync($"epic/{JiraClient.Segment(epicIdOrKey)}", cancellationToken));

    /// <summary>
    /// Gets the issues in the specified epic.
    /// </summary>
    /// <param name="epicIdOrKey">The epic ID or key.</param>
    /// <param name="jql">Extra JQL to filter the issues.</param>
    /// <param name="fields">The fields to return.</param>
    /// <param name="startAt">The index of the first issue to return.</param>
    /// <param name="maxResults">The largest number of issues to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issues.</returns>
    [McpServerTool(Name = "atlassian_jira_get_epic_issues", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the issues in the specified Jira Software epic.")]
    public async Task<string> GetIssues(
        [Description("The epic key, such as PROJ-10, or its ID.")] string epicIdOrKey,
        [Description("Extra JQL that the issues must also match.")] string? jql = null,
        [Description("A comma-separated list of the fields to return. Defaults to all navigable fields.")] string? fields = null,
        [Description("The index of the first issue to return, starting at 0.")] int? startAt = null,
        [Description("The largest number of issues to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync(
            BoardTools.IssuePath($"epic/{JiraClient.Segment(epicIdOrKey)}/issue", jql, fields, startAt, maxResults),
            cancellationToken));

    /// <summary>
    /// Moves the specified issues into the specified epic.
    /// </summary>
    /// <param name="epicIdOrKey">The epic ID or key.</param>
    /// <param name="issueKeys">The issue keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_move_issues_to_epic", Idempotent = true, OpenWorld = true)]
    [Description("Moves the specified Jira issues into the specified epic, replacing any epic they were in.")]
    public async Task<string> MoveIssues(
        [Description("The epic key, such as PROJ-10, or its ID.")] string epicIdOrKey,
        [Description("A comma-separated list of issue keys, such as \"PROJ-1,PROJ-2\". At most 50.")] string issueKeys,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = BoardTools.RequireKeys(issueKeys);
        await this.jira.SendAgileAsync(HttpMethod.Post, $"epic/{JiraClient.Segment(epicIdOrKey)}/issue", new { issues = keys }, cancellationToken);
        return ToolResult.Success($"Moved {string.Join(", ", keys)} to epic {epicIdOrKey.Trim()}.");
    }

    /// <summary>
    /// Removes the specified issues from their epics.
    /// </summary>
    /// <param name="issueKeys">The issue keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_remove_issues_from_epic", Idempotent = true, OpenWorld = true)]
    [Description("Removes the specified Jira issues from whatever epic they are in.")]
    public async Task<string> RemoveIssues(
        [Description("A comma-separated list of issue keys, such as \"PROJ-1,PROJ-2\". At most 50.")] string issueKeys,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = BoardTools.RequireKeys(issueKeys);
        await this.jira.SendAgileAsync(HttpMethod.Post, "epic/none/issue", new { issues = keys }, cancellationToken);
        return ToolResult.Success($"Removed {string.Join(", ", keys)} from their epics.");
    }
}
