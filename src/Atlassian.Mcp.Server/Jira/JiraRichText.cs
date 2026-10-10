// <copyright file="JiraRichText.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Jira;

/// <summary>
/// Reads Jira responses whose rich text is returned as Markdown or as ADF.
/// </summary>
internal static class JiraRichText
{
    /// <summary>
    /// Validates a rich-text format argument.
    /// </summary>
    /// <param name="richTextFormat">The argument: markdown (the default) or adf.</param>
    /// <returns><see langword="true"/> for Markdown; <see langword="false"/> for ADF.</returns>
    /// <exception cref="McpException">The argument is neither.</exception>
    public static bool IsMarkdown(string? richTextFormat)
        => (richTextFormat ?? "markdown").Trim().ToUpperInvariant() switch
        {
            "MARKDOWN" or "" => true,
            "ADF" => false,
            _ => throw new McpException("The richTextFormat parameter must be markdown or adf."),
        };

    /// <summary>
    /// Gets a Jira resource and converts its rich text to Markdown, writing embedded files as
    /// <c>![name](attachment:ID)</c>.
    /// <para>
    /// Matching a media node to its attachment needs the HTML that Jira renders for the same text
    /// (see <see cref="RenderedAttachments"/>). That HTML is requested only when the response has
    /// media and the caller did not already ask for it, and is then removed again, so text without
    /// files costs one request as before.
    /// </para>
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    /// <param name="path">Builds the request path, with the rendered HTML (<see langword="true"/>) or without it.</param>
    /// <param name="renderedRequested">A value indicating whether the caller asked for the rendered HTML.</param>
    /// <param name="renderedProperty">The property that holds the rendered HTML: <c>renderedBody</c> or <c>renderedFields</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, with its rich text as Markdown.</returns>
    public static async Task<JsonNode?> GetAsMarkdownAsync(
        JiraClient jira,
        Func<bool, string> path,
        bool renderedRequested,
        string renderedProperty,
        CancellationToken cancellationToken)
    {
        JsonNode? response = await jira.GetAsync(path(renderedRequested), cancellationToken);
        if (!RenderedAttachments.HasMedia(response))
        {
            return AdfToMarkdown.ConvertDocuments(response);
        }

        if (!renderedRequested)
        {
            response = await jira.GetAsync(path(true), cancellationToken);
        }

        IReadOnlyDictionary<string, AttachmentReference> attachments = RenderedAttachments.Find(response);
        if (!renderedRequested)
        {
            RenderedAttachments.RemoveRendered(response, renderedProperty);
        }

        return AdfToMarkdown.ConvertDocuments(response, attachments);
    }

    /// <summary>
    /// Adds <paramref name="value"/> to a comma-separated <c>expand</c> argument, unless it is already there.
    /// </summary>
    /// <param name="expand">The argument, or <see langword="null"/>.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The argument with the value.</returns>
    public static string WithExpand(string? expand, string value)
        => Expands(expand, value) ? expand! : string.IsNullOrWhiteSpace(expand) ? value : $"{expand},{value}";

    /// <summary>
    /// Returns <see langword="true"/> when a comma-separated <c>expand</c> argument includes <paramref name="value"/>.
    /// </summary>
    /// <param name="expand">The argument, or <see langword="null"/>.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value is there.</returns>
    public static bool Expands(string? expand, string value)
        => (expand ?? string.Empty).Split(',', StringSplitOptions.TrimEntries).Contains(value, StringComparer.OrdinalIgnoreCase);
}
