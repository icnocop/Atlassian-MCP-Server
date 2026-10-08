// <copyright file="HistoryTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Issues;

/// <summary>
/// Tools for the change history of Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraIssues)]
public sealed class HistoryTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public HistoryTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the change history of the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="startAt">The index of the first change to return.</param>
    /// <param name="maxResults">The largest number of changes to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of changes.</returns>
    [McpServerTool(Name = "atlassian_jira_get_changelog", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the change history of the specified Jira issue, oldest first: who changed which field, when, and from what to what.")]
    public async Task<string> GetChangelog(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Optional index of the first change to return, for paging. Defaults to 0.")] int? startAt = null,
        [Description("Optional largest number of changes to return. Defaults to 100.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"issue/{JiraClient.Segment(issueKey)}/changelog")
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }
}
