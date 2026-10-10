// <copyright file="RenderedAttachmentsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Jira;

namespace Atlassian.Mcp.Server.Tests.Jira;

/// <summary>
/// Tests for <see cref="RenderedAttachments"/>. The comments are shaped like the ones Jira Cloud
/// returns, with invented names, IDs, and files.
/// </summary>
[TestClass]
public sealed class RenderedAttachmentsTests
{
    /// <summary>
    /// Verifies that files rendered as links are matched by the media ID that the link names.
    /// </summary>
    [TestMethod]
    public void Find_WithFileLinks_MatchesByMediaId()
    {
        // Arrange
        const string Blocks = """
            {"type":"mediaGroup","content":[
              {"type":"media","attrs":{"type":"file","id":"aaaaaaaa-0000-0000-0000-000000000001","collection":""}},
              {"type":"media","attrs":{"type":"file","id":"aaaaaaaa-0000-0000-0000-000000000002","collection":""}}]}
            """;
        const string Html = """
            <p><span class="nobr"><a href="/rest/api/3/attachment/content/502" title="logs.zip attached to PROJ-1" data-attachment-type="file" data-attachment-name="logs.zip" data-media-services-type="file" data-media-services-id="aaaaaaaa-0000-0000-0000-000000000002" rel="noreferrer">logs.zip<sup><img class="rendericon" src="/images/icons/link_attachment_7.gif" height="7" width="7" alt="" border="0"/></sup></a></span><br/>
            <span class="nobr"><a href="/rest/api/3/attachment/content/501" title="Jane &amp; Bob.zip attached to PROJ-1" data-attachment-name="Jane &amp; Bob.zip" data-media-services-id="aaaaaaaa-0000-0000-0000-000000000001" rel="noreferrer">Jane &amp; Bob.zip</a></span></p>
            """;
        JsonObject comment = Comment(Blocks, Html);

        // Act
        IReadOnlyDictionary<string, AttachmentReference> attachments = RenderedAttachments.Find(comment);

        // Assert
        Assert.AreEqual(new AttachmentReference("501", "Jane & Bob.zip"), attachments["aaaaaaaa-0000-0000-0000-000000000001"]);
        Assert.AreEqual(new AttachmentReference("502", "logs.zip"), attachments["aaaaaaaa-0000-0000-0000-000000000002"]);
    }

    /// <summary>
    /// Verifies that images, which Jira renders without the media ID, are matched by their
    /// alternative text even when the attachment IDs are not in the same order as the media nodes.
    /// </summary>
    [TestMethod]
    public void Find_WithImagesThatHaveAltText_MatchesByAltText()
    {
        // Arrange
        const string Blocks = """
            {"type":"mediaSingle","attrs":{"layout":"align-start"},"content":[{"type":"media","attrs":{"type":"file","id":"bbbbbbbb-0000-0000-0000-000000000001","alt":"first.png","collection":""}}]},
            {"type":"mediaSingle","attrs":{"layout":"align-start"},"content":[{"type":"media","attrs":{"type":"file","id":"bbbbbbbb-0000-0000-0000-000000000002","alt":"second.png","collection":""}}]}
            """;
        const string Html = """
            <p><span class="image-wrap" style=""><img src="https://example.atlassian.net/rest/api/3/attachment/content/602" alt="second.png" width="469" /></span></p>
            <p><span class="image-wrap" style=""><img src="https://example.atlassian.net/rest/api/3/attachment/content/601" alt="first.png" width="558" /></span></p>
            """;
        JsonObject comment = Comment(Blocks, Html);

        // Act
        IReadOnlyDictionary<string, AttachmentReference> attachments = RenderedAttachments.Find(comment);

        // Assert
        Assert.AreEqual("601", attachments["bbbbbbbb-0000-0000-0000-000000000001"].AttachmentId);
        Assert.AreEqual("602", attachments["bbbbbbbb-0000-0000-0000-000000000002"].AttachmentId);
    }

    /// <summary>
    /// Verifies that images without alternative text are matched by position when the unmatched
    /// media nodes and images are equal in number.
    /// </summary>
    [TestMethod]
    public void Find_WithImagesWithoutAltText_MatchesByPosition()
    {
        // Arrange
        const string Blocks = """
            {"type":"mediaSingle","content":[{"type":"media","attrs":{"type":"file","id":"cccccccc-0000-0000-0000-000000000001","alt":"","collection":""}}]},
            {"type":"mediaSingle","content":[{"type":"media","attrs":{"type":"file","id":"cccccccc-0000-0000-0000-000000000002","collection":""}}]}
            """;
        const string Html = """
            <p><img src="/rest/api/3/attachment/content/702" alt="" /></p><p><img src="/rest/api/3/attachment/content/701" /></p>
            """;
        JsonObject comment = Comment(Blocks, Html);

        // Act
        IReadOnlyDictionary<string, AttachmentReference> attachments = RenderedAttachments.Find(comment);

        // Assert
        Assert.AreEqual("702", attachments["cccccccc-0000-0000-0000-000000000001"].AttachmentId);
        Assert.AreEqual("701", attachments["cccccccc-0000-0000-0000-000000000002"].AttachmentId);
    }

