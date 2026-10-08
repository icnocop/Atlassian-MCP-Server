// <copyright file="AdfSections.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// Replaces the content under one heading of an ADF document, leaving every other node exactly as
/// it was, so that macros and other content outside the section survive an edit.
/// </summary>
public static class AdfSections
{
    /// <summary>
    /// Returns the plain text of every top-level heading in a document, for error messages.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The headings, prefixed with Markdown heading marks to show their level.</returns>
    public static IReadOnlyList<string> ListHeadings(JsonObject document)
        => TopLevel(document)
            .Where(IsHeading)
            .Select(node => new string('#', LevelOf(node)) + " " + PlainText(node))
            .ToList();

    /// <summary>
    /// Replaces the content of the section that starts at the heading named <paramref name="heading"/>.
    /// The section runs to the next top-level heading of the same or a higher level, or to the end.
    /// </summary>
    /// <param name="document">The document. It is not changed.</param>
    /// <param name="heading">The heading text. Leading Markdown heading marks are ignored.</param>
    /// <param name="replacement">The new content of the section, as an ADF document. When it starts with the same heading, that heading replaces the old one.</param>
    /// <param name="pageTitle">The page title, which is rejected as a heading because it is not part of the body.</param>
    /// <returns>The new document, and the old nodes that were replaced.</returns>
    /// <exception cref="McpException">The heading is missing, ambiguous, the page title, or would cover the whole page.</exception>
    public static (JsonObject Document, JsonArray Removed) Replace(JsonObject document, string heading, JsonObject replacement, string? pageTitle)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(replacement);

        string wanted = Normalize(heading);
        if (wanted.Length == 0)
        {
            throw new McpException("The heading parameter must name a heading in the page.");
        }

        List<JsonNode?> nodes = TopLevel(document).Select(node => (JsonNode?)node).ToList();
        List<int> matches = Enumerable.Range(0, nodes.Count)
            .Where(index => nodes[index] is JsonObject node && IsHeading(node) && Normalize(PlainText(node)) == wanted)
            .ToList();

        if (matches.Count == 0)
        {
            if (pageTitle is not null && Normalize(pageTitle) == wanted)
            {
                throw new McpException("That is the page title, not a section heading. To replace the whole page, use atlassian_confluence_update_page; to rename it, pass a new title there.");
            }

            IReadOnlyList<string> headings = ListHeadings(document);
            throw new McpException($"The page has no heading '{heading}'. Its headings are: {(headings.Count == 0 ? "none" : string.Join("; ", headings))}.");
        }

        if (matches.Count > 1)
        {
            throw new McpException($"The page has {matches.Count} headings named '{heading}', so the section is ambiguous. Rename one of them, or use atlassian_confluence_update_page with bodyFormat adf.");
        }

        int start = matches[0];
        int level = LevelOf((JsonObject)nodes[start]!);
        int end = start + 1;
        while (end < nodes.Count && !(nodes[end] is JsonObject next && IsHeading(next) && LevelOf(next) <= level))
        {
            end++;
        }

        if (start == 0 && end == nodes.Count && level == 1)
        {
            throw new McpException($"The heading '{heading}' starts the page and its section covers the whole page. Use atlassian_confluence_update_page to replace the whole page.");
        }

        List<JsonNode?> newContent = (replacement["content"] as JsonArray ?? []).Select(node => node?.DeepClone()).ToList();

        // When the new content repeats the heading, it replaces the old heading (which allows a
        // rename or a level change); otherwise the old heading stays.
        bool replacesHeading = newContent.Count > 0
            && newContent[0] is JsonObject first
            && IsHeading(first)
            && Normalize(PlainText(first)) == wanted;

        int removeFrom = replacesHeading ? start : start + 1;
        var removed = new JsonArray(nodes.Skip(removeFrom).Take(end - removeFrom).Select(node => node?.DeepClone()).ToArray());

        var content = new JsonArray();
        foreach (JsonNode? node in nodes.Take(removeFrom))
        {
            content.Add(node?.DeepClone());
        }

        foreach (JsonNode? node in newContent)
        {
            content.Add(node);
        }

        foreach (JsonNode? node in nodes.Skip(end))
        {
            content.Add(node?.DeepClone());
        }

        var result = (JsonObject)document.DeepClone();
        result["content"] = content;
        return (result, removed);
    }

    private static IEnumerable<JsonObject> TopLevel(JsonObject document)
        => (document["content"] as JsonArray ?? []).OfType<JsonObject>();

    private static bool IsHeading(JsonObject node)
        => node["type"] is JsonValue type && type.TryGetValue(out string? name) && name == "heading";

    private static int LevelOf(JsonObject heading)
        => heading["attrs"]?["level"] is JsonValue level && level.TryGetValue(out int value) ? value : 1;

    private static string PlainText(JsonNode? node)
    {
        var text = new StringBuilder();
        Collect(node, text);
        return text.ToString();
    }

    private static void Collect(JsonNode? node, StringBuilder text)
    {
        switch (node)
        {
            case JsonObject item:
                if (item["text"] is JsonValue value && value.TryGetValue(out string? part))
                {
                    text.Append(part);
                }

                Collect(item["content"], text);
                break;

            case JsonArray items:
                foreach (JsonNode? child in items)
                {
                    Collect(child, text);
                }

                break;
        }
    }

    private static string Normalize(string text)
        => string.Join(' ', text.Trim().TrimStart('#').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
