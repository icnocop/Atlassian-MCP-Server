// <copyright file="MetadataTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Fields;

/// <summary>
/// Tools for the site-wide values that issue fields take: issue types, priorities, statuses, and resolutions.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraFields)]
public sealed class MetadataTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public MetadataTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets every issue type of the site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issue types.</returns>
    [McpServerTool(Name = "atlassian_jira_get_issue_types", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets every issue type of the Jira site: ID, name, description, and whether it is a subtask type.")]
    public async Task<string> GetIssueTypes(CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync("issuetype", cancellationToken));

    /// <summary>
    /// Gets every priority of the site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The priorities.</returns>
    [McpServerTool(Name = "atlassian_jira_get_priorities", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets every issue priority of the Jira site: ID, name, and description.")]
    public async Task<string> GetPriorities(CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync("priority", cancellationToken));

    /// <summary>
    /// Gets every status of the site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statuses.</returns>
    [McpServerTool(Name = "atlassian_jira_get_statuses", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets every workflow status of the Jira site: ID, name, and status category (To Do, In Progress, or Done).")]
    public async Task<string> GetStatuses(CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync("status", cancellationToken));

    /// <summary>
    /// Gets every resolution of the site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolutions.</returns>
    [McpServerTool(Name = "atlassian_jira_get_resolutions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets every issue resolution of the Jira site, such as Done, Won't Do, and Duplicate.")]
    public async Task<string> GetResolutions(CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync("resolution", cancellationToken));
}