    /// <summary>
    /// Verifies that a file Jira could not render is left unmatched rather than guessed, while the
    /// other files of the same comment are still matched.
    /// </summary>
    [TestMethod]
    public void Find_WithFileJiraCouldNotRender_LeavesItUnmatched()
    {
        // Arrange
        const string Blocks = """
            {"type":"mediaGroup","content":[{"type":"media","attrs":{"type":"file","id":"dddddddd-0000-0000-0000-000000000001","collection":""}}]},
            {"type":"mediaSingle","attrs":{"layout":"align-start"},"content":[{"type":"media","attrs":{"type":"file","id":"dddddddd-0000-0000-0000-000000000002","collection":"","height":1080,"width":1920}}]}
            """;
        const string Html = """
            <p><a href="/rest/api/3/attachment/content/801" data-attachment-name="logs.zip" data-media-services-id="dddddddd-0000-0000-0000-000000000001">logs.zip</a></p>
            <p><span class="error">Unable to render embedded object: File (demo.mp4) not found.</span></p>
            """;
        JsonObject comment = Comment(Blocks, Html);

        // Act
        IReadOnlyDictionary<string, AttachmentReference> attachments = RenderedAttachments.Find(comment);

        // Assert
        Assert.HasCount(1, attachments);
        Assert.AreEqual("801", attachments["dddddddd-0000-0000-0000-000000000001"].AttachmentId);
    }

    /// <summary>
    /// Verifies that an issue's fields and comments are paired with the same entries of renderedFields.
    /// </summary>
    [TestMethod]
    public void Find_WithIssueAndRenderedFields_MatchesDescriptionAndComments()
    {
        // Arrange
        JsonNode issue = JsonNode.Parse("""
            {"key":"PROJ-1",
             "fields":{
               "description":{"type":"doc","version":1,"content":[{"type":"mediaSingle","content":[{"type":"media","attrs":{"type":"file","id":"eeeeeeee-0000-0000-0000-000000000001","alt":"a.png","collection":""}}]}]},
               "comment":{"comments":[{"id":"1","body":{"type":"doc","version":1,"content":[{"type":"mediaSingle","content":[{"type":"media","attrs":{"type":"file","id":"eeeeeeee-0000-0000-0000-000000000002","alt":"b.png","collection":""}}]}]}}]}},
             "renderedFields":{
               "description":"<p><img src=\"/rest/api/3/attachment/content/901\" alt=\"a.png\" /></p>",
               "comment":{"comments":[{"id":"1","body":"<p><img src=\"/rest/api/3/attachment/content/902\" alt=\"b.png\" /></p>"}]}}}
            """)!;

        // Act
        IReadOnlyDictionary<string, AttachmentReference> attachments = RenderedAttachments.Find(issue);

        // Assert
        Assert.AreEqual("901", attachments["eeeeeeee-0000-0000-0000-000000000001"].AttachmentId);
        Assert.AreEqual("902", attachments["eeeeeeee-0000-0000-0000-000000000002"].AttachmentId);
    }

    /// <summary>
    /// Verifies that media nodes are found at any depth, and that text without them has none.
    /// </summary>
    [TestMethod]
    public void HasMedia_WithAndWithoutMedia_ReportsIt()
    {
        // Arrange
        JsonObject withMedia = Comment("""{"type":"mediaGroup","content":[{"type":"media","attrs":{"id":"x"}}]}""", "<p></p>");
        JsonObject withoutMedia = Comment("""{"type":"paragraph","content":[{"type":"text","text":"media"}]}""", "<p>media</p>");

        // Act and assert
        Assert.IsTrue(RenderedAttachments.HasMedia(withMedia));
        Assert.IsFalse(RenderedAttachments.HasMedia(withoutMedia));
    }

    private static JsonObject Comment(string blocks, string renderedBody)
        => new JsonObject
        {
            ["id"] = "10001",
            ["body"] = JsonNode.Parse($$"""{"type":"doc","version":1,"content":[{{blocks}}]}"""),
            ["renderedBody"] = renderedBody,
        };
}
