// <copyright file="AttachmentTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Attachments;

/// <summary>
/// Tools for the attachments of Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraAttachments)]
public sealed class AttachmentTools
{
    /// <summary>The largest attachment returned by <see cref="GetContent"/>, to protect the model's context.</summary>
    internal const long MaxContentBytes = 10 * 1024 * 1024;

    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public AttachmentTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets all attachments for the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attachments.</returns>
    [McpServerTool(Name = "atlassian_jira_get_attachments", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets all attachments for the specified Jira issue: ID, file name, size, media type, author, and creation date.")]
    public async Task<string> GetAll(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        CancellationToken cancellationToken)
    {
        JsonNode? issue = await this.jira.GetAsync($"issue/{JiraClient.Segment(issueKey)}?fields=attachment", cancellationToken);
        return ToolResult.Json(issue?["fields"]?["attachment"] ?? new JsonArray());
    }

    /// <summary>
    /// Gets the metadata of the specified attachment.
    /// </summary>
    /// <param name="attachmentId">The attachment ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attachment metadata.</returns>
    [McpServerTool(Name = "atlassian_jira_get_attachment", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the metadata of the specified Jira attachment: file name, size, media type, author, creation date, and download URL.")]
    public async Task<string> Get(
        [Description("The attachment ID, from atlassian_jira_get_attachments.")] string attachmentId,
        CancellationToken cancellationToken)
        => ToolResult.Json(await this.jira.GetAsync($"attachment/{JiraClient.Segment(attachmentId)}", cancellationToken));

    /// <summary>
    /// Downloads the content of the specified attachment.
    /// </summary>
    /// <param name="attachmentId">The attachment ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The content, as text for text files and as base64 otherwise.</returns>
    [McpServerTool(Name = "atlassian_jira_get_attachment_content", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Downloads the content of the specified Jira attachment, up to 10 MB. Text files (logs, JSON, XML, CSV) are returned as text; other files as base64.")]
    public async Task<string> GetContent(
        [Description("The attachment ID, from atlassian_jira_get_attachments.")] string attachmentId,
        CancellationToken cancellationToken)
    {
        (byte[] content, string? mediaType) = await this.jira.Http.DownloadAsync(
            JiraClient.PlatformPath + $"attachment/content/{JiraClient.Segment(attachmentId)}",
            MaxContentBytes,
            cancellationToken);

        return MediaTypes.IsText(mediaType)
            ? ToolResult.Json(new { attachmentId, mediaType, size = content.Length, text = System.Text.Encoding.UTF8.GetString(content) })
            : ToolResult.Json(new { attachmentId, mediaType, size = content.Length, base64Content = Convert.ToBase64String(content) });
    }

    /// <summary>
    /// Gets the attachment settings of the site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether attachments are enabled, and the largest upload allowed.</returns>
    [McpServerTool(Name = "atlassian_jira_get_attachment_settings", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the attachment settings of the Jira site: whether attachments are enabled, and the largest file size allowed in bytes.")]
    public async Task<string> GetSettings(CancellationToken cancellationToken)
        => ToolResult.Json(await this.jira.GetAsync("attachment/meta", cancellationToken));

    /// <summary>
    /// Attaches a file to the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="fileName">The file name, with its extension.</param>
    /// <param name="base64Content">The file content, encoded as base64.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attachment.</returns>
    [McpServerTool(Name = "atlassian_jira_add_attachment", OpenWorld = true)]
    [Description("Attaches a file to the specified Jira issue. The content is passed as base64.")]
    public async Task<string> Add(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The file name, with its extension, such as screenshot.png.")] string fileName,
        [Description("The file content, encoded as base64.")] string base64Content,
        CancellationToken cancellationToken)
    {
        byte[] content = DecodeBase64(base64Content);

        JsonNode? attachments = await this.jira.Http.UploadAsync(
            JiraClient.PlatformPath + $"issue/{JiraClient.Segment(issueKey)}/attachments",
            fileName,
            content,
            formFields: null,
            cancellationToken);

        return ToolResult.Json(attachments);
    }

    /// <summary>
    /// Deletes the specified attachment.
    /// </summary>
    /// <param name="attachmentId">The attachment ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_attachment", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified Jira attachment.")]
    public async Task<string> Delete(
        [Description("The attachment ID, from atlassian_jira_get_attachments.")] string attachmentId,
        CancellationToken cancellationToken)
    {
        await this.jira.SendAsync(HttpMethod.Delete, $"attachment/{JiraClient.Segment(attachmentId)}", body: null, cancellationToken);
        return ToolResult.Success($"Deleted attachment {attachmentId}.");
    }

    /// <summary>
    /// Decodes base64 content, accepting a data URL prefix such as <c>data:image/png;base64,</c>.
    /// </summary>
    /// <param name="base64Content">The content.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="McpException">The content is not valid base64.</exception>
    internal static byte[] DecodeBase64(string base64Content)
    {
        string text = base64Content.Trim();
        int comma = text.IndexOf(',', StringComparison.Ordinal);
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
        {
            text = text[(comma + 1)..];
        }

        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException exception)
        {
            throw new McpException("The base64Content parameter is not valid base64.", exception);
        }
    }
}
