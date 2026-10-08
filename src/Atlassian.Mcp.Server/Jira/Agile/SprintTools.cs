// <copyright file="SprintTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Agile;

/// <summary>
/// Tools for Jira Software sprints.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraAgile)]
public sealed class SprintTools
{
    private readonly JiraClient jira;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public SprintTools(JiraClient jira)
        : this(jira, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    /// <param name="timeProvider">The clock used for default dates.</param>
    internal SprintTools(JiraClient jira, TimeProvider timeProvider)
    {
        this.jira = jira;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the sprints of the specified board.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="state">The sprint states to include.</param>
    /// <param name="startAt">The index of the first sprint to return.</param>
    /// <param name="maxResults">The largest number of sprints to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sprints.</returns>
    [McpServerTool(Name = "atlassian_jira_list_sprints", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the sprints of the specified Jira Software board, optionally filtered by state.")]
    public async Task<string> GetAll(
        [Description("The board ID.")] int boardId,
        [Description("A comma-separated list of the sprint states to include: future, active, closed. Defaults to all.")] string? state = null,
        [Description("The index of the first sprint to return, starting at 0.")] int? startAt = null,
        [Description("The largest number of sprints to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"board/{boardId}/sprint")
            .Add("state", state?.Replace(" ", string.Empty, StringComparison.Ordinal))
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAgileAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the specified sprint.
    /// </summary>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sprint.</returns>
    [McpServerTool(Name = "atlassian_jira_get_sprint", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified Jira Software sprint: name, state, goal, dates, and board.")]
    public async Task<string> Get(
        [Description("The sprint ID.")] int sprintId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync($"sprint/{sprintId}", cancellationToken));

    /// <summary>
    /// Gets the issues in the specified sprint.
    /// </summary>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="jql">Extra JQL to filter the issues.</param>
    /// <param name="fields">The fields to return.</param>
    /// <param name="startAt">The index of the first issue to return.</param>
    /// <param name="maxResults">The largest number of issues to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issues.</returns>
    [McpServerTool(Name = "atlassian_jira_get_sprint_issues", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the issues in the specified Jira Software sprint.")]
    public async Task<string> GetIssues(
        [Description("The sprint ID.")] int sprintId,
        [Description("Extra JQL that the issues must also match.")] string? jql = null,
        [Description("A comma-separated list of the fields to return. Defaults to all navigable fields.")] string? fields = null,
        [Description("The index of the first issue to return, starting at 0.")] int? startAt = null,
        [Description("The largest number of issues to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAgileAsync(BoardTools.IssuePath($"sprint/{sprintId}/issue", jql, fields, startAt, maxResults), cancellationToken));

    /// <summary>
    /// Creates a sprint.
    /// </summary>
    /// <param name="name">The sprint name.</param>
    /// <param name="originBoardId">The board ID.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="goal">The sprint goal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new sprint.</returns>
    [McpServerTool(Name = "atlassian_jira_create_sprint", OpenWorld = true)]
    [Description("Creates a future sprint on the specified Jira Software board.")]
    public async Task<string> Create(
        [Description("The sprint name.")] string name,
        [Description("The ID of the board that the sprint belongs to.")] int originBoardId,
        [Description("The planned start date and time, in ISO 8601 format, such as 2026-01-05T09:00:00.000Z.")] string? startDate = null,
        [Description("The planned end date and time, in ISO 8601 format.")] string? endDate = null,
        [Description("The sprint goal.")] string? goal = null,
        CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["name"] = name, ["originBoardId"] = originBoardId };
        AddIfSet(body, "startDate", startDate);
        AddIfSet(body, "endDate", endDate);
        AddIfSet(body, "goal", goal);

        return ToolResult.Json(await this.jira.SendAgileAsync(HttpMethod.Post, "sprint", body, cancellationToken));
    }

    /// <summary>
    /// Changes the specified sprint. Only the values given are changed.
    /// </summary>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="name">The new name.</param>
    /// <param name="goal">The new goal.</param>
    /// <param name="startDate">The new start date.</param>
    /// <param name="endDate">The new end date.</param>
    /// <param name="state">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated sprint.</returns>
    [McpServerTool(Name = "atlassian_jira_update_sprint", OpenWorld = true)]
    [Description("Changes the specified Jira Software sprint. Only the values given are changed. To start or close a sprint, prefer atlassian_jira_start_sprint and atlassian_jira_close_sprint.")]
    public async Task<string> Update(
        [Description("The sprint ID.")] int sprintId,
        [Description("The new sprint name.")] string? name = null,
        [Description("The new sprint goal.")] string? goal = null,
        [Description("The new start date and time, in ISO 8601 format.")] string? startDate = null,
        [Description("The new end date and time, in ISO 8601 format.")] string? endDate = null,
        [Description("The new state: future, active, or closed.")] string? state = null,
        CancellationToken cancellationToken = default)
    {
        var body = new JsonObject();
        AddIfSet(body, "name", name);
        AddIfSet(body, "goal", goal);
        AddIfSet(body, "startDate", startDate);
        AddIfSet(body, "endDate", endDate);
        AddIfSet(body, "state", state?.Trim().ToLowerInvariant());

        if (body.Count == 0)
        {
            throw new McpException("Give at least one value to change: name, goal, startDate, endDate, or state.");
        }

        return ToolResult.Json(await this.jira.SendAgileAsync(HttpMethod.Post, $"sprint/{sprintId}", body, cancellationToken));
    }

    /// <summary>
    /// Starts the specified sprint.
    /// </summary>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="startDate">The start date, or now.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The started sprint.</returns>
    [McpServerTool(Name = "atlassian_jira_start_sprint", OpenWorld = true)]
    [Description("Starts the specified future Jira Software sprint, making it active.")]
    public async Task<string> Start(
        [Description("The sprint ID.")] int sprintId,
        [Description("The end date and time, in ISO 8601 format, such as 2026-01-19T17:00:00.000Z.")] string endDate,
        [Description("The start date and time, in ISO 8601 format. Defaults to now.")] string? startDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endDate))
        {
            throw new McpException("The endDate parameter is required to start a sprint.");
        }

        var body = new JsonObject
        {
            ["state"] = "active",
            ["startDate"] = string.IsNullOrWhiteSpace(startDate) ? this.Now() : startDate.Trim(),
            ["endDate"] = endDate.Trim(),
        };

        return ToolResult.Json(await this.jira.SendAgileAsync(HttpMethod.Post, $"sprint/{sprintId}", body, cancellationToken));
    }

    /// <summary>
    /// Closes the specified sprint.
    /// </summary>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="completeDate">The completion date, or now.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The closed sprint.</returns>
    [McpServerTool(Name = "atlassian_jira_close_sprint", OpenWorld = true)]
    [Description("Closes the specified active Jira Software sprint. Incomplete issues stay where Jira's rules put them.")]
    public async Task<string> Close(
        [Description("The sprint ID.")] int sprintId,
        [Description("The completion date and time, in ISO 8601 format. Defaults to now.")] string? completeDate = null,
        CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["state"] = "closed",
            ["completeDate"] = string.IsNullOrWhiteSpace(completeDate) ? this.Now() : completeDate.Trim(),
        };

        return ToolResult.Json(await this.jira.SendAgileAsync(HttpMethod.Post, $"sprint/{sprintId}", body, cancellationToken));
    }

    /// <summary>
    /// Deletes the specified sprint.
    /// </summary>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_sprint", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified Jira Software sprint. Its issues move to the backlog.")]
    public async Task<string> Delete(
        [Description("The sprint ID.")] int sprintId,
        CancellationToken cancellationToken = default)
    {
        await this.jira.SendAgileAsync(HttpMethod.Delete, $"sprint/{sprintId}", body: null, cancellationToken);
        return ToolResult.Success($"Deleted sprint {sprintId}.");
    }

    /// <summary>
    /// Moves the specified issues into the specified sprint.
    /// </summary>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="issueKeys">The issue keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_move_issues_to_sprint", Idempotent = true, OpenWorld = true)]
    [Description("Moves the specified Jira issues into the specified open sprint.")]
    public async Task<string> MoveIssues(
        [Description("The sprint ID.")] int sprintId,
        [Description("A comma-separated list of issue keys, such as \"PROJ-1,PROJ-2\". At most 50.")] string issueKeys,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = BoardTools.RequireKeys(issueKeys);
        await this.jira.SendAgileAsync(HttpMethod.Post, $"sprint/{sprintId}/issue", new { issues = keys }, cancellationToken);
        return ToolResult.Success($"Moved {string.Join(", ", keys)} to sprint {sprintId}.");
    }

    private static void AddIfSet(JsonObject body, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            body[name] = value.Trim();
        }
    }

    private string Now()
        => this.timeProvider.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
