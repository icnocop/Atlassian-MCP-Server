// <copyright file="SearchTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Jira.Issues;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Search;

/// <summary>
/// Tools for finding Jira issues with JQL.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraSearch)]
public sealed class SearchTools
{
    /// <summary>The fields returned when the caller does not choose any: enough to triage a list of issues.</summary>
    internal const string DefaultFields = "summary,status,issuetype,priority,assignee,reporter,created,updated,resolution,labels,parent";

    /// <summary>The largest page that the search endpoint returns.</summary>
    internal const int MaxPageSize = 100;

    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public SearchTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Searches for issues with JQL.
    /// </summary>
    /// <param name="jql">The JQL query.</param>
    /// <param name="fields">The fields to return.</param>
    /// <param name="maxResults">The largest number of issues to return.</param>
    /// <param name="nextPageToken">The token of the page to return.</param>
    /// <param name="includeApproximateTotal">A value indicating whether to count the matching issues.</param>
    /// <param name="richTextFormat">The format for rich text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of issues.</returns>
    [McpServerTool(Name = "atlassian_jira_search_issues", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Searches for Jira issues with JQL, such as: project = PROJ AND text ~ \"login error\" ORDER BY updated DESC. Returns one page of issues; pass nextPageToken from the result to get the next page.")]
    public async Task<string> Issues(
        [Description("The JQL query. A query must be bounded (for example by project); an unbounded query such as ORDER BY created alone is rejected.")] string jql,
        [Description("Optional comma-separated fields to return. Defaults to summary, status, issue type, priority, assignee, reporter, dates, resolution, labels, and parent. Use *navigable for all common fields.")] string? fields = null,
        [Description("Optional largest number of issues to return, up to 100. Defaults to 50.")] int maxResults = 50,
        [Description("Optional token from the previous result, to get the next page.")] string? nextPageToken = null,
        [Description("Whether to also count all matching issues. The count is approximate and costs an extra request.")] bool includeApproximateTotal = false,
        [Description("The format for rich text such as descriptions: markdown (default) or adf.")] string richTextFormat = "markdown",
        CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["jql"] = jql,
            ["fields"] = new JsonArray(JsonArguments.SplitList(fields ?? DefaultFields).Select(field => (JsonNode)field).ToArray()),
            ["maxResults"] = Math.Clamp(maxResults, 1, MaxPageSize),
        };

        if (!string.IsNullOrWhiteSpace(nextPageToken))
        {
            body["nextPageToken"] = nextPageToken.Trim();
        }

        JsonNode? page = await this.jira.SendAsync(HttpMethod.Post, "search/jql", body, cancellationToken);

        var result = new JsonObject
        {
            ["issues"] = IssueTools.FormatRichText(page?["issues"], richTextFormat)?.DeepClone() ?? new JsonArray(),
            ["isLast"] = page?["isLast"]?.DeepClone() ?? (page?["nextPageToken"] is null),
            ["nextPageToken"] = page?["nextPageToken"]?.DeepClone(),
        };

        if (includeApproximateTotal)
        {
            JsonNode? count = await this.jira.SendAsync(HttpMethod.Post, "search/approximate-count", new JsonObject { ["jql"] = jql }, cancellationToken);
            result["approximateTotal"] = count?["count"]?.DeepClone();
        }

        return ToolResult.Json(result);
    }

    /// <summary>
    /// Checks JQL for errors.
    /// </summary>
    /// <param name="jql">The JQL query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parse result, with any errors.</returns>
    [McpServerTool(Name = "atlassian_jira_validate_jql", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Checks a JQL query for syntax errors and unknown fields, functions, or values, without running it.")]
    public async Task<string> Validate(
        [Description("The JQL query to check.")] string jql,
        CancellationToken cancellationToken = default)
    {
        JsonNode? result = await this.jira.SendAsync(
            HttpMethod.Post,
            "jql/parse?validation=strict",
            new JsonObject { ["queries"] = new JsonArray(jql) },
            cancellationToken);

        JsonNode? query = (result?["queries"] as JsonArray)?.FirstOrDefault();
        JsonArray errors = query?["errors"] as JsonArray ?? [];

        return ToolResult.Json(new JsonObject
        {
            ["valid"] = errors.Count == 0,
            ["errors"] = errors.DeepClone(),
            ["warnings"] = query?["warnings"]?.DeepClone(),
        });
    }

    /// <summary>
    /// Gets suggested values for a JQL field.
    /// </summary>
    /// <param name="fieldName">The field name.</param>
    /// <param name="fieldValue">The start of the value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The suggestions.</returns>
    [McpServerTool(Name = "atlassian_jira_get_jql_suggestions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets suggested values for a field in a JQL query, such as the project names, statuses, or versions that start with some text.")]
    public async Task<string> GetSuggestions(
        [Description("The JQL field name, such as project, status, fixVersion, or assignee.")] string fieldName,
        [Description("Optional start of the value, to narrow the suggestions.")] string? fieldValue = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("jql/autocompletedata/suggestions")
            .Add("fieldName", fieldName)
            .Add("fieldValue", fieldValue)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }
}
