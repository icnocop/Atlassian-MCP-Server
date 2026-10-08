// <copyright file="LinkToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Links;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Links;

/// <summary>
/// Tests for <see cref="LinkTools"/>.
/// </summary>
[TestClass]
public sealed class LinkToolsTests : IDisposable
{
    private const string LinkTypes = """
        {"issueLinkTypes":[
          {"id":"1","name":"Blocks","inward":"is blocked by","outward":"blocks"},
          {"id":"2","name":"Relates","inward":"relates to","outward":"relates to"}
        ]}
        """;

    private RecordingHttpClient http = null!;
    private JiraMetadataCache metadata = null!;
    private LinkTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.metadata = new JiraMetadataCache();
        this.tools = new LinkTools(new JiraClient(this.http), this.metadata);
    }

    /// <summary>
    /// Disposes the metadata cache.
    /// </summary>
    public void Dispose() => this.metadata.Dispose();

    /// <summary>
    /// Verifies that a link type given by its inward phrase resolves to the type name, without swapping the issues.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithInwardPhrase_ResolvesTypeAndKeepsDirection()
    {
        // Arrange
        this.http.Respond(LinkTypes).Respond(null);

        // Act
        string result = await this.tools.Create("IS BLOCKED BY", "PROJ-1", "PROJ-2");

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("rest/api/3/issueLink", request.Path);
        Assert.AreEqual("Blocks", request.Body!["type"]!["name"]!.GetValue<string>());
        Assert.AreEqual("PROJ-1", request.Body["inwardIssue"]!["key"]!.GetValue<string>());
        Assert.AreEqual("PROJ-2", request.Body["outwardIssue"]!["key"]!.GetValue<string>());
        StringAssert.Contains(result, "Created link: PROJ-1 blocks PROJ-2 (PROJ-2 is blocked by PROJ-1).");
    }

    /// <summary>
    /// Verifies that a link comment is converted to ADF.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithComment_SendsAdfComment()
    {
        // Arrange
        this.http.Respond(LinkTypes).Respond(null);

        // Act
        await this.tools.Create("Relates", "PROJ-1", "PROJ-2", "See *this*");

        // Assert
        Assert.AreEqual("doc", this.http.LastRequest.Body!["comment"]!["body"]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an unknown link type is reported with the available type names.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithUnknownType_ThrowsWithAvailableNames()
    {
        // Arrange
        this.http.Respond(LinkTypes);

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Create("Clones", "PROJ-1", "PROJ-2"));

        // Assert
        StringAssert.Contains(exception.Message, "Blocks, Relates");
        Assert.AreEqual(1, this.http.Requests.Count);
    }

    /// <summary>
    /// Verifies that getting the link types fetches them once and returns them.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetTypes_CalledTwice_FetchesOnce()
    {
        // Arrange
        this.http.Respond(LinkTypes);

        // Act
        await this.tools.GetTypes();
        string result = await this.tools.GetTypes();

        // Assert
        Assert.AreEqual(1, this.http.Requests.Count);
        Assert.AreEqual("rest/api/3/issueLinkType", this.http.LastRequest.Path);
        Assert.AreEqual(2, JsonNode.Parse(result)!.AsArray().Count);
    }

    /// <summary>
    /// Verifies that getting the links of an issue returns its issuelinks field.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_WithIssueKey_ReturnsIssueLinks()
    {
        // Arrange
        this.http.Respond("""{"fields":{"issuelinks":[{"id":"300","type":{"name":"Blocks"}}]}}""");

        // Act
        string result = await this.tools.GetAll("PROJ-1");

        // Assert
        Assert.AreEqual("rest/api/3/issue/PROJ-1?fields=issuelinks", this.http.LastRequest.Path);
        Assert.AreEqual("""[{"id":"300","type":{"name":"Blocks"}}]""", result);
    }

    /// <summary>
    /// Verifies that deleting a link sends a DELETE request.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithLinkId_SendsDeleteRequest()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Delete("300");

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/issueLink/300", this.http.LastRequest.Path);
    }
}
