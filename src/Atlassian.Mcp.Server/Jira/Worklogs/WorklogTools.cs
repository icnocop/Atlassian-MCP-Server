// <copyright file="WorklogTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Worklogs;

/// <summary>
/// Tools for the worklogs (logged work) of Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraWorklogs)]
public sealed class WorklogTools
{
    /// <summary>The ways a worklog change can adjust the remaining estimate of its issue.</summary>
    private static readonly HashSet<string> AdjustEstimateModes = new(StringComparer.Ordinal) { "new", "leave", "manual", "auto" };

    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorklogTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public WorklogTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>Gets or sets the clock that supplies the default start time of new worklogs.</summary>
    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets the worklogs of the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of worklogs.</returns>
    [McpServerTool(Name = "atlassian_jira_get_worklogs", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the worklogs (logged work) of the specified Jira issue, one page at a time.")]
    public async Task<string> GetAll(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"issue/{JiraClient.Segment(issueKey)}/worklog")
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the specified worklog.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="worklogId">The worklog ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The worklog.</returns>
    [McpServerTool(Name = "atlassian_jira_get_worklog", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified worklog of the specified Jira issue.")]
    public async Task<string> Get(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The worklog ID.")] string worklogId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync(
            $"issue/{JiraClient.Segment(issueKey)}/worklog/{JiraClient.Segment(worklogId)}",
            cancellationToken));

    /// <summary>
    /// Gets several worklogs at once.
    /// </summary>
    /// <param name="worklogIds">A comma-separated list of worklog IDs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The worklogs.</returns>
    [McpServerTool(Name = "atlassian_jira_get_worklogs_by_ids", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the Jira worklogs with the specified IDs, from any issues, in one request (up to 1000).")]
    public async Task<string> GetByIds(
        [Description("Comma-separated worklog IDs.")] string worklogIds,
        CancellationToken cancellationToken = default)
    {
        List<long> ids = JsonArguments.SplitList(worklogIds)
            .Select(id => long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? value
                : throw new McpException($"The worklog ID '{id}' is not a number."))
            .ToList();

        if (ids.Count == 0)
        {
            throw new McpException("The worklogIds parameter must list at least one worklog ID.");
        }

        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, "worklog/list", new { ids }, cancellationToken));
    }

    /// <summary>
    /// Gets the IDs of the worklogs updated since the specified time.
    /// </summary>
    /// <param name="since">The time, in milliseconds since the Unix epoch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of worklog IDs and update times.</returns>
    [McpServerTool(Name = "atlassian_jira_get_worklogs_updated_since", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the IDs of the Jira worklogs created or updated since the specified time, up to 1000 per page. Pass the IDs to atlassian_jira_get_worklogs_by_ids for details.")]
    public async Task<string> GetUpdatedSince(
        [Description("The time, in milliseconds since the Unix epoch. Use the 'until' value of the previous page to get the next page.")] long since,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync(new QueryString("worklog/updated").Add("since", since).ToString(), cancellationToken));

    /// <summary>
    /// Logs work on the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="timeSpent">The time spent.</param>
    /// <param name="started">The start time.</param>
    /// <param name="comment">The comment, in Markdown.</param>
    /// <param name="adjustEstimate">How to adjust the remaining estimate.</param>
    /// <param name="newEstimate">The new remaining estimate.</param>
    /// <param name="reduceBy">The amount to reduce the remaining estimate by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new worklog.</returns>
    [McpServerTool(Name = "atlassian_jira_add_worklog", OpenWorld = true)]
    [Description("Logs work on the specified Jira issue and adjusts its remaining estimate.")]
    public async Task<string> Add(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The time spent, as a Jira duration such as 30m, 2h, or 1d 4h.")] string timeSpent,
        [Description("Optional start time, as an ISO 8601 date and time such as 2026-01-15T09:30:00Z. Defaults to now.")] string? started = null,
        [Description("Optional comment, in Markdown.")] string? comment = null,
        [Description("Optional adjustment of the remaining estimate: auto (reduce by the time spent; the default), new (set to newEstimate), manual (reduce by reduceBy), or leave (unchanged).")] string? adjustEstimate = null,
        [Description("The new remaining estimate, as a Jira duration. Required when adjustEstimate is new.")] string? newEstimate = null,
        [Description("The amount to reduce the remaining estimate by, as a Jira duration. Required when adjustEstimate is manual.")] string? reduceBy = null,
        CancellationToken cancellationToken = default)
    {
        string path = BuildPath($"issue/{JiraClient.Segment(issueKey)}/worklog", adjustEstimate, newEstimate, "reduceBy", reduceBy);
        var body = new
        {
            timeSpent,
            started = FormatStarted(started, this.Clock),
            comment = JiraClient.ToAdf(comment),
        };

        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, path, body, cancellationToken));
    }

    /// <summary>
    /// Updates the specified worklog.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="worklogId">The worklog ID.</param>
    /// <param name="timeSpent">The new time spent.</param>
    /// <param name="started">The new start time.</param>
    /// <param name="comment">The new comment, in Markdown.</param>
    /// <param name="adjustEstimate">How to adjust the remaining estimate.</param>
    /// <param name="newEstimate">The new remaining estimate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated worklog.</returns>
    [McpServerTool(Name = "atlassian_jira_update_worklog", Idempotent = true, OpenWorld = true)]
    [Description("Updates the specified worklog of the specified Jira issue. Only the values that are passed are changed.")]
    public async Task<string> Update(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The worklog ID.")] string worklogId,
        [Description("Optional new time spent, as a Jira duration such as 30m, 2h, or 1d 4h.")] string? timeSpent = null,
        [Description("Optional new start time, as an ISO 8601 date and time.")] string? started = null,
        [Description("Optional new comment, in Markdown.")] string? comment = null,
        [Description("Optional adjustment of the remaining estimate: auto (the default), new (set to newEstimate), or leave (unchanged).")] string? adjustEstimate = null,
        [Description("The new remaining estimate, as a Jira duration. Required when adjustEstimate is new.")] string? newEstimate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(adjustEstimate?.Trim(), "manual", StringComparison.Ordinal))
        {
            throw new McpException("The adjustEstimate parameter cannot be manual when updating a worklog. Use auto, new, or leave.");
        }

        string path = BuildPath(
            $"issue/{JiraClient.Segment(issueKey)}/worklog/{JiraClient.Segment(worklogId)}",
            adjustEstimate,
            newEstimate,
            manualName: null,
            manualValue: null);

        var body = new
        {
            timeSpent,
            started = string.IsNullOrWhiteSpace(started) ? null : FormatStarted(started, this.Clock),
            comment = JiraClient.ToAdf(comment),
        };

        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Put, path, body, cancellationToken));
    }

