// <copyright file="AdfInspector.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// Finds the content in an ADF document that Markdown cannot represent, so that a tool can refuse
/// to replace a document with Markdown when that would silently lose it.
/// </summary>
public static class AdfInspector
{
    /// <summary>
    /// The node types that a round trip through Markdown loses or changes: macros and other
    /// extensions, media, smart links shown as cards, mentions, panels, expands, layouts, status
    /// lozenges, dates, emoji, and task and decision lists.
    /// </summary>
    private static readonly HashSet<string> LossyTypes = new(StringComparer.Ordinal)
    {
        "extension",
        "bodiedExtension",
        "inlineExtension",
        "multiBodiedExtension",
        "media",
        "mediaSingle",
        "mediaGroup",
        "mediaInline",
        "inlineCard",
        "blockCard",
        "embedCard",
        "mention",
        "panel",
        "expand",
        "nestedExpand",
        "layoutSection",
        "status",
        "date",
        "emoji",
        "taskList",
        "decisionList",
        "placeholder",
        "syncBlock",
        "bodiedSyncBlock",
    };

    /// <summary>
    /// Counts the content in <paramref name="node"/> that Markdown cannot represent, by node type.
    /// </summary>
    /// <param name="node">The document or node.</param>
    /// <returns>The count of each lossy node type found; empty when the document is plain.</returns>
    public static IReadOnlyDictionary<string, int> FindLossyContent(JsonNode? node)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        Visit(node, counts);
        return counts;
    }

    /// <summary>
    /// Describes the lossy content for a message, such as "2 extension, 1 inlineCard".
    /// </summary>
    /// <param name="counts">The counts from <see cref="FindLossyContent"/>.</param>
    /// <returns>The description.</returns>
    public static string Describe(IReadOnlyDictionary<string, int> counts)
        => string.Join(", ", (counts ?? new Dictionary<string, int>()).Select(pair => $"{pair.Value} {pair.Key}"));

    private static void Visit(JsonNode? node, SortedDictionary<string, int> counts)
    {
        switch (node)
        {
            case JsonObject item:
                if (item["type"] is JsonValue type && type.TryGetValue(out string? name) && LossyTypes.Contains(name))
                {
                    counts[name] = counts.GetValueOrDefault(name) + 1;
                }

                Visit(item["content"], counts);
                break;

            case JsonArray items:
                foreach (JsonNode? child in items)
                {
                    Visit(child, counts);
                }

                break;
        }
    }
}
