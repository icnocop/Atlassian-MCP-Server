// <copyright file="AdfSectionsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Common.Adf;

/// <summary>
/// Tests for <see cref="AdfSections"/>.
/// </summary>
[TestClass]
public sealed class AdfSectionsTests
{
    private const string Page = """
        # Intro

        hello

        ## Setup

        old setup

        ### Detail

        old detail

        ## Usage

        usage text
        """;

    /// <summary>
    /// Verifies that the section runs to the next heading of the same or a higher level, and that
    /// a macro outside the section is kept.
    /// </summary>
    [TestMethod]
    public void Replace_WithSubsection_ReplacesUpToNextSameLevelHeadingAndKeepsMacro()
    {
        // Arrange
        JsonObject document = WithMacro(MarkdownToAdf.Convert(Page));

        // Act
        (JsonObject result, JsonArray removed) = AdfSections.Replace(document, "Setup", MarkdownToAdf.Convert("new setup"), "My Page");

        // Assert
        string markdown = AdfToMarkdown.Convert(result);
        StringAssert.Contains(markdown, "## Setup\n\nnew setup\n\n## Usage", StringComparison.Ordinal);
        Assert.IsFalse(markdown.Contains("old", StringComparison.Ordinal));
        Assert.AreEqual(1, AdfInspector.FindLossyContent(result)["extension"]);
        Assert.AreEqual(3, removed.Count);
    }

    /// <summary>
    /// Verifies that a replacement starting with the same heading replaces the heading too.
    /// </summary>
    [TestMethod]
    public void Replace_WithReplacementStartingWithHeading_ReplacesTheHeading()
    {
        // Arrange
        JsonObject document = MarkdownToAdf.Convert(Page);

        // Act
        (JsonObject result, _) = AdfSections.Replace(document, "## setup", MarkdownToAdf.Convert("### Setup\n\nx"), null);

        // Assert
        string markdown = AdfToMarkdown.Convert(result);
        StringAssert.Contains(markdown, "### Setup\n\nx\n\n## Usage", StringComparison.Ordinal);
        Assert.IsFalse(markdown.Contains("\n## Setup\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that a missing heading is reported with the headings that exist.
    /// </summary>
    [TestMethod]
    public void Replace_WithMissingHeading_ThrowsListingHeadings()
    {
        // Arrange
        JsonObject document = MarkdownToAdf.Convert(Page);

        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(
            () => AdfSections.Replace(document, "Nope", MarkdownToAdf.Convert("x"), null));

        // Assert
        StringAssert.Contains(exception.Message, "## Setup", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a heading that appears twice is rejected as ambiguous.
    /// </summary>
    [TestMethod]
    public void Replace_WithDuplicateHeading_ThrowsAmbiguous()
    {
        // Arrange
        JsonObject document = MarkdownToAdf.Convert("## Notes\n\na\n\n## Notes\n\nb");

        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(
            () => AdfSections.Replace(document, "Notes", MarkdownToAdf.Convert("x"), null));

        // Assert
        StringAssert.Contains(exception.Message, "ambiguous", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the page title is rejected as a section heading.
    /// </summary>
    [TestMethod]
    public void Replace_WithPageTitle_ThrowsPageTitleError()
    {
        // Arrange
        JsonObject document = MarkdownToAdf.Convert(Page);

        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(
            () => AdfSections.Replace(document, "My Page", MarkdownToAdf.Convert("x"), "My Page"));

        // Assert
        StringAssert.Contains(exception.Message, "page title", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a leading level-1 heading whose section covers the whole page is rejected.
    /// </summary>
    [TestMethod]
    public void Replace_WithLeadingH1CoveringWholePage_Throws()
    {
        // Arrange
        JsonObject document = MarkdownToAdf.Convert("# Only\n\ntext\n\n## Sub\n\nmore");

        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(
            () => AdfSections.Replace(document, "Only", MarkdownToAdf.Convert("x"), null));

        // Assert
        StringAssert.Contains(exception.Message, "whole page", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the replaced nodes are returned, so a caller can check them for macros.
    /// </summary>
    [TestMethod]
    public void Replace_WithMacroInsideSection_ReturnsItAsRemoved()
    {
        // Arrange
        JsonObject document = WithMacro(MarkdownToAdf.Convert(Page));

        // Act
        (_, JsonArray removed) = AdfSections.Replace(document, "Usage", MarkdownToAdf.Convert("new"), null);

        // Assert
        Assert.AreEqual(1, AdfInspector.FindLossyContent(removed)["extension"]);
    }

    private static JsonObject WithMacro(JsonObject document)
    {
        document["content"]!.AsArray().Add(JsonNode.Parse("""{"type":"extension","attrs":{"extensionKey":"toc"}}"""));
        return document;
    }
}
