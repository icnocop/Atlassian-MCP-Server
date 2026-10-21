// <copyright file="AdfNodes.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;

namespace Atlassian.Mcp.Server.Tests.Common.Adf;

/// <summary>
/// Helpers for inspecting the ADF documents that the tests produce.
/// </summary>
internal static class AdfNodes
{
    /// <summary>
    /// Converts Markdown to ADF and returns the block nodes of the document.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <returns>The content array of the document.</returns>
    public static JsonElement Blocks(string markdown)
        => JsonDocument.Parse(MarkdownToAdf.Convert(markdown).ToJsonString()).RootElement.Clone().GetProperty("content");

    /// <summary>
    /// Returns the type of a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The type.</returns>
    public static string TypeOf(JsonElement node) => node.GetProperty("type").GetString()!;

    /// <summary>
    /// Concatenates the text of every leaf text node directly under a block.
    /// </summary>
    /// <param name="node">The block node.</param>
    /// <returns>The text.</returns>
    public static string TextOf(JsonElement node)
        => string.Concat(node.GetProperty("content").EnumerateArray()
            .Select(child => child.TryGetProperty("text", out JsonElement text) ? text.GetString() : string.Empty));

    /// <summary>
    /// Returns the mark types of a text node.
    /// </summary>
    /// <param name="textNode">The text node.</param>
    /// <returns>The mark types, or an empty list when the node has no marks.</returns>
    public static List<string> MarksOf(JsonElement textNode)
        => textNode.TryGetProperty("marks", out JsonElement marks)
            ? marks.EnumerateArray().Select(mark => mark.GetProperty("type").GetString()!).ToList()
            : [];

    /// <summary>
    /// Returns a copy of a document as Confluence stores it: every node except text has a localId,
    /// table cells have explicit colspan and rowspan, and text nodes list text before type.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The copy.</returns>
    public static JsonObject AsStoredByConfluence(JsonObject document)
    {
        var copy = (JsonObject)document.DeepClone();
        int next = 0;
        Visit(copy["content"] as JsonArray);
        return copy;

        void Visit(JsonArray? nodes)
        {
            if (nodes is null)
            {
                return;
            }

            for (int index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] is not JsonObject node)
                {
                    continue;
                }

                string type = node["type"]!.GetValue<string>();
                if (type == "text")
                {
                    var reordered = new JsonObject { ["text"] = node["text"]!.DeepClone(), ["type"] = "text" };
                    if (node["marks"] is JsonNode marks)
                    {
                        reordered["marks"] = marks.DeepClone();
                    }

                    nodes[index] = reordered;
                    continue;
                }

                if (node["attrs"] is not JsonObject attrs)
                {
                    attrs = [];
                    node["attrs"] = attrs;
                }

                if (type is "tableHeader" or "tableCell")
                {
                    attrs["colspan"] ??= 1;
                    attrs["rowspan"] ??= 1;
                }

                attrs["localId"] = $"id{++next}";
                Visit(node["content"] as JsonArray);
            }
        }
    }

    /// <summary>
    /// Returns the localId of every node of a document, in document order, with an empty string
    /// for a non-text node without one.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The IDs.</returns>
    public static List<string> Ids(JsonObject document)
    {
        var ids = new List<string>();
        Collect(document["content"] as JsonArray);
        return ids;

        void Collect(JsonArray? nodes)
        {
            foreach (JsonObject node in (nodes ?? []).OfType<JsonObject>())
            {
                if (node["type"]!.GetValue<string>() == "text")
                {
                    continue;
                }

                ids.Add(node["attrs"]?["localId"]?.GetValue<string>() ?? string.Empty);
                Collect(node["content"] as JsonArray);
            }
        }
    }
}
