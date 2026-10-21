// <copyright file="AdfLocalIds.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// Carries the <c>localId</c> attributes of a page's nodes over to the new version of its body.
/// </summary>
/// <remarks>
/// Confluence gives every heading, paragraph, table, row, cell, list, and list item a <c>localId</c>,
/// and gives a node that arrives without one a new, random one. A body converted from Markdown has
/// none, so without this every node of the page gets a new ID on every update, and the Confluence
/// page history compares the versions as if every paragraph had changed formatting. Nodes are matched
/// in document order: first those whose content is unchanged, then, between two unchanged nodes, those
/// of the same type, so an edited paragraph keeps its ID and shows as a text change. A new node gets
/// no ID, and Confluence assigns one.
/// </remarks>
public static class AdfLocalIds
{
    private const string LocalId = "localId";

    /// <summary>
    /// Copies the <c>localId</c> of each node of <paramref name="previous"/> to the matching node of
    /// <paramref name="updated"/> that has none, changing <paramref name="updated"/>.
    /// </summary>
    /// <param name="previous">The current body of the page. It is not changed.</param>
    /// <param name="updated">The new body.</param>
    /// <returns>The number of IDs copied.</returns>
    public static int Preserve(JsonObject previous, JsonObject updated)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(updated);

        // An ID that the new body already has, such as one kept by a section update, is not given
        // to a second node.
        var taken = new HashSet<string>(StringComparer.Ordinal);
        CollectIds(updated, taken);

        int copied = 0;
        Align(previous["content"] as JsonArray, updated["content"] as JsonArray, taken, ref copied);
        return copied;
    }

    private static void Align(JsonArray? previous, JsonArray? updated, HashSet<string> taken, ref int copied)
    {
        if (previous is null || updated is null || previous.Count == 0 || updated.Count == 0)
        {
            return;
        }

        List<JsonObject> oldNodes = previous.OfType<JsonObject>().ToList();
        List<JsonObject> newNodes = updated.OfType<JsonObject>().ToList();
        string[] oldKeys = oldNodes.Select(Fingerprint).ToArray();
        string[] newKeys = newNodes.Select(Fingerprint).ToArray();

        // The longest common subsequence of unchanged nodes.
        int[,] lengths = new int[oldKeys.Length + 1, newKeys.Length + 1];
        for (int i = oldKeys.Length - 1; i >= 0; i--)
        {
            for (int j = newKeys.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = oldKeys[i] == newKeys[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        int oldIndex = 0;
        int newIndex = 0;
        int oldGapStart = 0;
        int newGapStart = 0;
        while (oldIndex < oldKeys.Length && newIndex < newKeys.Length)
        {
            if (oldKeys[oldIndex] == newKeys[newIndex])
            {
                PairGap(oldNodes, oldGapStart, oldIndex, newNodes, newGapStart, newIndex, taken, ref copied);
                Pair(oldNodes[oldIndex], newNodes[newIndex], taken, ref copied);
                oldIndex++;
                newIndex++;
                oldGapStart = oldIndex;
                newGapStart = newIndex;
            }
            else if (lengths[oldIndex + 1, newIndex] >= lengths[oldIndex, newIndex + 1])
            {
                oldIndex++;
            }
            else
            {
                newIndex++;
            }
        }

        PairGap(oldNodes, oldGapStart, oldNodes.Count, newNodes, newGapStart, newNodes.Count, taken, ref copied);
    }

    // Between two unchanged nodes, pairs each new node with the next old node of the same type.
    private static void PairGap(List<JsonObject> oldNodes, int oldStart, int oldEnd, List<JsonObject> newNodes, int newStart, int newEnd, HashSet<string> taken, ref int copied)
    {
        int next = oldStart;
        for (int index = newStart; index < newEnd; index++)
        {
            string type = TypeOf(newNodes[index]);
            int match = next;
            while (match < oldEnd && TypeOf(oldNodes[match]) != type)
            {
                match++;
            }

            if (match < oldEnd)
            {
                Pair(oldNodes[match], newNodes[index], taken, ref copied);
                next = match + 1;
            }
        }
    }

    private static void Pair(JsonObject previous, JsonObject updated, HashSet<string> taken, ref int copied)
    {
        if (previous["attrs"]?[LocalId] is JsonValue value
            && value.TryGetValue(out string? id)
            && !string.IsNullOrEmpty(id)
            && updated["attrs"]?[LocalId] is null
            && taken.Add(id))
        {
            if (updated["attrs"] is not JsonObject attrs)
            {
                attrs = [];
                updated["attrs"] = attrs;
            }

            attrs[LocalId] = id;
            copied++;
        }

        Align(previous["content"] as JsonArray, updated["content"] as JsonArray, taken, ref copied);
    }

    private static string TypeOf(JsonObject node)
        => node["type"] is JsonValue type && type.TryGetValue(out string? name) ? name : string.Empty;

    // The node without its IDs, and without the attributes that Confluence adds with their default
    // values, written with its properties in a fixed order: Confluence returns them in a different
    // order than the Markdown converter writes them.
    private static string Fingerprint(JsonObject node)
    {
        var text = new StringBuilder();
        Write(node, text);
        return text.ToString();
    }

    private static void Write(JsonNode? node, StringBuilder text)
    {
        switch (node)
        {
            case JsonObject item:
                text.Append('{');
                foreach (KeyValuePair<string, JsonNode?> property in item.OrderBy(property => property.Key, StringComparer.Ordinal))
                {
                    if (property.Key == "attrs" && property.Value is JsonObject attrs)
                    {
                        var kept = new JsonObject();
                        foreach (KeyValuePair<string, JsonNode?> attribute in attrs)
                        {
                            if (!IsIgnored(attribute.Key, attribute.Value))
                            {
                                kept[attribute.Key] = attribute.Value?.DeepClone();
                            }
                        }

                        if (kept.Count == 0)
                        {
                            continue;
                        }

                        text.Append("\"attrs\":");
                        Write(kept, text);
                    }
                    else
                    {
                        text.Append(JsonSerializer.Serialize(property.Key)).Append(':');
                        Write(property.Value, text);
                    }

                    text.Append(',');
                }

                text.Append('}');
                break;

            case JsonArray items:
                text.Append('[');
                foreach (JsonNode? child in items)
                {
                    Write(child, text);
                    text.Append(',');
                }

                text.Append(']');
                break;

            default:
                text.Append(node?.ToJsonString() ?? "null");
                break;
        }
    }

    private static bool IsIgnored(string name, JsonNode? value)
        => name == LocalId
            || ((name == "colspan" || name == "rowspan") && value is JsonValue span && span.TryGetValue(out int count) && count == 1);

    private static void CollectIds(JsonNode? node, HashSet<string> ids)
    {
        switch (node)
        {
            case JsonObject item:
                if (item["attrs"]?[LocalId] is JsonValue value && value.TryGetValue(out string? id) && !string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }

                CollectIds(item["content"], ids);
                break;

            case JsonArray items:
                foreach (JsonNode? child in items)
                {
                    CollectIds(child, ids);
                }

                break;
        }
    }
}
