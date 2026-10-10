// <copyright file="AdfInspectorTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;

namespace Atlassian.Mcp.Server.Tests.Common.Adf;

/// <summary>
/// Tests for <see cref="AdfInspector"/>.
/// </summary>
[TestClass]
public sealed class AdfInspectorTests
{
    /// <summary>
    /// Verifies that nested macros, smart links, and media are counted by type.
    /// </summary>
    [TestMethod]
    public void FindLossyContent_WithNestedNodes_CountsEachType()
    {
        // Arrange
        JsonNode document = JsonNode.Parse("""
            {"type":"doc","version":1,"content":[
              {"type":"extension","attrs":{"extensionKey":"toc"}},
              {"type":"paragraph","content":[{"type":"inlineCard","attrs":{"url":"https://x"}}]},
              {"type":"bulletList","content":[{"type":"listItem","content":[
                {"type":"extension","attrs":{"extensionKey":"jira"}}
              ]}]},
              {"type":"mediaSingle","content":[{"type":"media","attrs":{"id":"1"}}]}
            ]}
            """)!;

        // Act
        IReadOnlyDictionary<string, int> counts = AdfInspector.FindLossyContent(document);

        // Assert
        Assert.AreEqual(2, counts["extension"]);
        Assert.AreEqual(1, counts["inlineCard"]);
        Assert.AreEqual(1, counts["mediaSingle"]);
        Assert.AreEqual(1, counts["media"]);
    }

    /// <summary>
    /// Verifies that a document with only plain content has nothing lossy.
    /// </summary>
    [TestMethod]
    public void FindLossyContent_WithPlainDocument_ReturnsEmpty()
    {
        // Arrange
        JsonObject document = MarkdownToAdf.Convert("# Title\n\n- a\n- b\n\n**bold** `code`");

        // Act
        IReadOnlyDictionary<string, int> counts = AdfInspector.FindLossyContent(document);

        // Assert
        Assert.AreEqual(0, counts.Count);
    }

    /// <summary>
    /// Verifies that a mention, which Markdown writes back as the same mention, is not lossy.
    /// </summary>
    [TestMethod]
    public void FindLossyContent_WithMention_ReturnsEmpty()
    {
        // Arrange
        JsonObject document = MarkdownToAdf.Convert("Ask @[Jane](accountid:abc)");

        // Act
        IReadOnlyDictionary<string, int> counts = AdfInspector.FindLossyContent(document);

        // Assert
        Assert.AreEqual(0, counts.Count);
    }

    /// <summary>
    /// Verifies that the description lists each count and type.
    /// </summary>
    [TestMethod]
    public void Describe_WithCounts_ListsCountAndType()
    {
        // Arrange
        var counts = new Dictionary<string, int> { ["extension"] = 2, ["inlineCard"] = 1 };

        // Act
        string description = AdfInspector.Describe(counts);

        // Assert
        Assert.AreEqual("2 extension, 1 inlineCard", description);
    }
}
