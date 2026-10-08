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

namespace Atlassian.Mcp.Server.Confluence.Labels;

/// <summary>
/// Tools for the labels of Confluence pages.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.Confluence)]
public sealed class LabelTools
{
    private readonly ConfluenceClient confluence;

    /// <summary>
    /// Initializes a new instance of the <see cref="LabelTools"/> class.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    public LabelTools(ConfluenceClient confluence)
    {
        this.confluence = confluence;
    }

    /// <summary>
    /// Gets the labels of the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The labels.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_labels", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the labels of the specified Confluence page.")]
    public async Task<string> GetAll(
        [Description("The page ID.")] string pageId,
        CancellationToken cancellationToken = default)
    {
        JsonNode? response = await this.confluence.GetAsync($"pages/{Uri.EscapeDataString(pageId.Trim())}/labels?limit=250", cancellationToken);
        return ToolResult.Json(new JsonArray((response?["results"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(label => (JsonNode?)label["name"]?.DeepClone())
            .ToArray()));
    }

    /// <summary>
    /// Adds labels to the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="labels">The labels, comma-separated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_confluence_add_labels", Idempotent = true, OpenWorld = true)]
    [Description("Adds labels to the specified Confluence page, keeping its existing labels.")]
    public async Task<string> Add(
        [Description("The page ID.")] string pageId,
        [Description("Comma-separated labels to add. Labels cannot contain spaces.")] string labels,
        CancellationToken cancellationToken = default)
    {
        List<string> names = JsonArguments.SplitList(labels);
        if (names.Count == 0)
        {
            throw new McpException("The labels parameter must name at least one label.");
        }

        var body = new JsonArray(names.Select(name => (JsonNode)new JsonObject { ["prefix"] = "global", ["name"] = name }).ToArray());
        await this.confluence.SendV1Async(HttpMethod.Post, $"content/{Uri.EscapeDataString(pageId.Trim())}/label", body, cancellationToken);
        return ToolResult.Success($"Added labels to page {pageId}: {string.Join(", ", names)}.");
    }

    /// <summary>
    /// Removes a label from the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="label">The label.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_confluence_remove_label", Idempotent = true, OpenWorld = true)]
    [Description("Removes a label from the specified Confluence page.")]
    public async Task<string> Remove(
        [Description("The page ID.")] string pageId,
        [Description("The label to remove.")] string label,
        CancellationToken cancellationToken = default)
    {
        await this.confluence.SendV1Async(
            HttpMethod.Delete,
            $"content/{Uri.EscapeDataString(pageId.Trim())}/label/{Uri.EscapeDataString(label.Trim())}",
            body: null,
            cancellationToken);

        return ToolResult.Success($"Removed label {label} from page {pageId}.");
    }
}
