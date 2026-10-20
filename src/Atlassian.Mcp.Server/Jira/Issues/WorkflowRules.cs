// <copyright file="WorkflowRules.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Http;

namespace Atlassian.Mcp.Server.Jira.Issues;

/// <summary>
/// The conditions and validators of the transitions in an issue's workflow, described in words.
/// <para>
/// Jira leaves a transition out of an issue's transitions when one of its conditions is not met, such
/// as a field that must have a value, so the caller cannot tell why it is missing. The workflow
/// definition says why: each rule has a key, such as <c>system:check-field-value</c>, and parameters.
/// Reading it can need more permission than moving the issue; <see cref="Unavailable"/> says why it
/// could not be read.
/// </para>
/// </summary>
public sealed class WorkflowRules
{
    private readonly Dictionary<string, JsonObject> transitions;
    private readonly IReadOnlyDictionary<string, string> fieldNames;

    private WorkflowRules(Dictionary<string, JsonObject> transitions, IReadOnlyDictionary<string, string> fieldNames, string? unavailable)
    {
        this.transitions = transitions;
        this.fieldNames = fieldNames;
        this.Unavailable = unavailable;
    }

    /// <summary>
    /// Gets why the workflow's rules could not be read, or <see langword="null"/> when they were.
    /// </summary>
    public string? Unavailable { get; }

    /// <summary>
    /// Reads the rules of the workflow that the specified issue is in.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    /// <param name="metadata">The cache of the site's fields, for their names.</param>
    /// <param name="issue">The escaped issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rules; when they cannot be read, <see cref="Unavailable"/> says why.</returns>
    public static async Task<WorkflowRules> LoadAsync(JiraClient jira, JiraMetadataCache metadata, string issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jira);
        ArgumentNullException.ThrowIfNull(metadata);