    /// <summary>
    /// Deletes the specified worklog.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="worklogId">The worklog ID.</param>
    /// <param name="adjustEstimate">How to adjust the remaining estimate.</param>
    /// <param name="newEstimate">The new remaining estimate.</param>
    /// <param name="increaseBy">The amount to increase the remaining estimate by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_worklog", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified worklog of the specified Jira issue and adjusts its remaining estimate.")]
    public async Task<string> Delete(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The worklog ID.")] string worklogId,
        [Description("Optional adjustment of the remaining estimate: auto (increase by the time spent; the default), new (set to newEstimate), manual (increase by increaseBy), or leave (unchanged).")] string? adjustEstimate = null,
        [Description("The new remaining estimate, as a Jira duration. Required when adjustEstimate is new.")] string? newEstimate = null,
        [Description("The amount to increase the remaining estimate by, as a Jira duration. Required when adjustEstimate is manual.")] string? increaseBy = null,
        CancellationToken cancellationToken = default)
    {
        string path = BuildPath(
            $"issue/{JiraClient.Segment(issueKey)}/worklog/{JiraClient.Segment(worklogId)}",
            adjustEstimate,
            newEstimate,
            "increaseBy",
            increaseBy);

        await this.jira.SendAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        return ToolResult.Success($"Deleted worklog {worklogId} from issue {issueKey}.");
    }

    /// <summary>
    /// Formats a start time the way the worklog API requires, such as
    /// <c>2026-01-15T09:30:00.000+0000</c>.
    /// </summary>
    /// <param name="started">The start time as ISO 8601 text, or <see langword="null"/> for now.</param>
    /// <param name="clock">The clock that supplies the current time.</param>
    /// <returns>The formatted start time, in UTC.</returns>
    /// <exception cref="McpException">The start time cannot be parsed.</exception>
    internal static string FormatStarted(string? started, TimeProvider clock)
    {
        DateTimeOffset time;
        if (string.IsNullOrWhiteSpace(started))
        {
            time = clock.GetUtcNow();
        }
        else if (!DateTimeOffset.TryParse(started.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time))
        {
            throw new McpException($"The started parameter '{started}' is not an ISO 8601 date and time, such as 2026-01-15T09:30:00Z.");
        }

        return time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+0000'", CultureInfo.InvariantCulture);
    }

    private static string BuildPath(string path, string? adjustEstimate, string? newEstimate, string? manualName, string? manualValue)
    {
        string? mode = string.IsNullOrWhiteSpace(adjustEstimate) ? null : adjustEstimate.Trim().ToLowerInvariant();
        if (mode is not null && !AdjustEstimateModes.Contains(mode))
        {
            throw new McpException($"The adjustEstimate parameter must be new, leave, manual, or auto, not '{adjustEstimate}'.");
        }

        if (mode == "new" && string.IsNullOrWhiteSpace(newEstimate))
        {
            throw new McpException("The newEstimate parameter is required when adjustEstimate is new.");
        }

        if (mode == "manual" && string.IsNullOrWhiteSpace(manualValue))
        {
            throw new McpException($"The {manualName} parameter is required when adjustEstimate is manual.");
        }

        var query = new QueryString(path).Add("adjustEstimate", mode);
        if (mode == "new")
        {
            query.Add("newEstimate", newEstimate);
        }

        if (mode == "manual" && manualName is not null)
        {
            query.Add(manualName, manualValue);
        }

        return query.ToString();
    }
}
