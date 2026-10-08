// <copyright file="AdfNodes.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
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
}