        try
        {
            JsonNode? fields = (await jira.GetAsync($"issue/{issue}?fields=project,issuetype", cancellationToken))?["fields"];
            string? projectId = (string?)fields?["project"]?["id"];
            string? issueTypeId = (string?)fields?["issuetype"]?["id"];
            if (projectId is null || issueTypeId is null)
            {
                return Empty("Jira did not return the issue's project and issue type.");
            }

            var body = new JsonObject
            {
                ["projectAndIssueTypes"] = new JsonArray(new JsonObject { ["projectId"] = projectId, ["issueTypeId"] = issueTypeId }),
            };
            JsonNode? response = await jira.SendAsync(HttpMethod.Post, "workflows?expand=values.transitions", body, cancellationToken);
            JsonObject? workflow = (response?["workflows"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
            if (workflow is null)
            {
                return Empty("Jira did not return the issue's workflow.");
            }

            var transitions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (JsonObject transition in (workflow["transitions"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (IdOf(transition) is { } id)
                {
                    transitions[id] = transition;
                }
            }

            IReadOnlyDictionary<string, string> fieldNames;
            try
            {
                fieldNames = await metadata.GetFieldNamesAsync(jira, cancellationToken);
            }
            catch (AtlassianApiException)
            {
                // the rules still read correctly with field IDs instead of names
                fieldNames = new Dictionary<string, string>();
            }

            return new WorkflowRules(transitions, fieldNames, unavailable: null);
        }
        catch (AtlassianApiException exception)
        {
            return Empty($"The workflow's rules could not be read: {exception.Message}");
        }
    }

    /// <summary>
    /// Describes the conditions of the specified transition, which must all be met for Jira to offer it.
    /// </summary>
    /// <param name="transitionId">The transition ID.</param>
    /// <returns>The conditions, such as "Severity (customfield_10050) is not empty"; or <see langword="null"/> when it has none.</returns>
    public string? DescribeConditions(string transitionId)
    {
        if (!this.transitions.TryGetValue(transitionId, out JsonObject? transition) || transition["conditions"] is not JsonObject conditions)
        {
            return null;
        }

        string description = this.DescribeConditionGroup(conditions);
        return description.Length == 0 ? null : description;
    }

    /// <summary>
    /// Describes the validators of the specified transition, which check the issue when it is moved.
    /// </summary>
    /// <param name="transitionId">The transition ID.</param>
    /// <returns>The validators; empty when it has none.</returns>
    public IReadOnlyList<string> DescribeValidators(string transitionId)
    {
        if (!this.transitions.TryGetValue(transitionId, out JsonObject? transition))
        {
            return [];
        }

        return (transition["validators"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(this.DescribeRule)
            .ToList();
    }

    /// <summary>
    /// Describes one rule, a condition or a validator.
    /// </summary>
    /// <param name="rule">The rule, with its <c>ruleKey</c> and <c>parameters</c>.</param>
    /// <returns>The description.</returns>
    internal string DescribeRule(JsonObject rule)
    {
        string ruleKey = (string?)rule["ruleKey"] ?? "unknown rule";
        JsonObject parameters = rule["parameters"] as JsonObject ?? [];

        switch (ruleKey)
        {
            case "system:check-field-value":
            {
                string field = this.FieldName((string?)parameters["fieldId"]);
                string comparator = (string?)parameters["comparator"] ?? "=";
                List<string> values = ParseValues((string?)parameters["fieldValue"]);
                if (values.All(value => value.Length == 0))
                {
                    return comparator == "!=" ? $"{field} is not empty" : $"{field} is empty";
                }

                string kind = string.Equals((string?)parameters["comparisonType"], "OPTIONID", StringComparison.OrdinalIgnoreCase) ? " (option IDs)" : string.Empty;
                return $"{field} {comparator} {string.Join(", ", values)}{kind}";
            }

            case "system:validate-field-value" when string.Equals((string?)parameters["ruleType"], "fieldRequired", StringComparison.OrdinalIgnoreCase):
            {
                string fields = string.Join(", ", ((string?)parameters["fieldsRequired"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(this.FieldName));
                string message = (string?)parameters["errorMessage"] is { Length: > 0 } text ? $" (\"{text}\")" : string.Empty;
                return $"{fields} must have a value{message}";
            }

            default:
                return parameters.Count == 0 ? ruleKey : $"{ruleKey} {parameters.ToJsonString()}";
        }
    }

    private static WorkflowRules Empty(string reason)
        => new([], new Dictionary<string, string>(), reason);

    private static string? IdOf(JsonObject transition)
        => transition["id"] switch
        {
            JsonValue value when value.TryGetValue(out string? text) => text,
            JsonValue value when value.TryGetValue(out long number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };

    /// <summary>
    /// Parses a field value parameter, which Jira gives as a JSON array in a string, such as <c>["",""]</c>.
    /// </summary>
    /// <param name="fieldValue">The parameter.</param>
    /// <returns>The values.</returns>
    private static List<string> ParseValues(string? fieldValue)
    {
        if (string.IsNullOrWhiteSpace(fieldValue))
        {
            return [string.Empty];
        }

        try
        {
            if (JsonNode.Parse(fieldValue) is JsonArray array)
            {
                return array.Select(value => value?.ToString() ?? string.Empty).ToList();
            }
        }
        catch (JsonException)
        {
            // not JSON: the value as it is
        }

        return [fieldValue];
    }

    private string DescribeConditionGroup(JsonObject group)
    {
        string separator = string.Equals((string?)group["operation"], "ANY", StringComparison.OrdinalIgnoreCase) ? " or " : " and ";

        IEnumerable<string> rules = (group["conditions"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(this.DescribeRule);
        IEnumerable<string> groups = (group["conditionGroups"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(this.DescribeConditionGroup)
            .Where(description => description.Length > 0)
            .Select(description => $"({description})");

        return string.Join(separator, rules.Concat(groups));
    }

    private string FieldName(string? fieldId)
    {
        if (string.IsNullOrEmpty(fieldId))
        {
            return "a field";
        }

        return this.fieldNames.TryGetValue(fieldId, out string? name) ? $"{name} ({fieldId})" : fieldId;
    }
}
