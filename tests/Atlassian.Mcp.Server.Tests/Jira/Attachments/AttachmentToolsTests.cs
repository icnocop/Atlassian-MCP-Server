// <copyright file="AttachmentToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Attachments;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Attachments;

/// <summary>
/// Tests for <see cref="AttachmentTools"/>.
/// </summary>
[TestClass]
public sealed class AttachmentToolsTests
{
    private RecordingHttpClient http = null!;
    private AttachmentTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new AttachmentTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that getting all attachments requests only the attachment field and returns its value.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_WithIssueKey_ReturnsTheAttachmentField()
    {
        // Arrange
        this.http.Respond("""{"fields":{"attachment":[{"id":"10001","filename":"log.txt","self":"https://x"}]}}""");

        // Act
        string result = await this.tools.GetAll("PROJ-1", CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/issue/PROJ-1?fields=attachment", this.http.LastRequest.Path);
        Assert.AreEqual("""[{"id":"10001","filename":"log.txt"}]""", result);
    }

    /// <summary>
    /// Verifies that adding an attachment decodes the content and uploads it to the issue.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithBase64Content_UploadsTheDecodedBytes()
    {
        // Arrange
        this.http.Respond("""[{"id":"10002","filename":"notes.txt"}]""");
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello"));

        // Act
        await this.tools.Add("PROJ-1", "notes.txt", base64, CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("rest/api/3/issue/PROJ-1/attachments", request.Path);
        Assert.AreEqual("notes.txt", request.FileName);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(request.Content!));
    }

    /// <summary>
    /// Verifies that a data URL prefix is removed before decoding.
    /// </summary>
    [TestMethod]
    public void DecodeBase64_WithDataUrlPrefix_DecodesTheContent()
    {
        // Act
        byte[] bytes = AttachmentTools.DecodeBase64("data:text/plain;base64," + Convert.ToBase64String([1, 2, 3]));

        // Assert
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, bytes);
    }

    /// <summary>
    /// Verifies that content that is not base64 is reported as an MCP error.
    /// </summary>
    [TestMethod]
    public void DecodeBase64_WithInvalidContent_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => AttachmentTools.DecodeBase64("not base64!"));
    }

    /// <summary>
    /// Verifies that a text attachment is returned as text.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetContent_WithTextAttachment_ReturnsText()
    {
        // Arrange
        this.http.Respond($$"""{"mediaType":"text/plain","base64":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes("line 1"))}}"}""");

        // Act
        string result = await this.tools.GetContent("10001", CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/attachment/content/10001", this.http.LastRequest.Path);
        Assert.AreEqual("line 1", JsonNode.Parse(result)!["text"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that deleting an attachment sends a DELETE request and confirms it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithAttachmentId_SendsDeleteRequest()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        string result = await this.tools.Delete("10001", CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/attachment/10001", this.http.LastRequest.Path);
        StringAssert.Contains(result, "Deleted attachment 10001.");
    }
}
