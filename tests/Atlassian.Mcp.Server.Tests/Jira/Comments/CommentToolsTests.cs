// <copyright file="CommentToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Comments;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Comments;

/// <summary>
/// Tests for <see cref="CommentTools"/>.
/// </summary>
[TestClass]
public sealed class CommentToolsTests
{
    private RecordingHttpClient http = null!;
    private CommentTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new CommentTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that getting comments passes paging, order, and the rendered-body expansion.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_WithPagingAndRenderedBody_SendsQueryString()
    {
        // Arrange
        this.http.Respond("""{"comments":[],"total":0}""");

        // Act
        await this.tools.GetAll("PROJ-1", startAt: 10, maxResults: 5, orderBy: "-created", includeRenderedBody: true);

        // Assert
        Assert.AreEqual(
            "rest/api/3/issue/PROJ-1/comment?startAt=10&maxResults=5&orderBy=-created&expand=renderedBody",
            this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that adding a comment converts the Markdown body to ADF.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithMarkdownBody_SendsAdfDocument()
    {
        // Arrange
        this.http.Respond("""{"id":"10100"}""");

        // Act
        string result = await this.tools.Add("PROJ-1", "Fixed in **build 42**.");

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/comment", request.Path);
        JsonNode body = request.Body!["body"]!;
        Assert.AreEqual("doc", body["type"]!.GetValue<string>());
        Assert.AreEqual("strong", body["content"]![0]!["content"]![1]!["marks"]![0]!["type"]!.GetValue<string>());
        Assert.IsNull(request.Body["visibility"]);
        Assert.AreEqual("""{"id":"10100"}""", result);
    }

    /// <summary>
    /// Verifies that a role restriction is sent as the comment visibility.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithRoleVisibility_SendsVisibility()
    {
        // Arrange
        this.http.Respond("""{"id":"10100"}""");

        // Act
        await this.tools.Add("PROJ-1", "Internal note", visibilityType: "Role", visibilityValue: "Developers");

        // Assert
        JsonNode visibility = this.http.LastRequest.Body!["visibility"]!;
        Assert.AreEqual("role", visibility["type"]!.GetValue<string>());
        Assert.AreEqual("Developers", visibility["value"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a visibility type without a value is rejected.
    /// </summary>
    [TestMethod]
    public void BuildBody_WithTypeButNoValue_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => CommentTools.BuildBody("text", "group", null));
    }

    /// <summary>
    /// Verifies that an unknown visibility type is rejected.
    /// </summary>
    [TestMethod]
    public void BuildBody_WithUnknownType_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => CommentTools.BuildBody("text", "user", "someone"));
    }

    /// <summary>
    /// Verifies that updating a comment sends a PUT request to the comment.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithBody_SendsPutRequest()
    {
        // Arrange
        this.http.Respond("""{"id":"10100"}""");

        // Act
        await this.tools.Update("PROJ-1", "10100", "Updated");

        // Assert
        Assert.AreEqual(HttpMethod.Put, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/comment/10100", this.http.LastRequest.Path);
        Assert.AreEqual("doc", this.http.LastRequest.Body!["body"]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that deleting a comment sends a DELETE request and confirms it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithCommentId_SendsDeleteRequest()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        string result = await this.tools.Delete("PROJ-1", "10100");

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/comment/10100", this.http.LastRequest.Path);
        StringAssert.Contains(result, "Deleted comment 10100 from PROJ-1.");
    }
}
