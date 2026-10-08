// <copyright file="LabelTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Issues;

/// <summary>
/// Tools for adding and removing the labels of Jira issues without replacing the others.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraIssues)]
public sealed class LabelTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="LabelTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public LabelTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Adds labels to the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="labels">The labels, comma-separated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_add_labels", Idempotent = true, OpenWorld = true)]
    [Description("Adds labels to the specified Jira issue, keeping its existing labels.")]
    public Task<string> Add(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Comma-separated labels to add. Labels cannot contain spaces.")] string labels,
        CancellationToken cancellationToken = default)
        => this.ChangeAsync(issueKey, labels, "add", "Added", cancellationToken);

    /// <summary>
    /// Removes labels from the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="labels">The labels, comma-separated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_remove_labels", Idempotent = true, OpenWorld = true)]
    [Description("Removes labels from the specified Jira issue, keeping its other labels.")]
    public Task<string> Remove(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Comma-separated labels to remove.")] string labels,
        CancellationToken cancellationToken = default)
        => this.ChangeAsync(issueKey, labels, "remove", "Removed", cancellationToken);

    private async Task<string> ChangeAsync(string issueKey, string labels, string operation, string verb, CancellationToken cancellationToken)
    {
        List<string> names = JsonArguments.SplitList(labels);
        if (names.Count == 0)
        {
            throw new McpException("The labels parameter must name at least one label.");
        }

        var body = new JsonObject
        {
            ["update"] = new JsonObject
            {
                ["labels"] = new JsonArray(names.Select(name => (JsonNode)new JsonObject { [operation] = name }).ToArray()),
            },
        };

        await this.jira.SendAsync(HttpMethod.Put, $"issue/{JiraClient.Segment(issueKey)}", body, cancellationToken);
        return ToolResult.Success($"{verb} labels on {issueKey}: {string.Join(", ", names)}.");
    }
}
