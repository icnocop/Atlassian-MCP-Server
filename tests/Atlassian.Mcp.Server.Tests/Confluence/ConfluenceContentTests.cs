// <copyright file="ConfluenceContentTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Confluence;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Confluence;

/// <summary>
/// Tests for <see cref="ConfluenceContent"/>.
/// </summary>
[TestClass]
public sealed class ConfluenceContentTests
{
    /// <summary>
    /// Verifies that the body format names and their aliases are normalized.
    /// </summary>
    [TestMethod]
    public void NormalizeFormat_WithAliases_ReturnsCanonicalNames()
    {
        // Act and assert
        Assert.AreEqual("markdown", ConfluenceContent.NormalizeFormat(null));
        Assert.AreEqual("markdown", ConfluenceContent.NormalizeFormat("MD"));
        Assert.AreEqual("adf", ConfluenceContent.NormalizeFormat("atlas_doc_format"));
        Assert.AreEqual("storage", ConfluenceContent.NormalizeFormat("XHTML"));
    }

    /// <summary>
    /// Verifies that an unknown body format is rejected.
    /// </summary>
    [TestMethod]
    public void NormalizeFormat_WithUnknownFormat_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => ConfluenceContent.NormalizeFormat("wiki"));
    }

    /// <summary>
    /// Verifies that Markdown is sent as an ADF document serialized to a JSON string.
    /// </summary>
    [TestMethod]
    public void ToRequestBody_WithMarkdown_ReturnsAdfJsonString()
    {
        // Act
        JsonObject body = ConfluenceContent.ToRequestBody("# Title", "markdown");

        // Assert
        Assert.AreEqual("atlas_doc_format", body["representation"]!.GetValue<string>());
        JsonNode document = JsonNode.Parse(body["value"]!.GetValue<string>())!;
        Assert.AreEqual("heading", document["content"]![0]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that ADF that is not a document is rejected.
    /// </summary>
    [TestMethod]
    public void ToRequestBody_WithInvalidAdf_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => ConfluenceContent.ToRequestBody("""{"type":"paragraph"}""", "adf"));
    }

    /// <summary>
    /// Verifies that valid ADF and storage bodies are passed through unchanged.
    /// </summary>
    [TestMethod]
    public void ToRequestBody_WithAdfAndStorage_PassesThrough()
    {
        // Arrange
        const string adf = """{"type":"doc","version":1,"content":[]}""";

        // Act
        JsonObject adfBody = ConfluenceContent.ToRequestBody(adf, "adf");
        JsonObject storageBody = ConfluenceContent.ToRequestBody("<p>Hi</p>", "storage");

        // Assert
        Assert.AreEqual(adf, adfBody["value"]!.GetValue<string>());
        Assert.AreEqual("storage", storageBody["representation"]!.GetValue<string>());
        Assert.AreEqual("<p>Hi</p>", storageBody["value"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that the cursor is read from the next link, and is missing on the last page.
    /// </summary>
    [TestMethod]
    public void NextCursor_WithNextLink_ReturnsDecodedCursor()
    {
        // Arrange
        JsonNode response = JsonNode.Parse("""{"_links":{"next":"/wiki/api/v2/pages?cursor=abc%3D&limit=25"}}""")!;

        // Act
        string? cursor = ConfluenceContent.NextCursor(response);

        // Assert
        Assert.AreEqual("abc=", cursor);
        Assert.IsNull(ConfluenceContent.NextCursor(JsonNode.Parse("""{"results":[]}""")));
    }
}
