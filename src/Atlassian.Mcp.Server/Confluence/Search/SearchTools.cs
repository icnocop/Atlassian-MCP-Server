// <copyright file="SearchTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Confluence.Search;

/// <summary>
/// Tools for finding Confluence content with CQL.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.Confluence)]
public sealed class SearchTools
{
    private readonly ConfluenceClient confluence;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchTools"/> class.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    public SearchTools(ConfluenceClient confluence)
    {
        this.confluence = confluence;
    }

    /// <summary>
    /// Searches for content with CQL.
    /// </summary>
    /// <param name="cql">The CQL query.</param>
    /// <param name="limit">The largest number of results to return.</param>
    /// <param name="cursor">The cursor of the page of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of results.</returns>
    [McpServerTool(Name = "atlassian_confluence_search", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Searches Confluence with CQL, such as: type = page AND space = DOCS AND text ~ \"release notes\" ORDER BY lastmodified DESC. Returns the ID, type, title, space, URL, last-modified date, and an excerpt of each result.")]
    public async Task<string> Content(
        [Description("The CQL query.")] string cql,
        [Description("Optional largest number of results to return, up to 100. Defaults to 25.")] int limit = 25,
        [Description("Optional cursor from the previous result, to get the next page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("search")
            .Add("cql", cql)
            .Add("limit", Math.Clamp(limit, 1, 100))
            .Add("cursor", cursor)
            .ToString();

        JsonNode? response = await this.confluence.GetV1Async(path, cancellationToken);

        var results = (response?["results"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(result => new JsonObject
            {
                ["id"] = result["content"]?["id"]?.DeepClone(),
                ["type"] = result["content"]?["type"]?.DeepClone() ?? result["entityType"]?.DeepClone(),
                ["title"] = result["content"]?["title"]?.DeepClone() ?? result["title"]?.DeepClone(),
                ["space"] = result["resultGlobalContainer"]?["title"]?.DeepClone(),
                ["url"] = result["url"] is JsonValue url ? this.confluence.WebUrl(url.GetValue<string>()).ToString() : null,
                ["lastModified"] = result["lastModified"]?.DeepClone(),
                ["excerpt"] = result["excerpt"]?.DeepClone(),
            })
            .ToArray();

        return ToolResult.Json(new JsonObject
        {
            ["results"] = new JsonArray(results),
            ["totalSize"] = response?["totalSize"]?.DeepClone(),
            ["nextCursor"] = ConfluenceContent.NextCursor(response),
        });
    }
}
