// <copyright file="LinkTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Links;

/// <summary>
/// Tools for the links between Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraLinks)]
public sealed class LinkTools
{
    private readonly JiraClient jira;
    private readonly JiraMetadataCache metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    /// <param name="metadata">The cache of site metadata.</param>
    public LinkTools(JiraClient jira, JiraMetadataCache metadata)
    {
        this.jira = jira;
        this.metadata = metadata;
    }

    /// <summary>
    /// Gets the issue link types that the site defines.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The link types.</returns>
    [McpServerTool(Name = "atlassian_jira_get_link_types", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the issue link types that the Jira site defines, with their names and directional phrases (for example Blocks: \"blocks\" / \"is blocked by\").")]
    public async Task<string> GetTypes(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JsonObject> types = await this.metadata.GetLinkTypesAsync(this.jira, cancellationToken);
        return ToolResult.Json(new JsonArray(types.Select(type => (JsonNode?)type.DeepClone()).ToArray()));
    }

    /// <summary>
    /// Gets the links of the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The issue links.</returns>
    [McpServerTool(Name = "atlassian_jira_get_issue_links", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the links of the specified Jira issue: each link's ID, type, and the linked issue.")]
    public async Task<string> GetAll(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        CancellationToken cancellationToken = default)
    {
        JsonNode? issue = await this.jira.GetAsync($"issue/{JiraClient.Segment(issueKey)}?fields=issuelinks", cancellationToken);
        return ToolResult.Json(issue?["fields"]?["issuelinks"] ?? new JsonArray());
    }

    /// <summary>
    /// Links two issues.
    /// </summary>
    /// <param name="linkType">The link type name, or either of its directional phrases.</param>
    /// <param name="inwardIssue">The issue on the inward side.</param>
    /// <param name="outwardIssue">The issue on the outward side.</param>
    /// <param name="comment">An optional comment, as Markdown.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation that spells the link out in both directions.</returns>
    [McpServerTool(Name = "atlassian_jira_link_issues", OpenWorld = true)]
    [Description("Links two Jira issues. The link reads \"inwardIssue <outward phrase> outwardIssue\": with type Blocks, inwardIssue blocks outwardIssue. The direction is never swapped; to reverse a link, swap the two issue keys. The confirmation spells the link out in both directions.")]
    public async Task<string> Create(
        [Description("The link type name (such as Blocks, Relates, Duplicate) or either of its directional phrases (such as \"is blocked by\"). See atlassian_jira_get_link_types.")] string linkType,
        [Description("The issue on the inward side: the subject of the outward phrase, such as the blocker in \"A blocks B\".")] string inwardIssue,
        [Description("The issue on the outward side: the object of the outward phrase, such as the blocked issue in \"A blocks B\".")] string outwardIssue,
        [Description("An optional comment to add to the outward issue with the link, as Markdown.")] string? comment = null,
        CancellationToken cancellationToken = default)
    {
        JsonObject type = await this.metadata.FindLinkTypeAsync(this.jira, linkType, cancellationToken)
            ?? throw new McpException(await this.UnknownTypeMessageAsync(linkType, cancellationToken));

        string inward = inwardIssue.Trim();
        string outward = outwardIssue.Trim();

        var request = new JsonObject
        {
            ["type"] = new JsonObject { ["name"] = type["name"]?.GetValue<string>() },
            ["inwardIssue"] = new JsonObject { ["key"] = inward },
            ["outwardIssue"] = new JsonObject { ["key"] = outward },
        };

        if (!string.IsNullOrWhiteSpace(comment))
        {
            request["comment"] = new JsonObject { ["body"] = JiraClient.ToAdf(comment) };
        }

        await this.jira.SendAsync(HttpMethod.Post, "issueLink", request, cancellationToken);
        return ToolResult.Success(DescribeLink(type, inward, outward));
    }

    /// <summary>
    /// Deletes the specified issue link.
    /// </summary>
    /// <param name="linkId">The link ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_issue_link", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified link between two Jira issues. The issues themselves are not changed.")]
    public async Task<string> Delete(
        [Description("The link ID, from atlassian_jira_get_issue_links.")] string linkId,
        CancellationToken cancellationToken = default)
    {
        await this.jira.SendAsync(HttpMethod.Delete, $"issueLink/{JiraClient.Segment(linkId)}", body: null, cancellationToken);
        return ToolResult.Success($"Deleted issue link {linkId}.");
    }

    /// <summary>
    /// Spells out a link in both directions, such as
    /// "Created link: PROJ-1 blocks PROJ-2 (PROJ-2 is blocked by PROJ-1).".
    /// </summary>
    /// <param name="type">The link type.</param>
    /// <param name="inward">The inward issue key.</param>
    /// <param name="outward">The outward issue key.</param>
    /// <returns>The description.</returns>
    internal static string DescribeLink(JsonObject type, string inward, string outward)
    {
        string name = type["name"]?.GetValue<string>() ?? "link";
        string outwardPhrase = type["outward"]?.GetValue<string>() ?? name;
        string inwardPhrase = type["inward"]?.GetValue<string>() ?? name;

        return $"Created link: {inward} {outwardPhrase} {outward} ({outward} {inwardPhrase} {inward}).";
    }

    private async Task<string> UnknownTypeMessageAsync(string linkType, CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonObject> types = await this.metadata.GetLinkTypesAsync(this.jira, cancellationToken);
        IEnumerable<string> names = types
            .Select(type => type["name"]?.GetValue<string>())
            .OfType<string>();

        return $"The site has no link type named '{linkType}'. Available link types: {string.Join(", ", names)}.";
    }
}
