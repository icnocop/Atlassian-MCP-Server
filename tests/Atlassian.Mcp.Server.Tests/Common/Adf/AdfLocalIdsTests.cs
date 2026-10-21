// <copyright file="AdfLocalIdsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;

namespace Atlassian.Mcp.Server.Tests.Common.Adf;

/// <summary>
/// Tests for <see cref="AdfLocalIds"/>.
/// </summary>
[TestClass]
public sealed class AdfLocalIdsTests
{
    private const string Page = """
        # Goal

        Create the repositories.

        | Repository | Branch |
        | --- | --- |
        | `DevOps` | `main` |

        - **One** item
        - Two
        """;

    /// <summary>
    /// Verifies that converting the same Markdown again gives every node the ID it had, although
    /// Confluence returns the nodes with their properties in another order and with default
    /// table cell attributes.
    /// </summary>
    [TestMethod]
    public void Preserve_WithUnchangedMarkdown_CopiesEveryId()
    {
        // Arrange
        JsonObject previous = AdfNodes.AsStoredByConfluence(MarkdownToAdf.Convert(Page));
        JsonObject updated = MarkdownToAdf.Convert(Page);

        // Act
        int copied = AdfLocalIds.Preserve(previous, updated);

        // Assert
        List<string> ids = AdfNodes.Ids(previous);
        Assert.AreEqual(ids.Count, copied);
        CollectionAssert.AreEqual(ids, AdfNodes.Ids(updated));
    }

    /// <summary>
    /// Verifies that an edited paragraph keeps its ID, so that Confluence compares it as a text change.
    /// </summary>
    [TestMethod]
    public void Preserve_WithEditedParagraph_KeepsItsId()
    {
        // Arrange
        JsonObject previous = AdfNodes.AsStoredByConfluence(MarkdownToAdf.Convert("# A\n\none\n\ntwo"));
        JsonObject updated = MarkdownToAdf.Convert("# A\n\none, edited\n\ntwo");

        // Act
        AdfLocalIds.Preserve(previous, updated);

        // Assert
        CollectionAssert.AreEqual(AdfNodes.Ids(previous), AdfNodes.Ids(updated));
    }

    /// <summary>
    /// Verifies that an inserted paragraph gets no ID, for Confluence to assign, and that the nodes
    /// around it keep theirs.
    /// </summary>
    [TestMethod]
    public void Preserve_WithInsertedParagraph_LeavesItWithoutId()
    {
        // Arrange
        JsonObject previous = AdfNodes.AsStoredByConfluence(MarkdownToAdf.Convert("# A\n\none\n\ntwo"));
        JsonObject updated = MarkdownToAdf.Convert("# A\n\none\n\ninserted\n\ntwo");

        // Act
        AdfLocalIds.Preserve(previous, updated);

        // Assert
        JsonArray content = updated["content"]!.AsArray();
        Assert.AreEqual("id1", IdOf(content[0]));
        Assert.AreEqual("id2", IdOf(content[1]));
        Assert.IsNull(IdOf(content[2]));
        Assert.AreEqual("id3", IdOf(content[3]));
    }

    /// <summary>
    /// Verifies that editing one table cell keeps the IDs of the table, its rows, and its cells.
    /// </summary>
    [TestMethod]
    public void Preserve_WithEditedTableCell_KeepsTableRowAndCellIds()
    {
        // Arrange
        JsonObject previous = AdfNodes.AsStoredByConfluence(MarkdownToAdf.Convert(Page));
        JsonObject updated = MarkdownToAdf.Convert(Page.Replace("`main`", "`dev`", StringComparison.Ordinal));

        // Act
        AdfLocalIds.Preserve(previous, updated);

        // Assert
        CollectionAssert.AreEqual(AdfNodes.Ids(previous), AdfNodes.Ids(updated));
    }

    /// <summary>
    /// Verifies that an ID that the new body already has is neither replaced nor given to a second node.
    /// </summary>
    [TestMethod]
    public void Preserve_WithIdAlreadyInUpdated_KeepsItAndDoesNotRepeatIt()
    {
        // Arrange
        JsonObject previous = AdfNodes.AsStoredByConfluence(MarkdownToAdf.Convert("one\n\ntwo"));
        JsonObject updated = MarkdownToAdf.Convert("one\n\ntwo");
        updated["content"]![1]!["attrs"] = new JsonObject { ["localId"] = "id1" };

        // Act
        AdfLocalIds.Preserve(previous, updated);

        // Assert
        JsonArray content = updated["content"]!.AsArray();
        Assert.IsNull(IdOf(content[0]));
        Assert.AreEqual("id1", IdOf(content[1]));
    }

    private static string? IdOf(JsonNode? node) => node?["attrs"]?["localId"]?.GetValue<string>();
}
