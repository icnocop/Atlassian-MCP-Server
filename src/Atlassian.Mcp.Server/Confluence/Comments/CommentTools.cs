// <copyright file="CommentTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Confluence.Comments;

/// <summary>
/// Tools for the footer comments of Confluence pages.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.Confluence)]
public sealed class CommentTools
{
    private readonly ConfluenceClient confluence;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentTools"/> class.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    public CommentTools(ConfluenceClient confluence)
    {
        this.confluence = confluence;
    }

    /// <summary>
    /// Gets the footer comments of the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="limit">The largest number of comments to return.</param>
    /// <param name="cursor">The cursor of the page of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of comments.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_comments", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the footer comments of the specified Confluence page, with their bodies in Markdown.")]
    public async Task<string> GetAll(
        [Description("The page ID.")] string pageId,
        [Description("Optional largest number of comments to return, up to 250. Defaults to 25.")] int limit = 25,
        [Description("Optional cursor from the previous result, to get the next page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"pages/{Uri.EscapeDataString(pageId.Trim())}/footer-comments")
            .Add("body-format", ConfluenceContent.AdfRepresentation)
            .Add("limit", Math.Clamp(limit, 1, 250))
            .Add("cursor", cursor)
            .ToString();

        JsonNode? response = await this.confluence.GetAsync(path, cancellationToken);

        var comments = (response?["results"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(comment => (JsonNode)new JsonObject
            {
                ["id"] = comment["id"]?.DeepClone(),
                ["authorId"] = comment["version"]?["authorId"]?.DeepClone(),
                ["createdAt"] = comment["version"]?["createdAt"]?.DeepClone(),
                ["body"] = AdfToMarkdown.Convert(ConfluenceContent.ReadAdf(comment)),
            })
            .ToArray();

        return ToolResult.Json(new JsonObject
        {
            ["comments"] = new JsonArray(comments),
            ["nextCursor"] = ConfluenceContent.NextCursor(response),
        });
    }

    /// <summary>
    /// Adds a footer comment to the specified page, or a reply to a comment.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="body">The comment, in Markdown.</param>
    /// <param name="parentCommentId">The ID of the comment to reply to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new comment.</returns>
    [McpServerTool(Name = "atlassian_confluence_add_comment", OpenWorld = true)]
    [Description("Adds a footer comment to the specified Confluence page, or a reply to one of its comments. The comment is published immediately. Mention a user, notifying them, with `@[Display Name]`, or with `@[Display Name](accountid:ID)` when the account ID is known; a bare @name stays plain text.")]
    public async Task<string> Add(
        [Description("The page ID. Ignored when parentCommentId is given.")] string pageId,
        [Description("The comment, in Markdown.")] string body,
        [Description("Optional ID of the comment to reply to.")] string? parentCommentId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new JsonObject { ["body"] = await ConfluenceContent.ToRequestBodyAsync(this.confluence, body, "markdown", cancellationToken) };
        if (string.IsNullOrWhiteSpace(parentCommentId))
        {
            request["pageId"] = pageId.Trim();
        }
        else
        {
            request["parentCommentId"] = parentCommentId.Trim();
        }

        JsonNode? comment = await this.confluence.SendAsync(HttpMethod.Post, "footer-comments", request, cancellationToken);
        return ToolResult.Json(new JsonObject
        {
            ["id"] = comment?["id"]?.DeepClone(),
            ["message"] = "Added the comment.",
        });
    }
}
