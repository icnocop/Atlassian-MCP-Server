// <copyright file="TransitionTools.cs" company="icnocop">
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
/// Tools for moving Jira issues through their workflow.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraIssues)]
public sealed class TransitionTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public TransitionTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the transitions available for the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transitions.</returns>
    [McpServerTool(Name = "atlassian_jira_get_transitions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the workflow transitions available for the specified Jira issue in its current status: ID, name, target status, and the fields each transition requires.")]
    public async Task<string> GetAll(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        CancellationToken cancellationToken = default)
    {
        JsonNode? response = await this.jira.GetAsync($"issue/{JiraClient.Segment(issueKey)}/transitions?expand=transitions.fields", cancellationToken);

        var transitions = (response?["transitions"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(transition => new
            {
                id = transition["id"]?.GetValue<string>(),
                name = transition["name"]?.GetValue<string>(),
                toStatus = transition["to"]?["name"]?.GetValue<string>(),
                toStatusCategory = transition["to"]?["statusCategory"]?["name"]?.GetValue<string>(),
                requiredFields = (transition["fields"] as JsonObject ?? [])
                    .Where(field => field.Value?["required"]?.GetValue<bool>() == true)
                    .Select(field => field.Key)
                    .ToList(),
            });

        return ToolResult.Json(transitions);
    }

    /// <summary>
    /// Moves the specified issue through a workflow transition.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="transition">The transition ID or name.</param>
    /// <param name="resolution">The resolution name.</param>
    /// <param name="comment">A comment, in Markdown.</param>
    /// <param name="additionalFields">Other fields that the transition requires, as a JSON object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_transition_issue", OpenWorld = true)]
    [Description("Moves the specified Jira issue through a workflow transition, such as Start Progress or Done. Optionally sets the resolution and adds a comment.")]
    public async Task<string> Apply(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The transition ID or name, from atlassian_jira_get_transitions.")] string transition,
        [Description("Optional resolution name, such as Done, Fixed, or Won't Do, for transitions to a resolved status.")] string? resolution = null,
        [Description("Optional comment to add with the transition, in Markdown.")] string? comment = null,
        [Description("Optional JSON object of other fields that the transition screen requires, keyed by field ID.")] string? additionalFields = null,
        CancellationToken cancellationToken = default)
    {
        string issue = JiraClient.Segment(issueKey);
        (string id, string name) = await this.ResolveAsync(issue, transition, cancellationToken);

        JsonObject fieldValues = JsonArguments.ParseObject(additionalFields, nameof(additionalFields)) ?? [];
        if (!string.IsNullOrWhiteSpace(resolution))
        {
            fieldValues["resolution"] = new JsonObject { ["name"] = resolution.Trim() };
        }

        var body = new JsonObject { ["transition"] = new JsonObject { ["id"] = id } };
        if (fieldValues.Count > 0)
        {
            body["fields"] = fieldValues;
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            body["update"] = new JsonObject
            {
                ["comment"] = new JsonArray(new JsonObject { ["add"] = new JsonObject { ["body"] = JiraClient.ToAdf(comment) } }),
            };
        }

        await this.jira.SendAsync(HttpMethod.Post, $"issue/{issue}/transitions", body, cancellationToken);
        return ToolResult.Success($"Applied transition '{name}' to {issueKey}.");
    }

    private async Task<(string Id, string Name)> ResolveAsync(string issue, string transition, CancellationToken cancellationToken)
    {
        string wanted = transition.Trim();
        JsonNode? response = await this.jira.GetAsync($"issue/{issue}/transitions", cancellationToken);
        List<JsonObject> available = (response?["transitions"] as JsonArray ?? []).OfType<JsonObject>().ToList();

        JsonObject? match = available.FirstOrDefault(candidate => string.Equals(candidate["id"]?.GetValue<string>(), wanted, StringComparison.Ordinal))
            ?? available.FirstOrDefault(candidate => string.Equals(candidate["name"]?.GetValue<string>(), wanted, StringComparison.OrdinalIgnoreCase))
            ?? available.FirstOrDefault(candidate => string.Equals(candidate["to"]?["name"]?.GetValue<string>(), wanted, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            string names = string.Join(", ", available.Select(candidate => $"{candidate["name"]} ({candidate["id"]}) -> {candidate["to"]?["name"]}"));
            throw new McpException($"The transition '{transition}' is not available for this issue in its current status. Available transitions: {(names.Length == 0 ? "none" : names)}.");
        }

        return (match["id"]!.GetValue<string>(), match["name"]?.GetValue<string>() ?? wanted);
    }
}
