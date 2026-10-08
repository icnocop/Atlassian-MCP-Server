// <copyright file="PageTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Net;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Confluence.Spaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Confluence.Pages;

/// <summary>
/// Tools for reading and writing Confluence pages. Writes are saved as drafts by default, so a
/// person can preview them in Confluence before they are published.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.Confluence)]
public sealed class PageTools
{
    /// <summary>The version message used when the caller does not give one.</summary>
    internal const string DefaultVersionMessage = "Updated via Atlassian MCP Server";

    private readonly ConfluenceClient confluence;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageTools"/> class.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    public PageTools(ConfluenceClient confluence)
    {
        this.confluence = confluence;
    }

    /// <summary>
    /// Gets the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="bodyFormat">The body format.</param>
    /// <param name="includeDraft">A value indicating whether to return the unpublished draft when there is one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_page", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified Confluence page: title, status, space, parent, version, links, and body. The body is Markdown by default; content that Markdown cannot represent (macros, smart links, media, panels) appears as <!-- adf:... --> placeholders and is counted in contentMarkdownCannotRepresent. To edit such a page without losing that content, use atlassian_confluence_update_page_section, or read and write with bodyFormat adf.")]
    public async Task<string> Get(
        [Description("The page ID, the number in the page URL.")] string pageId,
        [Description("The body format: markdown (default), adf, or storage.")] string bodyFormat = "markdown",
        [Description("Whether to return the unpublished draft of the page, when there is one, instead of the published version.")] bool includeDraft = false,
        CancellationToken cancellationToken = default)
    {
        string format = ConfluenceContent.NormalizeFormat(bodyFormat);

        JsonNode page = includeDraft
            ? await this.TryGetDraftAsync(pageId, format, cancellationToken) ?? await this.GetPublishedOrDraftAsync(pageId, format, cancellationToken)
            : await this.GetPublishedOrDraftAsync(pageId, format, cancellationToken);

        return ToolResult.Json(this.View(page, format));
    }

