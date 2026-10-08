// <copyright file="SpaceToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Confluence;
using Atlassian.Mcp.Server.Confluence.Spaces;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Confluence.Spaces;

/// <summary>
/// Tests for <see cref="SpaceTools"/>.
/// </summary>
[TestClass]
public sealed class SpaceToolsTests
{
    private RecordingHttpClient http = null!;
    private ConfluenceClient confluence = null!;

    /// <summary>
    /// Creates the client with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.confluence = new ConfluenceClient(this.http);
    }

    /// <summary>
    /// Verifies that a numeric value is taken as a space ID without a request.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ResolveIdAsync_WithNumericValue_ReturnsItWithoutRequest()
    {
        // Act
        string id = await SpaceTools.ResolveIdAsync(this.confluence, "123", CancellationToken.None);

        // Assert
        Assert.AreEqual("123", id);
        Assert.AreEqual(0, this.http.Requests.Count);
    }

    /// <summary>
    /// Verifies that a space key is resolved when the response holds that exact key.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ResolveIdAsync_WithMatchingKey_ReturnsTheId()
    {
        // Arrange
        this.http.Respond("""{"results":[{"id":"9","key":"DOCS"}]}""");

        // Act
        string id = await SpaceTools.ResolveIdAsync(this.confluence, "docs", CancellationToken.None);

        // Assert
        Assert.AreEqual("9", id);
        Assert.AreEqual("wiki/api/v2/spaces?keys=docs", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that a response holding a different space is not taken as a match, because the API
    /// ignores keys it does not recognize.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ResolveIdAsync_WithDifferentKeyInResponse_ThrowsMcpException()
    {
        // Arrange
        this.http.Respond("""{"results":[{"id":"1","key":"~someone.else"}]}""");

        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => SpaceTools.ResolveIdAsync(this.confluence, "~me", CancellationToken.None));
    }
}
