// <copyright file="RankTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Agile;

/// <summary>
/// Tools that change the rank (order) of Jira issues and epics.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraAgile)]
public sealed class RankTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="RankTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public RankTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Ranks the specified issues before or after an anchor issue.
    /// </summary>
    /// <param name="issueKeys">The issue keys, in the order they should end up.</param>
    /// <param name="rankBeforeIssue">The issue to rank them before.</param>
    /// <param name="rankAfterIssue">The issue to rank them after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ranking result.</returns>
    [McpServerTool(Name = "atlassian_jira_rank_issues", Idempotent = true, OpenWorld = true)]
    [Description("Ranks the specified Jira issues immediately before or after an anchor issue. Give exactly one of rankBeforeIssue and rankAfterIssue.")]
    public async Task<string> MoveIssues(
        [Description("A comma-separated list of the issue keys to move, in the order they should end up. At most 50.")] string issueKeys,
        [Description("The key of the issue to rank them before.")] string? rankBeforeIssue = null,
        [Description("The key of the issue to rank them after.")] string? rankAfterIssue = null,
        CancellationToken cancellationToken = default)
    {
        List<string> keys = BoardTools.RequireKeys(issueKeys);
        var body = new JsonObject { ["issues"] = new JsonArray(keys.Select(key => (JsonNode)key).ToArray()) };
        AddAnchor(body, "rankBeforeIssue", rankBeforeIssue, "rankAfterIssue", rankAfterIssue);

        JsonNode? response = await this.jira.SendAgileAsync(HttpMethod.Put, "issue/rank", body, cancellationToken);
        return response is null
            ? ToolResult.Success($"Ranked {string.Join(", ", keys)}.")
            : ToolResult.Json(response);
    }

    /// <summary>
    /// Ranks the specified epic before or after another epic.
    /// </summary>
    /// <param name="epicIdOrKey">The epic to move.</param>
    /// <param name="rankBeforeEpic">The epic to rank it before.</param>
    /// <param name="rankAfterEpic">The epic to rank it after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_rank_epic", Idempotent = true, OpenWorld = true)]
    [Description("Ranks the specified Jira epic immediately before or after another epic. Give exactly one of rankBeforeEpic and rankAfterEpic.")]
    public async Task<string> MoveEpic(
        [Description("The key or ID of the epic to move.")] string epicIdOrKey,
        [Description("The key or ID of the epic to rank it before.")] string? rankBeforeEpic = null,
        [Description("The key or ID of the epic to rank it after.")] string? rankAfterEpic = null,
        CancellationToken cancellationToken = default)
    {
        var body = new JsonObject();
        AddAnchor(body, "rankBeforeEpic", rankBeforeEpic, "rankAfterEpic", rankAfterEpic);

        await this.jira.SendAgileAsync(HttpMethod.Put, $"epic/{JiraClient.Segment(epicIdOrKey)}/rank", body, cancellationToken);
        return ToolResult.Success($"Ranked epic {epicIdOrKey.Trim()}.");
    }

    /// <summary>
    /// Adds exactly one rank anchor to a request body.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="beforeName">The name of the "before" property.</param>
    /// <param name="before">The "before" anchor.</param>
    /// <param name="afterName">The name of the "after" property.</param>
    /// <param name="after">The "after" anchor.</param>
    /// <exception cref="McpException">Neither or both anchors are given.</exception>
    internal static void AddAnchor(JsonObject body, string beforeName, string? before, string afterName, string? after)
    {
        bool hasBefore = !string.IsNullOrWhiteSpace(before);
        bool hasAfter = !string.IsNullOrWhiteSpace(after);

        if (hasBefore == hasAfter)
        {
            throw new McpException($"Give exactly one of {beforeName} and {afterName}.");
        }

        if (hasBefore)
        {
            body[beforeName] = before!.Trim();
        }
        else
        {
            body[afterName] = after!.Trim();
        }
    }
}