    /// <summary>
    /// Gets the child pages of the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="limit">The largest number of pages to return.</param>
    /// <param name="cursor">The cursor of the page of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of child pages.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_page_children", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the child pages of the specified Confluence page: ID, title, and status.")]
    public async Task<string> GetChildren(
        [Description("The page ID.")] string pageId,
        [Description("Optional largest number of pages to return, up to 250. Defaults to 25.")] int limit = 25,
        [Description("Optional cursor from the previous result, to get the next page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"pages/{Segment(pageId)}/children")
            .Add("limit", Math.Clamp(limit, 1, 250))
            .Add("cursor", cursor)
            .ToString();

        JsonNode? response = await this.confluence.GetAsync(path, cancellationToken);
        return ToolResult.Json(new JsonObject
        {
            ["pages"] = response?["results"]?.DeepClone(),
            ["nextCursor"] = ConfluenceContent.NextCursor(response),
        });
    }

    /// <summary>
    /// Gets the version history of the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="limit">The largest number of versions to return.</param>
    /// <param name="cursor">The cursor of the page of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of versions.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_page_versions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the version history of the specified Confluence page, newest first: version number, author, date, and message.")]
    public async Task<string> GetVersions(
        [Description("The page ID.")] string pageId,
        [Description("Optional largest number of versions to return, up to 250. Defaults to 25.")] int limit = 25,
        [Description("Optional cursor from the previous result, to get the next page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"pages/{Segment(pageId)}/versions")
            .Add("limit", Math.Clamp(limit, 1, 250))
            .Add("cursor", cursor)
            .ToString();

        JsonNode? response = await this.confluence.GetAsync(path, cancellationToken);
        return ToolResult.Json(new JsonObject
        {
            ["versions"] = response?["results"]?.DeepClone(),
            ["nextCursor"] = ConfluenceContent.NextCursor(response),
        });
    }

    /// <summary>
    /// Creates a page, as a private draft by default.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="body">The body.</param>
    /// <param name="spaceKey">The space key or ID.</param>
    /// <param name="parentId">The parent page ID.</param>
    /// <param name="bodyFormat">The body format.</param>
    /// <param name="publish">A value indicating whether to publish the page instead of saving a draft.</param>
    /// <param name="restrictToMe">A value indicating whether only the creator can see and edit the page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new page, with the URL to preview it.</returns>
    [McpServerTool(Name = "atlassian_confluence_create_page", OpenWorld = true)]
    [Description("Creates a Confluence page. By default it is saved as a private draft that only you can see, so you can preview it in Confluence and publish it there or with atlassian_confluence_publish_page. Only publish directly when the user explicitly asks to.")]
    public async Task<string> Create(
        [Description("The page title.")] string title,
        [Description("The page body, in the format given by bodyFormat.")] string body,
        [Description("The space key, such as DOCS or ~username, or the space ID. Optional when parentId is given.")] string? spaceKey = null,
        [Description("Optional ID of the parent page. Defaults to the top level of the space.")] string? parentId = null,
        [Description("The body format: markdown (default), adf, or storage.")] string bodyFormat = "markdown",
        [Description("Whether to publish the page immediately instead of saving a draft. Defaults to false; only set it when the user explicitly asks to publish.")] bool publish = false,
        [Description("Whether only you can see and edit the page. Defaults to true for a draft and false for a published page.")] bool? restrictToMe = null,
        CancellationToken cancellationToken = default)
    {
        string spaceId = await this.ResolveSpaceIdAsync(spaceKey, parentId, cancellationToken);
        bool isPrivate = restrictToMe ?? !publish;

        var request = new JsonObject
        {
            ["spaceId"] = spaceId,
            ["status"] = publish ? "current" : "draft",
            ["title"] = title,
            ["body"] = ConfluenceContent.ToRequestBody(body, bodyFormat),
        };

        if (!string.IsNullOrWhiteSpace(parentId))
        {
            request["parentId"] = parentId.Trim();
        }

        string path = new QueryString("pages").Add("private", isPrivate ? true : null).ToString();
        JsonNode? page = await this.confluence.SendAsync(HttpMethod.Post, path, request, cancellationToken);

        return ToolResult.Json(new JsonObject
        {
            ["id"] = page?["id"]?.DeepClone(),
            ["title"] = page?["title"]?.DeepClone(),
            ["status"] = page?["status"]?.DeepClone(),
            ["private"] = isPrivate,
            ["url"] = this.Link(page, "webui"),
            ["message"] = publish
                ? "Published the page."
                : "Saved the page as a draft. It is not published. Open the URL to preview it, then publish it in Confluence or ask to publish it.",
        });
    }

    /// <summary>
    /// Updates the specified page, as a draft by default.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="body">The new body.</param>
    /// <param name="title">The new title.</param>
    /// <param name="bodyFormat">The body format.</param>
    /// <param name="versionMessage">The version message.</param>
    /// <param name="publish">A value indicating whether to publish the change instead of saving a draft.</param>
    /// <param name="allowContentLoss">A value indicating whether a Markdown body may replace content that Markdown cannot represent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation, with the URL to preview the change.</returns>
    [McpServerTool(Name = "atlassian_confluence_update_page", OpenWorld = true)]
    [Description("Replaces the body and/or title of the specified Confluence page. By default the change is saved as a draft and the published page does not change, so the user can preview it in Confluence. A Markdown body is refused when the page has content Markdown cannot represent (macros, smart links, media, panels); use atlassian_confluence_update_page_section, or bodyFormat adf, instead.")]
    public async Task<string> Update(
        [Description("The page ID.")] string pageId,
        [Description("Optional new body, in the format given by bodyFormat. Replaces the whole body. Omit to change only the title.")] string? body = null,
        [Description("Optional new title.")] string? title = null,
        [Description("The body format: markdown (default), adf, or storage.")] string bodyFormat = "markdown",
        [Description("Optional version message, shown in the page history.")] string? versionMessage = null,
        [Description("Whether to publish the change immediately instead of saving a draft. Defaults to false; only set it when the user explicitly asks to publish.")] bool publish = false,
        [Description("Whether to allow a Markdown body to replace content that Markdown cannot represent, losing it. Defaults to false; only set it when the user accepts the loss.")] bool allowContentLoss = false,
        CancellationToken cancellationToken = default)
    {
        if (body is null && string.IsNullOrWhiteSpace(title))
        {
            throw new McpException("Pass a new body, a new title, or both.");
        }

        string format = ConfluenceContent.NormalizeFormat(bodyFormat);
        JsonNode? published = await this.TryGetPublishedAsync(pageId, cancellationToken);
        JsonNode? draft = await this.TryGetDraftAsync(pageId, "adf", cancellationToken);
        JsonNode current = published ?? draft ?? throw new McpException($"Page {pageId} was not found, or you cannot see it.");

        JsonObject requestBody;
        if (body is null)
        {
            requestBody = ConfluenceContent.ToRequestBody(ConfluenceContent.ReadAdf(draft ?? current));
        }
        else
        {
            if (format == "markdown" && !allowContentLoss)
            {
                IReadOnlyDictionary<string, int> lossy = AdfInspector.FindLossyContent(ConfluenceContent.ReadAdf(current));
                if (lossy.Count > 0)
                {
                    throw new McpException(
                        $"Page {pageId} has content that Markdown cannot represent ({AdfInspector.Describe(lossy)}), so replacing its body with Markdown would lose it. "
                        + "Use atlassian_confluence_update_page_section to change one section, or get the page with bodyFormat adf and update it with bodyFormat adf. "
                        + "Set allowContentLoss only if the user accepts losing that content.");
                }
            }

            requestBody = ConfluenceContent.ToRequestBody(body, format);
        }

        string newTitle = string.IsNullOrWhiteSpace(title) ? (draft ?? current)["title"]!.GetValue<string>() : title.Trim();
        return await this.WriteAsync(pageId, newTitle, requestBody, versionMessage, publish, published, cancellationToken);
    }

    /// <summary>
    /// Replaces one section of the specified page, as a draft by default.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="heading">The heading of the section.</param>
    /// <param name="body">The new content of the section, in Markdown.</param>
    /// <param name="versionMessage">The version message.</param>
    /// <param name="publish">A value indicating whether to publish the change instead of saving a draft.</param>
    /// <param name="allowContentLoss">A value indicating whether the section may lose content that Markdown cannot represent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation, with the URL to preview the change.</returns>
    [McpServerTool(Name = "atlassian_confluence_update_page_section", OpenWorld = true)]
    [Description("Replaces the content under one heading of the specified Confluence page with Markdown, keeping the rest of the page (including macros and smart links) exactly as it is. The section runs to the next heading of the same or a higher level. By default the change is saved as a draft, building on any existing draft, and the published page does not change.")]
    public async Task<string> UpdateSection(
        [Description("The page ID.")] string pageId,
        [Description("The text of the heading that starts the section, such as Installation. Not the page title.")] string heading,
        [Description("The new content of the section, in Markdown. Start it with the same heading to rename the heading or change its level; otherwise the heading is kept.")] string body,
        [Description("Optional version message, shown in the page history.")] string? versionMessage = null,
        [Description("Whether to publish the change immediately instead of saving a draft. Defaults to false; only set it when the user explicitly asks to publish.")] bool publish = false,
        [Description("Whether to allow replacing content in the section that Markdown cannot represent. Defaults to false.")] bool allowContentLoss = false,
        CancellationToken cancellationToken = default)
    {
        JsonNode? published = await this.TryGetPublishedAsync(pageId, cancellationToken);
        JsonNode? draft = await this.TryGetDraftAsync(pageId, "adf", cancellationToken);
        JsonNode basis = draft ?? published ?? throw new McpException($"Page {pageId} was not found, or you cannot see it.");
        string title = basis["title"]!.GetValue<string>();

        (JsonObject document, JsonArray removed) = AdfSections.Replace(
            ConfluenceContent.ReadAdf(basis),
            heading,
            MarkdownToAdf.Convert(body),
            title);

        IReadOnlyDictionary<string, int> lossy = AdfInspector.FindLossyContent(removed);
        if (lossy.Count > 0 && !allowContentLoss)
        {
            throw new McpException(
                $"The section '{heading}' has content that Markdown cannot represent ({AdfInspector.Describe(lossy)}), so replacing it would lose that content. "
                + "Choose a smaller section, or get the page with bodyFormat adf and update it with bodyFormat adf. Set allowContentLoss only if the user accepts the loss.");
        }

        return await this.WriteAsync(pageId, title, ConfluenceContent.ToRequestBody(document), versionMessage, publish, published, cancellationToken);
    }

    /// <summary>
    /// Publishes the draft of the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="versionMessage">The version message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_confluence_publish_page", OpenWorld = true)]
    [Description("Publishes the draft of the specified Confluence page: a new page saved as a draft, or unpublished changes to an existing page. Only call this when the user explicitly asks to publish, after they have previewed the draft.")]
    public async Task<string> Publish(
        [Description("The page ID.")] string pageId,
        [Description("Optional version message, shown in the page history.")] string? versionMessage = null,
        CancellationToken cancellationToken = default)
    {
        JsonNode draft = await this.TryGetDraftAsync(pageId, "adf", cancellationToken)
            ?? throw new McpException($"Page {pageId} has no draft to publish.");
        JsonNode? published = await this.TryGetPublishedAsync(pageId, cancellationToken);

        return await this.WriteAsync(
            pageId,
            draft["title"]!.GetValue<string>(),
            ConfluenceContent.ToRequestBody(ConfluenceContent.ReadAdf(draft)),
            versionMessage,
            publish: true,
            published,
            cancellationToken);
    }

    /// <summary>
    /// Moves the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="targetPageId">The ID of the page to move it relative to.</param>
    /// <param name="position">Where to move it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_confluence_move_page", Idempotent = true, OpenWorld = true)]
    [Description("Moves the specified Confluence page, with its children: under another page, or before or after a sibling page. The target can be in another space.")]
    public async Task<string> Move(
        [Description("The ID of the page to move.")] string pageId,
        [Description("The ID of the target page.")] string targetPageId,
        [Description("Where to move the page: append (as the last child of the target, the default), before, or after the target.")] string position = "append",
        CancellationToken cancellationToken = default)
    {
        string where = position.Trim().ToUpperInvariant() switch
        {
            "APPEND" or "" => "append",
            "BEFORE" => "before",
            "AFTER" => "after",
            _ => throw new McpException("The position parameter must be append, before, or after."),
        };

        await this.confluence.SendV1Async(HttpMethod.Put, $"content/{Segment(pageId)}/move/{where}/{Segment(targetPageId)}", body: null, cancellationToken);
        return ToolResult.Success($"Moved page {pageId} ({where} page {targetPageId}).");
    }

    /// <summary>
    /// Deletes the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_confluence_delete_page", Destructive = true, OpenWorld = true)]
    [Toolset(Toolsets.ConfluenceAdmin)]
    [Description("Deletes the specified Confluence page. A published page goes to the space's trash, where a space administrator can restore it; a page that was never published is deleted permanently.")]
    public async Task<string> Delete(
        [Description("The page ID.")] string pageId,
        CancellationToken cancellationToken = default)
    {
        JsonNode? published = await this.TryGetPublishedAsync(pageId, cancellationToken);
        string path = new QueryString($"pages/{Segment(pageId)}").Add("draft", published is null ? true : null).ToString();

        await this.confluence.SendAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        return ToolResult.Success(published is null ? $"Deleted the draft page {pageId}." : $"Moved page {pageId} to the trash.");
    }

    private static string Segment(string value) => Uri.EscapeDataString(value.Trim());

    private static string BodyFormatParameter(string format) => format == "storage" ? ConfluenceContent.StorageRepresentation : ConfluenceContent.AdfRepresentation;

    private static bool IsNotFound(AtlassianApiException exception) => exception.StatusCode == HttpStatusCode.NotFound;

    private async Task<string> WriteAsync(
        string pageId,
        string title,
        JsonObject body,
        string? versionMessage,
        bool publish,
        JsonNode? published,
        CancellationToken cancellationToken)
    {
        // A draft holds a single version, and Confluence requires version 1 for it. Publishing
        // creates the next version of the published page, or version 1 for a page never published.
        int version = publish ? (published?["version"]?["number"]?.GetValue<int>() ?? 0) + 1 : 1;

        var request = new JsonObject
        {
            ["id"] = pageId.Trim(),
            ["status"] = publish ? "current" : "draft",
            ["title"] = title,
            ["body"] = body,
            ["version"] = new JsonObject
            {
                ["number"] = version,
                ["message"] = string.IsNullOrWhiteSpace(versionMessage) ? DefaultVersionMessage : versionMessage.Trim(),
            },
        };

        JsonNode? page = await this.confluence.SendAsync(HttpMethod.Put, $"pages/{Segment(pageId)}", request, cancellationToken);

        return ToolResult.Json(new JsonObject
        {
            ["id"] = page?["id"]?.DeepClone() ?? pageId,
            ["title"] = page?["title"]?.DeepClone() ?? title,
            ["status"] = page?["status"]?.DeepClone(),
            ["version"] = page?["version"]?["number"]?.DeepClone(),
            ["url"] = this.Link(page, publish ? "webui" : "editui") ?? this.Link(page, "webui"),
            ["message"] = publish
                ? "Published the change."
                : published is null
                    ? "Saved the change to the draft page. It is not published. Open the URL to preview it."
                    : "Saved the change as a draft; the published page is unchanged. Open the URL to preview it in the editor, then publish it there or ask to publish it.",
        });
    }

    private async Task<string> ResolveSpaceIdAsync(string? spaceKey, string? parentId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(spaceKey))
        {
            return await SpaceTools.ResolveIdAsync(this.confluence, spaceKey, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(parentId))
        {
            JsonNode parent = await this.GetPublishedOrDraftAsync(parentId, "adf", cancellationToken);
            return parent["spaceId"]?.GetValue<string>() ?? throw new McpException($"Could not find the space of page {parentId}.");
        }

        throw new McpException("Pass spaceKey, parentId, or both.");
    }

    private async Task<JsonNode> GetPublishedOrDraftAsync(string pageId, string format, CancellationToken cancellationToken)
        => await this.TryGetPublishedAsync(pageId, cancellationToken, format)
           ?? await this.TryGetDraftAsync(pageId, format, cancellationToken)
           ?? throw new McpException($"Page {pageId} was not found, or you cannot see it.");

    private async Task<JsonNode?> TryGetPublishedAsync(string pageId, CancellationToken cancellationToken, string format = "adf")
    {
        try
        {
            JsonNode? page = await this.confluence.GetAsync($"pages/{Segment(pageId)}?body-format={BodyFormatParameter(format)}", cancellationToken);
            return string.Equals(page?["status"]?.GetValue<string>(), "draft", StringComparison.Ordinal) ? null : page;
        }
        catch (AtlassianApiException exception) when (IsNotFound(exception))
        {
            return null;
        }
    }

    private async Task<JsonNode?> TryGetDraftAsync(string pageId, string format, CancellationToken cancellationToken)
    {
        try
        {
            JsonNode? page = await this.confluence.GetAsync($"pages/{Segment(pageId)}?body-format={BodyFormatParameter(format)}&get-draft=true", cancellationToken);
            return string.Equals(page?["status"]?.GetValue<string>(), "draft", StringComparison.Ordinal) ? page : null;
        }
        catch (AtlassianApiException exception) when (IsNotFound(exception))
        {
            return null;
        }
    }

    private JsonObject View(JsonNode page, string format)
    {
        var view = new JsonObject
        {
            ["id"] = page["id"]?.DeepClone(),
            ["title"] = page["title"]?.DeepClone(),
            ["status"] = page["status"]?.DeepClone(),
            ["spaceId"] = page["spaceId"]?.DeepClone(),
            ["parentId"] = page["parentId"]?.DeepClone(),
            ["authorId"] = page["authorId"]?.DeepClone(),
            ["createdAt"] = page["createdAt"]?.DeepClone(),
            ["version"] = page["version"]?.DeepClone(),
            ["url"] = this.Link(page, "webui"),
            ["editUrl"] = this.Link(page, "editui"),
        };

        if (format == "storage")
        {
            view["body"] = page["body"]?[ConfluenceContent.StorageRepresentation]?["value"]?.DeepClone();
            return view;
        }

        JsonObject document = ConfluenceContent.ReadAdf(page);
        IReadOnlyDictionary<string, int> lossy = AdfInspector.FindLossyContent(document);
        if (lossy.Count > 0)
        {
            view["contentMarkdownCannotRepresent"] = new JsonObject(lossy.Select(pair => KeyValuePair.Create(pair.Key, (JsonNode?)pair.Value)));
        }

        view["body"] = format == "adf" ? document : AdfToMarkdown.Convert(document);
        return view;
    }

    private string? Link(JsonNode? page, string name)
    {
        string? relative = page?["_links"]?[name]?.GetValue<string>();
        return relative is null ? null : this.confluence.WebUrl(relative).ToString();
    }
}
