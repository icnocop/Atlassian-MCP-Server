// <copyright file="CommentToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Http;
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
    /// Verifies that comments are returned as Markdown, and that comments without media need only one request.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_ByDefault_ReturnsMarkdownWithOneRequest()
    {
        // Arrange
        this.http.Respond("""{"comments":[{"id":"1","body":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"Hello"}]}]}}]}""");

        // Act
        JsonNode result = JsonNode.Parse(await this.tools.GetAll("PROJ-1"))!;

        // Assert
        Assert.HasCount(1, this.http.Requests);
        Assert.AreEqual("Hello", result["comments"]![0]!["body"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that comments with embedded files are requested again with the rendered body, that
    /// the files are written as ![name](attachment:ID), and that the rendered body, which was not
    /// asked for, is removed again.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_WithEmbeddedFiles_WritesAttachmentReferences()
    {
        // Arrange
        const string Body = """
            {"type":"doc","version":1,"content":[
              {"type":"paragraph","content":[{"type":"text","text":"See below."}]},
              {"type":"mediaSingle","attrs":{"layout":"align-start"},"content":[{"type":"media","attrs":{"type":"file","id":"11111111-0000-0000-0000-000000000001","alt":"screenshot.png","collection":""}}]},
              {"type":"mediaGroup","content":[{"type":"media","attrs":{"type":"file","id":"11111111-0000-0000-0000-000000000002","collection":""}}]}]}
            """;
        const string Rendered = """
            <p>See below.</p><p><span class="image-wrap"><img src="https://example.atlassian.net/rest/api/3/attachment/content/301" alt="screenshot.png" /></span></p><p><span class="nobr"><a href="/rest/api/3/attachment/content/302" data-attachment-name="logs.zip" data-media-services-id="11111111-0000-0000-0000-000000000002">logs.zip</a></span></p>
            """;
        this.http
            .Respond($$"""{"comments":[{"id":"1","body":{{Body}}}]}""")
            .Respond(new JsonObject
            {
                ["comments"] = new JsonArray(new JsonObject { ["id"] = "1", ["body"] = JsonNode.Parse(Body), ["renderedBody"] = Rendered }),
            }.ToJsonString());

        // Act
        JsonNode result = JsonNode.Parse(await this.tools.GetAll("PROJ-1"))!;

        // Assert
        Assert.AreEqual("rest/api/3/issue/PROJ-1/comment?expand=renderedBody", this.http.LastRequest.Path);
        JsonNode comment = result["comments"]![0]!;
        Assert.AreEqual("See below.\n\n![screenshot.png](attachment:301)\n\n![logs.zip](attachment:302)", comment["body"]!.GetValue<string>());
        Assert.IsNull(comment["renderedBody"]);
    }

    /// <summary>
    /// Verifies that richTextFormat adf returns the comment as Jira sent it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Get_WithAdfFormat_ReturnsTheAdf()
    {
        // Arrange
        this.http.Respond("""{"id":"1","body":{"type":"doc","version":1,"content":[{"type":"mediaGroup","content":[{"type":"media","attrs":{"type":"file","id":"x","collection":""}}]}]}}""");

        // Act
        JsonNode result = JsonNode.Parse(await this.tools.Get("PROJ-1", "1", richTextFormat: "adf"))!;

        // Assert
        Assert.HasCount(1, this.http.Requests);
        Assert.AreEqual("doc", result["body"]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an embedded attachment is looked up, its media ID taken from the media service
    /// URL its content redirects to, and the comment sent with a media node.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithEmbeddedAttachments_SendsMediaNodes()
    {
        // Arrange
        this.http
            .Respond("""{"id":"301","filename":"screenshot.png","mimeType":"image/png"}""")
            .Respond("""{"location":"https://api.media.atlassian.com/file/22222222-0000-0000-0000-000000000001/binary?token=abc&client=def"}""")
            .Respond("""{"id":"302","filename":"logs.zip","mimeType":"application/zip"}""")
            .Respond("""{"location":"https://api.media.atlassian.com/file/22222222-0000-0000-0000-000000000002/binary"}""")
            .Respond("""{"id":"10100"}""");

        // Act
        await this.tools.Add("PROJ-1", "The logs:\n\n![screen](attachment:301)\n\n![](attachment:302)");

        // Assert
        CollectionAssert.AreEqual(
            new[]
            {
                "rest/api/3/attachment/301",
                "rest/api/3/attachment/content/301",
                "rest/api/3/attachment/302",
                "rest/api/3/attachment/content/302",
                "rest/api/3/issue/PROJ-1/comment",
            },
            this.http.Requests.Select(request => request.Path).ToArray());
        JsonArray blocks = this.http.LastRequest.Body!["body"]!["content"]!.AsArray();
        Assert.AreEqual("mediaSingle", blocks[1]!["type"]!.GetValue<string>());
        Assert.AreEqual("22222222-0000-0000-0000-000000000001", blocks[1]!["content"]![0]!["attrs"]!["id"]!.GetValue<string>());
        Assert.AreEqual("screen", blocks[1]!["content"]![0]!["attrs"]!["alt"]!.GetValue<string>());
        Assert.AreEqual("mediaGroup", blocks[2]!["type"]!.GetValue<string>());
        Assert.AreEqual("22222222-0000-0000-0000-000000000002", blocks[2]!["content"]![0]!["attrs"]!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an attachment that does not exist fails the call before the comment is posted.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithMissingAttachment_ThrowsAndPostsNothing()
    {
        // Arrange
        this.http.Fail(new AtlassianApiException(HttpStatusCode.NotFound, "Atlassian returned 404 Not Found."));

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Add("PROJ-1", "![a](attachment:999)"));

        // Assert
        StringAssert.Contains(exception.Message, "Attachment 999", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "atlassian_jira_add_attachment", StringComparison.Ordinal);
        Assert.HasCount(1, this.http.Requests);
    }

    /// <summary>
    /// Verifies that a redirect that does not name a media service file fails the call rather than
    /// embedding the wrong file.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithUnexpectedRedirect_ThrowsAndPostsNothing()
    {
        // Arrange
        this.http
            .Respond("""{"id":"301","filename":"screenshot.png","mimeType":"image/png"}""")
            .Respond("""{"location":"https://example.atlassian.net/rest/api/3/attachment/content/301"}""");

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Add("PROJ-1", "![a](attachment:301)"));

        // Assert
        StringAssert.Contains(exception.Message, "cannot be embedded", StringComparison.Ordinal);
        Assert.HasCount(2, this.http.Requests);
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
    /// Verifies that a mention by name is resolved with a user search, skipping apps and inactive
    /// accounts, and sent as a mention node.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithMentionByName_SearchesAndSendsMentionNode()
    {
        // Arrange
        this.http
            .Respond("""
                [{"accountId":"app-1","displayName":"Jane Doe","accountType":"app","active":true},
                 {"accountId":"old-1","displayName":"Jane Doe","accountType":"atlassian","active":false},
                 {"accountId":"5b10ac8d","displayName":"Jane Doe","accountType":"atlassian","active":true}]
                """)
            .Respond("""{"id":"10100"}""");

        // Act
        await this.tools.Add("PROJ-1", "@[Jane Doe] please review.");

        // Assert
        Assert.HasCount(2, this.http.Requests);
        Assert.AreEqual("rest/api/3/user/search?query=Jane%20Doe&maxResults=50", this.http.Requests[0].Path);
        JsonNode mention = this.http.LastRequest.Body!["body"]!["content"]![0]!["content"]![0]!;
        Assert.AreEqual("mention", mention["type"]!.GetValue<string>());
        Assert.AreEqual("5b10ac8d", mention["attrs"]!["id"]!.GetValue<string>());
        Assert.AreEqual("@Jane Doe", mention["attrs"]!["text"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an ambiguous mention fails the call before the comment is posted.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithAmbiguousMention_ThrowsAndPostsNothing()
    {
        // Arrange
        this.http.Respond("""
            [{"accountId":"1","displayName":"Jane Doe","accountType":"atlassian","active":true},
             {"accountId":"2","displayName":"Jane Smith","accountType":"atlassian","active":true}]
            """);

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Add("PROJ-1", "@[Jane] hi"));

        // Assert
        StringAssert.Contains(exception.Message, "Jane Doe (1), Jane Smith (2)", StringComparison.Ordinal);
        Assert.HasCount(1, this.http.Requests);
        Assert.AreEqual(HttpMethod.Get, this.http.LastRequest.Method);
    }

    /// <summary>
    /// Verifies that a mention with an explicit account ID needs no user search.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithMentionWithAccountId_DoesNotSearch()
    {
        // Arrange
        this.http.Respond("""{"id":"10100"}""");

        // Act
        await this.tools.Add("PROJ-1", "@[Jane Doe](accountid:5b10ac8d) please review.");

        // Assert
        Assert.HasCount(1, this.http.Requests);
        Assert.AreEqual("5b10ac8d", this.http.LastRequest.Body!["body"]!["content"]![0]!["content"]![0]!["attrs"]!["id"]!.GetValue<string>());
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
