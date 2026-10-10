// <copyright file="IssueTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Issues;

/// <summary>
/// Tools for reading, creating, updating, and deleting Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraIssues)]
public sealed class IssueTools
{
    private readonly JiraClient jira;
    private readonly JiraMetadataCache metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="IssueTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    /// <param name="metadata">The cache of site-wide Jira definitions.</param>
    public IssueTools(JiraClient jira, JiraMetadataCache metadata)
    {
        this.jira = jira;
        this.metadata = metadata;
    }

    /// <summary>
    /// Gets the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="fields">The fields to return.</param>
    /// <param name="expand">The extra data to include.</param>
    /// <param name="richTextFormat">The format for rich text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issue.</returns>
    [McpServerTool(Name = "atlassian_jira_get_issue", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified Jira issue with its fields, including custom fields (customfield_*) that have a value. Rich text such as the description, environment, comments, and multi-line custom fields is returned as Markdown by default, with embedded files written as `![name](attachment:ID)`.")]
    public async Task<string> Get(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Optional comma-separated fields to return, such as summary,status,customfield_10010. Defaults to all fields.")] string? fields = null,
        [Description("Optional comma-separated extra data, such as changelog,renderedFields,transitions.")] string? expand = null,
        [Description("The format for rich text: markdown (default) or adf.")] string richTextFormat = "markdown",
        CancellationToken cancellationToken = default)
    {
        string Path(bool rendered) => new QueryString($"issue/{JiraClient.Segment(issueKey)}")
            .Add("fields", fields)
            .Add("expand", rendered ? JiraRichText.WithExpand(expand, "renderedFields") : expand)
            .ToString();

        bool renderedRequested = JiraRichText.Expands(expand, "renderedFields");
        if (!JiraRichText.IsMarkdown(richTextFormat))
        {
            return ToolResult.Json(await this.jira.GetAsync(Path(renderedRequested), cancellationToken));
        }

        return ToolResult.Json(await JiraRichText.GetAsMarkdownAsync(this.jira, Path, renderedRequested, "renderedFields", cancellationToken));
    }

    /// <summary>
    /// Creates an issue.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="issueType">The issue type name or ID.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="description">The description, in Markdown.</param>
    /// <param name="environment">The environment, in Markdown.</param>
    /// <param name="assigneeAccountId">The assignee's account ID.</param>
    /// <param name="priority">The priority name.</param>
    /// <param name="labels">The labels, comma-separated.</param>
    /// <param name="components">The component names, comma-separated.</param>
    /// <param name="dueDate">The due date.</param>
    /// <param name="originalEstimate">The original estimate.</param>
    /// <param name="parentKey">The parent issue key.</param>
    /// <param name="additionalFields">Other fields, as a JSON object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, ID, and URL of the new issue.</returns>
    [McpServerTool(Name = "atlassian_jira_create_issue", OpenWorld = true)]
    [Description("Creates a Jira issue. Call atlassian_jira_get_create_meta first to learn the project's required fields, custom fields, and allowed values. Text fields take Markdown. Mention a user, notifying them, with `@[Display Name]`, or with `@[Display Name](accountid:ID)` when the account ID is known; a bare @name stays plain text. Embed a file already attached to the issue, shown inline for an image or video and as a file card otherwise, with `![name](attachment:ID)` in a paragraph of its own.")]
    public async Task<string> Create(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        [Description("The issue type name, such as Bug, Task, Story, Epic, or Subtask, or its ID.")] string issueType,
        [Description("The summary (title), as plain text.")] string summary,
        [Description("Optional description, in Markdown.")] string? description = null,
        [Description("Optional environment, in Markdown, such as the operating system and version where a bug occurs.")] string? environment = null,
        [Description("Optional account ID of the assignee.")] string? assigneeAccountId = null,
        [Description("Optional priority name, such as High.")] string? priority = null,
        [Description("Optional comma-separated labels. Labels cannot contain spaces.")] string? labels = null,
        [Description("Optional comma-separated component names.")] string? components = null,
        [Description("Optional due date, as YYYY-MM-DD.")] string? dueDate = null,
        [Description("Optional original estimate, such as 2h or 1d 4h.")] string? originalEstimate = null,
        [Description("Optional key of the parent issue: the epic for a story or task, or the issue for a subtask.")] string? parentKey = null,
        [Description("Optional JSON object of other fields, keyed by field ID, such as {\"customfield_10010\": {\"value\": \"Option\"}, \"fixVersions\": [{\"name\": \"1.0\"}]}. Merged over the parameters above. Multi-line text custom fields take Markdown strings.")] string? additionalFields = null,
        CancellationToken cancellationToken = default)
    {
        var fieldValues = new JsonObject
        {
            ["project"] = IdOrKey(projectKey, "key"),
            ["issuetype"] = IdOrKey(issueType, "name"),
            ["summary"] = summary,
        };

        AdfReferences references = await this.jira.ResolveReferencesAsync([description, environment], cancellationToken);
        SetCommonFields(fieldValues, description, environment, assigneeAccountId, priority, labels, dueDate, parentKey, references);

        List<string> componentNames = JsonArguments.SplitList(components);
        if (componentNames.Count > 0)
        {
            fieldValues["components"] = new JsonArray(componentNames.Select(name => (JsonNode)new JsonObject { ["name"] = name }).ToArray());
        }

        if (originalEstimate is not null)
        {
            fieldValues["timetracking"] = new JsonObject { ["originalEstimate"] = originalEstimate };
        }

        await this.MergeAdditionalFieldsAsync(fieldValues, additionalFields, cancellationToken);

        JsonNode? created = await this.jira.SendAsync(HttpMethod.Post, "issue", new JsonObject { ["fields"] = fieldValues }, cancellationToken);
        string key = created?["key"]?.GetValue<string>() ?? string.Empty;

        return ToolResult.Json(new
        {
            key,
            id = created?["id"]?.GetValue<string>(),
            url = this.BrowseUrl(key),
        });
    }

    /// <summary>
    /// Updates the fields of the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="description">The description, in Markdown.</param>
    /// <param name="environment">The environment, in Markdown.</param>
    /// <param name="assigneeAccountId">The assignee's account ID.</param>
    /// <param name="priority">The priority name.</param>
    /// <param name="labels">The labels, comma-separated, replacing the existing ones.</param>
    /// <param name="dueDate">The due date.</param>
    /// <param name="originalEstimate">The original estimate.</param>
    /// <param name="remainingEstimate">The remaining estimate.</param>
    /// <param name="parentKey">The parent issue key.</param>
    /// <param name="additionalFields">Other fields, as a JSON object.</param>
    /// <param name="notifyUsers">A value indicating whether watchers are notified.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_update_issue", Idempotent = true, OpenWorld = true)]
    [Description("Updates the specified Jira issue. Only the fields that are passed change. Text fields take Markdown. Mention a user, notifying them, with `@[Display Name]`, or with `@[Display Name](accountid:ID)` when the account ID is known; a bare @name stays plain text. Embed a file already attached to the issue, shown inline for an image or video and as a file card otherwise, with `![name](attachment:ID)` in a paragraph of its own.")]
    public async Task<string> Update(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Optional new summary, as plain text.")] string? summary = null,
        [Description("Optional new description, in Markdown. Replaces the whole description.")] string? description = null,
        [Description("Optional new environment, in Markdown.")] string? environment = null,
        [Description("Optional account ID of the new assignee.")] string? assigneeAccountId = null,
        [Description("Optional new priority name.")] string? priority = null,
        [Description("Optional comma-separated labels. Replaces all existing labels; use atlassian_jira_add_labels to add instead.")] string? labels = null,
        [Description("Optional new due date, as YYYY-MM-DD.")] string? dueDate = null,
        [Description("Optional new original estimate, such as 2h.")] string? originalEstimate = null,
        [Description("Optional new remaining estimate, such as 1h 30m.")] string? remainingEstimate = null,
        [Description("Optional key of the new parent issue.")] string? parentKey = null,
        [Description("Optional JSON object of other fields, keyed by field ID, such as {\"fixVersions\": [{\"name\": \"1.0\"}]}. Multi-line text custom fields take Markdown strings.")] string? additionalFields = null,
        [Description("Whether watchers are notified of the change. Defaults to true.")] bool notifyUsers = true,
        CancellationToken cancellationToken = default)
    {
        var fieldValues = new JsonObject();
        if (summary is not null)
        {
            fieldValues["summary"] = summary;
        }

        AdfReferences references = await this.jira.ResolveReferencesAsync([description, environment], cancellationToken);
        SetCommonFields(fieldValues, description, environment, assigneeAccountId, priority, labels, dueDate, parentKey, references);

        if (originalEstimate is not null || remainingEstimate is not null)
        {
            var timeTracking = new JsonObject();
            if (originalEstimate is not null)
            {
                timeTracking["originalEstimate"] = originalEstimate;
            }

            if (remainingEstimate is not null)
            {
                timeTracking["remainingEstimate"] = remainingEstimate;
            }

            fieldValues["timetracking"] = timeTracking;
        }

        await this.MergeAdditionalFieldsAsync(fieldValues, additionalFields, cancellationToken);

        if (fieldValues.Count == 0)
        {
            throw new McpException("No fields to update were passed.");
        }

        string path = new QueryString($"issue/{JiraClient.Segment(issueKey)}").Add("notifyUsers", notifyUsers ? null : (bool?)false).ToString();
        await this.jira.SendAsync(HttpMethod.Put, path, new JsonObject { ["fields"] = fieldValues }, cancellationToken);

        return ToolResult.Success($"Updated {issueKey}: {string.Join(", ", fieldValues.Select(pair => pair.Key))}.");
    }

    /// <summary>
    /// Deletes the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="deleteSubtasks">A value indicating whether the subtasks are deleted too.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_issue", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified Jira issue permanently.")]
    public async Task<string> Delete(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("Whether the issue's subtasks are deleted too. An issue with subtasks cannot be deleted otherwise.")] bool deleteSubtasks = false,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"issue/{JiraClient.Segment(issueKey)}").Add("deleteSubtasks", deleteSubtasks ? true : null).ToString();
        await this.jira.SendAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        return ToolResult.Success($"Deleted {issueKey}.");
    }

