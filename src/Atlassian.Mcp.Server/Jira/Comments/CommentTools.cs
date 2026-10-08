// <copyright file="CommentTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Comments;

/// <summary>
/// Tools for the comments of Jira issues.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraComments)]
public sealed class CommentTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public CommentTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the comments for the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="startAt">The index of the first comment to return.</param>
    /// <param name="maxResults">The largest number of comments to return.</param>
    /// <param name="orderBy">The sort order.</param>
    /// <param name="includeRenderedBody">A value indicating whether to include the HTML rendering of each body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of comments.</returns>
    [McpServerTool(Name = "atlassian_jira_get_comments", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the comments for the specified Jira issue, one page at a time. Bodies are returned as Atlassian Document Format (ADF).")]
    public async Task<string> GetAll(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The zero-based index of the first comment to return. Defaults to 0.")] int? startAt = null,
        [Description("The largest number of comments to return. Defaults to 50.")] int? maxResults = null,
        [Description("The sort order: \"created\" for oldest first, or \"-created\" for newest first.")] string? orderBy = null,
        [Description("When true, each comment also includes its body rendered as HTML.")] bool? includeRenderedBody = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"issue/{JiraClient.Segment(issueKey)}/comment")
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .Add("orderBy", orderBy)
            .Add("expand", includeRenderedBody == true ? "renderedBody" : null)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the specified comment.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="commentId">The comment ID.</param>
    /// <param name="includeRenderedBody">A value indicating whether to include the HTML rendering of the body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The comment.</returns>
    [McpServerTool(Name = "atlassian_jira_get_comment", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified comment on a Jira issue.")]
    public async Task<string> Get(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The comment ID, from atlassian_jira_get_comments.")] string commentId,
        [Description("When true, the comment also includes its body rendered as HTML.")] bool? includeRenderedBody = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"issue/{JiraClient.Segment(issueKey)}/comment/{JiraClient.Segment(commentId)}")
            .Add("expand", includeRenderedBody == true ? "renderedBody" : null)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Adds a comment to the specified issue.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="body">The comment, as Markdown.</param>
    /// <param name="visibilityType">The kind of restriction: role or group.</param>
    /// <param name="visibilityValue">The role or group name that may see the comment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new comment.</returns>
    [McpServerTool(Name = "atlassian_jira_add_comment", OpenWorld = true)]
    [Description("Adds a comment to the specified Jira issue. The body is Markdown, converted to Atlassian Document Format. Optionally restricts who can see it to a project role or a group.")]
    public async Task<string> Add(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The comment, as Markdown.")] string body,
        [Description("Restricts visibility: \"role\" or \"group\". Requires visibilityValue.")] string? visibilityType = null,
        [Description("The project role name or group name that may see the comment.")] string? visibilityValue = null,
        CancellationToken cancellationToken = default)
    {
        JsonObject request = BuildBody(body, visibilityType, visibilityValue);
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, $"issue/{JiraClient.Segment(issueKey)}/comment", request, cancellationToken));
    }

    /// <summary>
    /// Replaces the body and visibility of the specified comment.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="commentId">The comment ID.</param>
    /// <param name="body">The new comment, as Markdown.</param>
    /// <param name="visibilityType">The kind of restriction: role or group.</param>
    /// <param name="visibilityValue">The role or group name that may see the comment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated comment.</returns>
    [McpServerTool(Name = "atlassian_jira_update_comment", Idempotent = true, OpenWorld = true)]
    [Description("Replaces the body of the specified comment on a Jira issue. The body is Markdown, converted to Atlassian Document Format.")]
    public async Task<string> Update(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The comment ID, from atlassian_jira_get_comments.")] string commentId,
        [Description("The new comment, as Markdown. It replaces the whole body.")] string body,
        [Description("Restricts visibility: \"role\" or \"group\". Requires visibilityValue.")] string? visibilityType = null,
        [Description("The project role name or group name that may see the comment.")] string? visibilityValue = null,
        CancellationToken cancellationToken = default)
    {
        JsonObject request = BuildBody(body, visibilityType, visibilityValue);
        string path = $"issue/{JiraClient.Segment(issueKey)}/comment/{JiraClient.Segment(commentId)}";
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Put, path, request, cancellationToken));
    }

    /// <summary>
    /// Deletes the specified comment.
    /// </summary>
    /// <param name="issueKey">The issue key or ID.</param>
    /// <param name="commentId">The comment ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_comment", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified comment from a Jira issue.")]
    public async Task<string> Delete(
        [Description("The issue key, such as PROJ-123, or the issue ID.")] string issueKey,
        [Description("The comment ID, from atlassian_jira_get_comments.")] string commentId,
        CancellationToken cancellationToken = default)
    {
        string path = $"issue/{JiraClient.Segment(issueKey)}/comment/{JiraClient.Segment(commentId)}";
        await this.jira.SendAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        return ToolResult.Success($"Deleted comment {commentId} from {issueKey}.");
    }

    /// <summary>
    /// Builds the request body of a comment.
    /// </summary>
    /// <param name="body">The comment, as Markdown.</param>
    /// <param name="visibilityType">The kind of restriction, or <see langword="null"/>.</param>
    /// <param name="visibilityValue">The role or group name, or <see langword="null"/>.</param>
    /// <returns>The request body.</returns>
    /// <exception cref="McpException">The visibility arguments are incomplete or invalid.</exception>
    internal static JsonObject BuildBody(string body, string? visibilityType, string? visibilityValue)
    {
        var request = new JsonObject { ["body"] = JiraClient.ToAdf(body) };

        bool hasType = !string.IsNullOrWhiteSpace(visibilityType);
        bool hasValue = !string.IsNullOrWhiteSpace(visibilityValue);
        if (hasType != hasValue)
        {
            throw new McpException("The visibilityType and visibilityValue parameters must be set together.");
        }

        if (hasType)
        {
            string type = visibilityType!.Trim().ToLowerInvariant();
            if (type is not ("role" or "group"))
            {
                throw new McpException("The visibilityType parameter must be \"role\" or \"group\".");
            }

            request["visibility"] = new JsonObject
            {
                ["type"] = type,
                ["value"] = visibilityValue!.Trim(),
            };
        }

        return request;
    }
}
