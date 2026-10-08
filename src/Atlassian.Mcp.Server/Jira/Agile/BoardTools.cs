// <copyright file="BoardTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Agile;

/// <summary>
/// Tools for Jira Software boards and their backlogs.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraAgile)]
public sealed class BoardTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public BoardTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the boards that the user can see.
    /// </summary>
    /// <param name="name">Text that the board name must contain.</param>
    /// <param name="projectKeyOrId">The project key or ID that the boards must belong to.</param>
    /// <param name="type">The board type.</param>
    /// <param name="startAt">The index of the first board to return.</param>
    /// <param name="maxResults">The largest number of boards to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The boards.</returns>
    [McpServerTool(Name = "atlassian_jira_list_boards", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the Jira Software boards that the user can see, optionally filtered by name, project, or type.")]
    public async Task<string> GetAll(
        [Description("Text that the board name must contain.")] string? name = null,
        [Description("The key or ID of the project that the boards must belong to.")] string? projectKeyOrId = null,
        [Description("The board type: scrum or kanban.")] string? type = null,
        [Description("The index of the first board to return, starting at 0.")] int? startAt = null,
        [Description("The largest number of boards to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("board")
            .Add("name", name)
            .Add("projectKeyOrId", projectKeyOrId)
            .Add("type", type)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAgileAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the specified board.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The board.</returns>
    [McpServerTool(Name = "atlassian_jira_get_board", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified Jira Software board: name, type, and location (project).")]
    public async Task<string> Get(
        [Description("The board ID.")] int boardId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync($"board/{boardId}", cancellationToken));

    /// <summary>
    /// Gets the configuration of the specified board.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The configuration.</returns>
    [McpServerTool(Name = "atlassian_jira_get_board_configuration", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the configuration of the specified Jira Software board: its saved filter, columns and their statuses, estimation field, and ranking field.")]
    public async Task<string> GetConfiguration(
        [Description("The board ID.")] int boardId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync($"board/{boardId}/configuration", cancellationToken));

    /// <summary>
    /// Gets the issues on the specified board.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="jql">Extra JQL to filter the issues.</param>
    /// <param name="fields">The fields to return.</param>
    /// <param name="startAt">The index of the first issue to return.</param>
    /// <param name="maxResults">The largest number of issues to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issues.</returns>
    [McpServerTool(Name = "atlassian_jira_get_board_issues", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the issues on the specified Jira Software board, including the backlog and sprints.")]
    public async Task<string> GetIssues(
        [Description("The board ID.")] int boardId,
        [Description("Extra JQL that the issues must also match, such as \"status = Done\".")] string? jql = null,
        [Description("A comma-separated list of the fields to return, such as \"summary,status,assignee\". Defaults to all navigable fields.")] string? fields = null,
        [Description("The index of the first issue to return, starting at 0.")] int? startAt = null,
        [Description("The largest number of issues to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync(IssuePath($"board/{boardId}/issue", jql, fields, startAt, maxResults), cancellationToken));

    /// <summary>
    /// Gets the backlog issues of the specified board.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="jql">Extra JQL to filter the issues.</param>
    /// <param name="fields">The fields to return.</param>
    /// <param name="startAt">The index of the first issue to return.</param>
    /// <param name="maxResults">The largest number of issues to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issues.</returns>
    [McpServerTool(Name = "atlassian_jira_get_backlog", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the issues in the backlog of the specified Jira Software board: the issues that are not in an active or future sprint.")]
    public async Task<string> GetBacklog(
        [Description("The board ID.")] int boardId,
        [Description("Extra JQL that the issues must also match.")] string? jql = null,
        [Description("A comma-separated list of the fields to return. Defaults to all navigable fields.")] string? fields = null,
        [Description("The index of the first issue to return, starting at 0.")] int? startAt = null,
        [Description("The largest number of issues to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync(IssuePath($"board/{boardId}/backlog", jql, fields, startAt, maxResults), cancellationToken));

    /// <summary>
    /// Moves the specified issues to the backlog.
    /// </summary>
    /// <param name="issueKeys">The issue keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_move_issues_to_backlog", Idempotent = true, OpenWorld = true)]
    [Description("Moves the specified Jira issues out of their sprints and into the backlog.")]
    public async Task<string> MoveToBacklog(
        [Description("A comma-separated list of issue keys, such as \"PROJ-1,PROJ-2\". At most 50.")] string issueKeys,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = RequireKeys(issueKeys);
        await this.jira.SendAgileAsync(HttpMethod.Post, "backlog/issue", new { issues = keys }, cancellationToken);
        return ToolResult.Success($"Moved {string.Join(", ", keys)} to the backlog.");
    }

    /// <summary>
    /// Builds the path of an issue list request.
    /// </summary>
    /// <param name="path">The path without a query string.</param>
    /// <param name="jql">Extra JQL.</param>
    /// <param name="fields">A comma-separated list of fields.</param>
    /// <param name="startAt">The index of the first issue.</param>
    /// <param name="maxResults">The largest number of issues.</param>
    /// <returns>The path with its query string.</returns>
    internal static string IssuePath(string path, string? jql, string? fields, int? startAt, int? maxResults)
        => new QueryString(path)
            .Add("jql", jql)
            .Add("fields", string.IsNullOrWhiteSpace(fields) ? null : string.Join(',', JsonArguments.SplitList(fields)))
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

    /// <summary>
    /// Splits a comma-separated list of issue keys, requiring at least one.
    /// </summary>
    /// <param name="issueKeys">The list.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="McpException">The list is empty.</exception>
    internal static List<string> RequireKeys(string issueKeys)
    {
        List<string> keys = JsonArguments.SplitList(issueKeys);
        if (keys.Count == 0)
        {
            throw new McpException("The issueKeys parameter must list at least one issue key.");
        }

        return keys;
    }
}