    /// <summary>
    /// Assigns the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="assigneeAccountId">The assignee's account ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_assign_issue", Idempotent = true, OpenWorld = true)]
    [Description("Assigns the specified Jira issue to a user, to the project's default assignee, or to nobody.")]
    public async Task<string> Assign(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The account ID of the assignee; -1 for the project's default assignee; omit to unassign.")] string? assigneeAccountId = null,
        CancellationToken cancellationToken = default)
    {
        await this.jira.SendAsync(
            HttpMethod.Put,
            $"issue/{JiraClient.Segment(issueKey)}/assignee",
            new JsonObject { ["accountId"] = string.IsNullOrWhiteSpace(assigneeAccountId) ? null : assigneeAccountId.Trim() },
            cancellationToken);

        return ToolResult.Success(string.IsNullOrWhiteSpace(assigneeAccountId)
            ? $"Unassigned {issueKey}."
            : $"Assigned {issueKey} to {assigneeAccountId}.");
    }

    /// <summary>
    /// Gets the metadata needed to create an issue in the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="issueType">The issue type name or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issue types, or the fields of one issue type.</returns>
    [McpServerTool(Name = "atlassian_jira_get_create_meta", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets what is needed to create a Jira issue in the specified project. Without an issue type, lists the project's issue types. With one, lists its fields: ID, name, whether it is required, its type, and up to 25 allowed values.")]
    public async Task<string> GetCreateMeta(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        [Description("Optional issue type name, such as Bug, or ID. Omit to list the project's issue types.")] string? issueType = null,
        CancellationToken cancellationToken = default)
    {
        string project = JiraClient.Segment(projectKey);
        JsonNode? typesResponse = await this.jira.GetAsync($"issue/createmeta/{project}/issuetypes?maxResults=200", cancellationToken);
        List<JsonObject> types = ItemsOf(typesResponse, "issueTypes", "values");

        if (string.IsNullOrWhiteSpace(issueType))
        {
            return ToolResult.Json(new
            {
                projectKey,
                issueTypes = types.Select(type => new
                {
                    id = type["id"]?.GetValue<string>(),
                    name = type["name"]?.GetValue<string>(),
                    subtask = type["subtask"]?.GetValue<bool>(),
                }),
            });
        }

        JsonObject type = types.FirstOrDefault(candidate =>
                string.Equals(candidate["name"]?.GetValue<string>(), issueType.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate["id"]?.GetValue<string>(), issueType.Trim(), StringComparison.Ordinal))
            ?? throw new McpException(
                $"The project {projectKey} has no issue type '{issueType}'. Its issue types are: {string.Join(", ", types.Select(candidate => candidate["name"]?.GetValue<string>()))}.");

        string typeId = type["id"]!.GetValue<string>();
        JsonNode? fieldsResponse = await this.jira.GetAsync($"issue/createmeta/{project}/issuetypes/{JiraClient.Segment(typeId)}?maxResults=200", cancellationToken);

        return ToolResult.Json(new
        {
            projectKey,
            issueType = new { id = typeId, name = type["name"]?.GetValue<string>() },
            fields = ItemsOf(fieldsResponse, "fields", "values").Select(SummarizeField),
        });
    }

    /// <summary>
    /// Summarizes one field of the create metadata, capping its allowed values.
    /// </summary>
    /// <param name="field">The field metadata.</param>
    /// <returns>The summary.</returns>
    internal static JsonObject SummarizeField(JsonObject field)
    {
        const int MaxAllowedValues = 25;

        var summary = new JsonObject
        {
            ["fieldId"] = field["fieldId"]?.DeepClone() ?? field["key"]?.DeepClone(),
            ["name"] = field["name"]?.DeepClone(),
            ["required"] = field["required"]?.DeepClone(),
            ["type"] = field["schema"]?["type"]?.DeepClone(),
            ["items"] = field["schema"]?["items"]?.DeepClone(),
            ["custom"] = field["schema"]?["custom"]?.DeepClone(),
            ["hasDefaultValue"] = field["hasDefaultValue"]?.DeepClone(),
        };

        if (field["allowedValues"] is JsonArray allowed && allowed.Count > 0)
        {
            summary["allowedValues"] = new JsonArray(allowed
                .OfType<JsonObject>()
                .Take(MaxAllowedValues)
                .Select(value => (JsonNode)new JsonObject
                {
                    ["id"] = value["id"]?.DeepClone(),
                    ["name"] = value["name"]?.DeepClone(),
                    ["value"] = value["value"]?.DeepClone(),
                })
                .ToArray());

            if (allowed.Count > MaxAllowedValues)
            {
                summary["allowedValuesTruncated"] = allowed.Count - MaxAllowedValues;
            }
        }

        return summary;
    }

    /// <summary>
    /// Converts the rich text in an API response to the requested format.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="richTextFormat">markdown or adf.</param>
    /// <returns>The converted response.</returns>
    /// <exception cref="McpException">The format is not known.</exception>
    internal static JsonNode? FormatRichText(JsonNode? response, string richTextFormat)
        => JiraRichText.IsMarkdown(richTextFormat) ? AdfToMarkdown.ConvertDocuments(response) : response;

    private static JsonObject IdOrKey(string value, string nonNumericProperty)
    {
        string trimmed = value.Trim();
        return trimmed.All(char.IsAsciiDigit)
            ? new JsonObject { ["id"] = trimmed }
            : new JsonObject { [nonNumericProperty] = trimmed };
    }

    private static void SetCommonFields(
        JsonObject fieldValues,
        string? description,
        string? environment,
        string? assigneeAccountId,
        string? priority,
        string? labels,
        string? dueDate,
        string? parentKey,
        AdfReferences references)
    {
        if (description is not null)
        {
            fieldValues["description"] = JiraClient.ToAdf(description, references);
        }

        if (environment is not null)
        {
            fieldValues["environment"] = JiraClient.ToAdf(environment, references);
        }

        if (!string.IsNullOrWhiteSpace(assigneeAccountId))
        {
            fieldValues["assignee"] = new JsonObject { ["accountId"] = assigneeAccountId.Trim() };
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            fieldValues["priority"] = IdOrKey(priority, "name");
        }

        if (labels is not null)
        {
            fieldValues["labels"] = new JsonArray(JsonArguments.SplitList(labels).Select(label => (JsonNode)label).ToArray());
        }

        if (!string.IsNullOrWhiteSpace(dueDate))
        {
            fieldValues["duedate"] = dueDate.Trim();
        }

        if (!string.IsNullOrWhiteSpace(parentKey))
        {
            fieldValues["parent"] = IdOrKey(parentKey, "key");
        }
    }

    private static List<JsonObject> ItemsOf(JsonNode? response, params string[] names)
    {
        foreach (string name in names)
        {
            if (response?[name] is JsonArray items)
            {
                return items.OfType<JsonObject>().ToList();
            }
        }

        return [];
    }

    private async Task MergeAdditionalFieldsAsync(JsonObject fieldValues, string? additionalFields, CancellationToken cancellationToken)
    {
        JsonObject? extra = JsonArguments.ParseObject(additionalFields, nameof(additionalFields));
        if (extra is null)
        {
            return;
        }

        // Only look up which fields hold rich text when there is a string that might need converting.
        if (extra.Any(pair => pair.Value is JsonValue value && value.TryGetValue(out string? _)))
        {
            IReadOnlySet<string> richText = await this.metadata.GetRichTextFieldIdsAsync(this.jira, cancellationToken);
            IEnumerable<string?> markdownTexts = extra
                .Where(pair => richText.Contains(pair.Key) && pair.Value is JsonValue value && value.TryGetValue(out string? _))
                .Select(pair => pair.Value!.GetValue<string>());
            AdfReferences references = await this.jira.ResolveReferencesAsync(markdownTexts, cancellationToken);
            MarkdownToAdf.ConvertRichTextFields(extra, richText.Contains, references);
        }

        foreach (string name in extra.Select(pair => pair.Key).ToList())
        {
            JsonNode? value = extra[name];
            extra.Remove(name);
            fieldValues[name] = value;
        }
    }

    private string BrowseUrl(string key) => new Uri(this.jira.Http.SiteUrl, $"browse/{key}").ToString();
}
