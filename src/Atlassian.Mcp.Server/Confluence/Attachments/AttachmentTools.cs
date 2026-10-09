// <copyright file="AttachmentTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Attachments;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;
using JiraAttachmentTools = Atlassian.Mcp.Server.Jira.Attachments.AttachmentTools;

namespace Atlassian.Mcp.Server.Confluence.Attachments;

/// <summary>
/// Tools for the attachments of Confluence pages.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.Confluence)]
public sealed class AttachmentTools
{
    private readonly ConfluenceClient confluence;
    private readonly AttachmentReader reader;
    private readonly Func<McpServer, IUploadApproval> approvals;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentTools"/> class.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    /// <param name="reader">Reads the content of a file to attach.</param>
    /// <param name="approvals">Creates the way to ask the user about a file, for the current tool call.</param>
    public AttachmentTools(ConfluenceClient confluence, AttachmentReader reader, Func<McpServer, IUploadApproval> approvals)
    {
        this.confluence = confluence;
        this.reader = reader;
        this.approvals = approvals;
    }

    /// <summary>
    /// Gets the attachments of the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="limit">The largest number of attachments to return.</param>
    /// <param name="cursor">The cursor of the page of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of attachments.</returns>
    [McpServerTool(Name = "atlassian_confluence_get_attachments", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the attachments of the specified Confluence page: ID, title (file name), media type, size, and version.")]
    public async Task<string> GetAll(
        [Description("The page ID.")] string pageId,
        [Description("Optional largest number of attachments to return, up to 250. Defaults to 25.")] int limit = 25,
        [Description("Optional cursor from the previous result, to get the next page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"pages/{Uri.EscapeDataString(pageId.Trim())}/attachments")
            .Add("limit", Math.Clamp(limit, 1, 250))
            .Add("cursor", cursor)
            .ToString();

        JsonNode? response = await this.confluence.GetAsync(path, cancellationToken);
        return ToolResult.Json(new JsonObject
        {
            ["attachments"] = response?["results"]?.DeepClone(),
            ["nextCursor"] = ConfluenceContent.NextCursor(response),
        });
    }

    /// <summary>
    /// Attaches a file to the specified page.
    /// </summary>
    /// <param name="pageId">The page ID.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="base64Content">The file content, encoded as base64.</param>
    /// <param name="comment">A comment describing the file.</param>
    /// <param name="filePath">The full path of a local file to upload.</param>
    /// <param name="server">The MCP server handling the call, used to ask the user about a file outside the allowed folders.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attachment.</returns>
    [McpServerTool(Name = "atlassian_confluence_add_attachment", OpenWorld = true)]
    [Description("Attaches a file to the specified Confluence page. An attachment with the same file name gets a new version. Pass the content as filePath, the full path of a local file, or as base64Content. Prefer filePath for any existing file, such as a screenshot or a diagram: it needs no encoding. A file outside the folders the user allowed (atlassian_jira_get_configuration lists them) is uploaded only if the user approves it in a prompt the server shows; you do not need to ask first.")]
    public async Task<string> Add(
        [Description("The page ID.")] string pageId,
        [Description(JiraAttachmentTools.FileNameDescription)] string? fileName = null,
        [Description(JiraAttachmentTools.Base64ContentDescription)] string? base64Content = null,
        [Description("Optional comment describing the file.")] string? comment = null,
        [Description(JiraAttachmentTools.FilePathDescription)] string? filePath = null,
        McpServer server = null!,
        CancellationToken cancellationToken = default)
    {
        (string name, byte[] content) = await this.reader.ReadAsync(
            fileName,
            base64Content,
            filePath,
            $"Confluence page {pageId.Trim()}",
            this.approvals(server),
            cancellationToken);

        var fields = new Dictionary<string, string> { ["minorEdit"] = "true" };
        if (!string.IsNullOrWhiteSpace(comment))
        {
            fields["comment"] = comment;
        }

        JsonNode? result = await this.confluence.Http.UploadAsync(
            ConfluenceClient.V1Path + $"content/{Uri.EscapeDataString(pageId.Trim())}/child/attachment",
            name,
            content,
            fields,
            cancellationToken);

        return ToolResult.Json(result?["results"] ?? result);
    }
}
