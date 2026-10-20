// <copyright file="TransitionTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
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
    private readonly JiraMetadataCache metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    /// <param name="metadata">The cache of site-wide definitions, for the names of fields in workflow rules.</param>
    public TransitionTools(JiraClient jira, JiraMetadataCache metadata)
    {
        this.jira = jira;
        this.metadata = metadata;
    }

    /// <summary>
    /// Gets the transitions available for the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="includeUnavailable">Whether to include the transitions that a workflow condition hides, with the conditions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transitions.</returns>
    [McpServerTool(Name = "atlassian_jira_get_transitions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the workflow transitions available for the specified Jira issue in its current status: ID, name, target status, and the fields each transition requires. Jira hides a transition whose workflow condition is not met, such as a field that must have a value; pass includeUnavailable to list those too, with why.")]
    public async Task<string> GetAll(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Whether to also list the transitions that a workflow condition hides in the issue's current state. Each transition then has isAvailable, its conditions (what must be true for Jira to offer it, such as \"Severity (customfield_10050) is not empty\"), and its validators (what is checked when it is applied). Reading the workflow can need administrator permission; without it, rulesUnavailable says why. Defaults to false.")] bool includeUnavailable = false,
        CancellationToken cancellationToken = default)
    {
        string issue = JiraClient.Segment(issueKey);
        string path = includeUnavailable
            ? $"issue/{issue}/transitions?expand=transitions.fields&includeUnavailableTransitions=true"
            : $"issue/{issue}/transitions?expand=transitions.fields";
        JsonNode? response = await this.jira.GetAsync(path, cancellationToken);
        List<JsonObject> found = (response?["transitions"] as JsonArray ?? []).OfType<JsonObject>().ToList();

        if (!includeUnavailable)
        {
            return ToolResult.Json(found.Select(transition => new
            {
                id = transition["id"]?.GetValue<string>(),
                name = transition["name"]?.GetValue<string>(),
                toStatus = transition["to"]?["name"]?.GetValue<string>(),
                toStatusCategory = transition["to"]?["statusCategory"]?["name"]?.GetValue<string>(),
                requiredFields = RequiredFields(transition),
            }));
        }

        WorkflowRules rules = await WorkflowRules.LoadAsync(this.jira, this.metadata, issue, cancellationToken);

        return ToolResult.Json(found.Select(transition =>
        {
            string? id = transition["id"]?.GetValue<string>();
            return new
            {
                id,
                name = transition["name"]?.GetValue<string>(),
                toStatus = transition["to"]?["name"]?.GetValue<string>(),
                toStatusCategory = transition["to"]?["statusCategory"]?["name"]?.GetValue<string>(),
                requiredFields = RequiredFields(transition),
                isAvailable = (bool?)transition["isAvailable"] ?? true,
                conditions = id is null ? null : rules.DescribeConditions(id),
                validators = id is null ? [] : rules.DescribeValidators(id),
                rulesUnavailable = rules.Unavailable,
            };
        }));
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
            JsonObject? adf = await this.jira.ToAdfAsync(comment, cancellationToken);
            body["update"] = new JsonObject
            {
                ["comment"] = new JsonArray(new JsonObject { ["add"] = new JsonObject { ["body"] = adf } }),
            };
        }

        await this.jira.SendAsync(HttpMethod.Post, $"issue/{issue}/transitions", body, cancellationToken);
        return ToolResult.Success($"Applied transition '{name}' to {issueKey}.");
    }

    private static JsonObject? Find(List<JsonObject> transitions, string wanted)
        => transitions.FirstOrDefault(candidate => string.Equals(candidate["id"]?.GetValue<string>(), wanted, StringComparison.Ordinal))
            ?? transitions.FirstOrDefault(candidate => string.Equals(candidate["name"]?.GetValue<string>(), wanted, StringComparison.OrdinalIgnoreCase))
            ?? transitions.FirstOrDefault(candidate => string.Equals(candidate["to"]?["name"]?.GetValue<string>(), wanted, StringComparison.OrdinalIgnoreCase));

    private static List<string> RequiredFields(JsonObject transition)
        => (transition["fields"] as JsonObject ?? [])
            .Where(field => field.Value?["required"]?.GetValue<bool>() == true)
            .Select(field => field.Key)
            .ToList();

    private async Task<(string Id, string Name)> ResolveAsync(string issue, string transition, CancellationToken cancellationToken)
    {
        string wanted = transition.Trim();
        JsonNode? response = await this.jira.GetAsync($"issue/{issue}/transitions", cancellationToken);
        List<JsonObject> available = (response?["transitions"] as JsonArray ?? []).OfType<JsonObject>().ToList();

        JsonObject? match = Find(available, wanted);
        if (match is not null)
        {
            return (match["id"]!.GetValue<string>(), match["name"]?.GetValue<string>() ?? wanted);
        }

        string names = string.Join(", ", available.Select(candidate => $"{candidate["name"]} ({candidate["id"]}) -> {candidate["to"]?["name"]}"));
        string availableTransitions = $"Available transitions: {(names.Length == 0 ? "none" : names)}.";

        // Jira leaves out a transition whose workflow condition is not met; say which, so that the
        // caller can set what is missing rather than conclude the transition does not exist.
        JsonObject? hidden = null;
        try
        {
            JsonNode? all = await this.jira.GetAsync($"issue/{issue}/transitions?includeUnavailableTransitions=true", cancellationToken);
            hidden = Find((all?["transitions"] as JsonArray ?? []).OfType<JsonObject>().ToList(), wanted);
        }
        catch (AtlassianApiException)
        {
            // only the explanation is lost; the transition is still not available
        }

        if (hidden?["id"]?.GetValue<string>() is { } hiddenId && (bool?)hidden["isAvailable"] == false)
        {
            WorkflowRules rules = await WorkflowRules.LoadAsync(this.jira, this.metadata, issue, cancellationToken);
            string why = rules.DescribeConditions(hiddenId) is { } conditions
                ? $"It requires: {conditions}. Set what is missing, for example with atlassian_jira_update_issue, and try again."
                : $"A workflow condition is not met. {rules.Unavailable ?? "The workflow does not describe it."}";
            throw new McpException($"The transition '{hidden["name"]}' ({hiddenId}) -> {hidden["to"]?["name"]} is hidden for this issue in its current state. {why} {availableTransitions}");
        }

        throw new McpException($"The transition '{transition}' is not available for this issue in its current status. {availableTransitions}");
    }
}
