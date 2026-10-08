// <copyright file="TimeTrackingTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Worklogs;

/// <summary>
/// Tools for the time estimates of Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraWorklogs)]
public sealed class TimeTrackingTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeTrackingTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public TimeTrackingTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the time tracking of the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original estimate, remaining estimate, and time spent.</returns>
    [McpServerTool(Name = "atlassian_jira_get_time_tracking", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the original estimate, remaining estimate, and time spent for the specified Jira issue.")]
    public async Task<string> Get(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        CancellationToken cancellationToken = default)
    {
        JsonNode? issue = await this.jira.GetAsync($"issue/{JiraClient.Segment(issueKey)}?fields=timetracking", cancellationToken);
        return ToolResult.Json(issue?["fields"]?["timetracking"] ?? new JsonObject());
    }

    /// <summary>
    /// Sets the original estimate, the remaining estimate, or both, of the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="originalEstimate">The original estimate.</param>
    /// <param name="remainingEstimate">The remaining estimate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_set_time_estimate", Idempotent = true, OpenWorld = true)]
    [Description("Sets the original estimate, the remaining estimate, or both, of the specified Jira issue.")]
    public async Task<string> SetEstimate(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Optional original estimate, as a Jira duration such as 2d or 4h 30m.")] string? originalEstimate = null,
        [Description("Optional remaining estimate, as a Jira duration such as 2d or 4h 30m.")] string? remainingEstimate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalEstimate) && string.IsNullOrWhiteSpace(remainingEstimate))
        {
            throw new McpException("Pass originalEstimate, remainingEstimate, or both.");
        }

        var body = new
        {
            fields = new
            {
                timetracking = new
                {
                    originalEstimate = string.IsNullOrWhiteSpace(originalEstimate) ? null : originalEstimate.Trim(),
                    remainingEstimate = string.IsNullOrWhiteSpace(remainingEstimate) ? null : remainingEstimate.Trim(),
                },
            },
        };

        await this.jira.SendAsync(HttpMethod.Put, $"issue/{JiraClient.Segment(issueKey)}", body, cancellationToken);
        return ToolResult.Success($"Updated the time estimates of issue {issueKey}.");
    }
}
