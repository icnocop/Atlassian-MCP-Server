// <copyright file="SpaceTools.cs" company="icnocop">
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

namespace Atlassian.Mcp.Server.Confluence.Spaces;

/// <summary>
/// Tools for Confluence spaces.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.Confluence)]
public sealed class SpaceTools
{
    private readonly ConfluenceClient confluence;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceTools"/> class.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    public SpaceTools(ConfluenceClient confluence)
    {
        this.confluence = confluence;
    }

    /// <summary>
    /// Gets the spaces that you can see.
    /// </summary>
    /// <param name="keys">The space keys to return.</param>
    /// <param name="type">The space type.</param>
    /// <param name="limit">The largest number of spaces to return.</param>
    /// <param name="cursor">The cursor of the page to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of spaces.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_spaces", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the Confluence spaces that you can see: ID, key, name, type, and home page ID.")]
    public async Task<string> GetAll(
        [Description("Optional comma-separated space keys to return.")] string? keys = null,
        [Description("Optional space type: global, personal, or collaboration.")] string? type = null,
        [Description("Optional largest number of spaces to return, up to 250. Defaults to 25.")] int limit = 25,
        [Description("Optional cursor from the previous result, to get the next page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("spaces")
            .AddEach("keys", JsonArguments.SplitList(keys))
            .Add("type", type)
            .Add("limit", Math.Clamp(limit, 1, 250))
            .Add("cursor", cursor)
            .ToString();

        JsonNode? response = await this.confluence.GetAsync(path, cancellationToken);
        return ToolResult.Json(new JsonObject
        {
            ["spaces"] = response?["results"]?.DeepClone(),
            ["nextCursor"] = ConfluenceContent.NextCursor(response),
        });
    }

    /// <summary>
    /// Gets the specified space.
    /// </summary>
    /// <param name="spaceKey">The space key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The space.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_space", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified Confluence space, including its description and home page ID.")]
    public async Task<string> Get(
        [Description("The space key, such as DOCS or ~username for a personal space, or the space ID.")] string spaceKey,
        CancellationToken cancellationToken = default)
    {
        string id = await ResolveIdAsync(this.confluence, spaceKey, cancellationToken);
        return ToolResult.Json(await this.confluence.GetAsync($"spaces/{Uri.EscapeDataString(id)}?description-format=plain", cancellationToken));
    }

    /// <summary>
    /// Resolves a space key to its ID. A numeric value is taken as an ID already.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    /// <param name="spaceKeyOrId">The space key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The space ID.</returns>
    /// <exception cref="McpException">No space has that key.</exception>
    internal static async Task<string> ResolveIdAsync(ConfluenceClient confluence, string spaceKeyOrId, CancellationToken cancellationToken)
    {
        string value = spaceKeyOrId.Trim();
        if (value.Length > 0 && value.All(char.IsAsciiDigit))
        {
            return value;
        }

        JsonNode? response = await confluence.GetAsync(new QueryString("spaces").Add("keys", value).ToString(), cancellationToken);

        // The API ignores a key it does not recognize instead of rejecting it, and can then return
        // other spaces, so only an exact match is accepted.
        JsonObject? space = (response?["results"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .FirstOrDefault(candidate => string.Equals(candidate["key"]?.GetValue<string>(), value, StringComparison.OrdinalIgnoreCase));

        return space?["id"]?.GetValue<string>()
            ?? throw new McpException($"No Confluence space has the key '{value}', or you cannot see it. Use atlassian_confluence_get_spaces to list spaces.");
    }
}
