// <copyright file="PageToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Confluence;
using Atlassian.Mcp.Server.Confluence.Pages;
using Atlassian.Mcp.Server.Tests.Common.Adf;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Confluence.Pages;

/// <summary>
/// Tests for <see cref="PageTools"/>.
/// </summary>
[TestClass]
public sealed class PageToolsTests
{
    private const string Macro = """{"type":"extension","attrs":{"extensionKey":"toc","extensionType":"com.atlassian.confluence.macro.core"}}""";

    private RecordingHttpClient http = null!;
    private PageTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new PageTools(new ConfluenceClient(this.http));
    }

    /// <summary>
    /// Verifies that embedding an attachment, which is supported for Jira only, is refused before anything is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithEmbeddedAttachment_ThrowsAndSendsNothing()
    {
        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.tools.Create("T", "![logs.zip](attachment:101)", spaceKey: "9", publish: true));

        // Assert
        StringAssert.Contains(exception.Message, "supported in Jira only", StringComparison.Ordinal);
        Assert.HasCount(0, this.http.Requests);
    }

    /// <summary>
    /// Verifies that a mention by name in a Markdown body is resolved with a Confluence user search
    /// and sent as a mention node.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithMentionByName_SearchesAndSendsMentionNode()
    {
        // Arrange
        this.http
            .Respond("""{"results":[{"user":{"accountId":"5b10ac8d","displayName":"Jane \"JD\" Doe","accountType":"atlassian"}}]}""")
            .Respond("""{"id":"1","title":"T","status":"current"}""");

        // Act
        await this.tools.Create("T", "Owner: @[Jane \"JD\" Doe]", spaceKey: "9", publish: true);

        // Assert
        Assert.AreEqual(
            "wiki/rest/api/search/user?cql=user.fullname~%22Jane%20%5C%22JD%5C%22%20Doe%22&limit=50",
            this.http.Requests[0].Path);
        JsonNode adf = JsonNode.Parse(this.http.LastRequest.Body!["body"]!["value"]!.GetValue<string>())!;
        JsonNode mention = adf["content"]![0]!["content"]![1]!;
        Assert.AreEqual("mention", mention["type"]!.GetValue<string>());
        Assert.AreEqual("5b10ac8d", mention["attrs"]!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a section of a page that has a mention can be replaced, because a mention is not lossy.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task UpdateSection_WithMentionInSection_ReplacesIt()
    {
        // Arrange
        string page = PageJson("current", 4, "## Owner\n\n@[Jane](accountid:a)\n\n## Notes\n\n@[Eve](accountid:e)");
        this.http.Respond(page).Respond(page).Respond(PageJson("draft", 1, "x"));

        // Act
        await this.tools.UpdateSection("1", "Owner", "@[Bob](accountid:b)");

        // Assert
        JsonNode document = JsonNode.Parse(this.http.LastRequest.Body!["body"]!["value"]!.GetValue<string>())!;
        Assert.AreEqual("## Owner\n\n@[Bob](accountid:b)\n\n## Notes\n\n@[Eve](accountid:e)", AdfToMarkdown.Convert(document));
    }

    /// <summary>
    /// Verifies that appending a page to its own parent is refused instead of reported as moved,
    /// because Confluence leaves the page where it is.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Move_AppendToCurrentParent_ThrowsAndDoesNotMove()
    {
        // Arrange
        this.http.Respond("""{"id":"5","title":"T","status":"current","parentId":"1"}""");

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Move("5", "1"));

        // Assert
        StringAssert.Contains(exception.Message, "before or after a sibling page", StringComparison.Ordinal);
        Assert.HasCount(1, this.http.Requests);
    }

    /// <summary>
    /// Verifies that appending a page to another parent moves it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Move_AppendToOtherParent_MovesPage()
    {
        // Arrange
        this.http.Respond("""{"id":"5","title":"T","status":"current","parentId":"2"}""").Respond("{}");

        // Act
        await this.tools.Move("5", "1");

        // Assert
        StringAssert.EndsWith(this.http.LastRequest.Path, "content/5/move/append/1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that moving a page after a sibling sends the move without looking up the page.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Move_AfterSibling_MovesWithoutLookingUpPage()
    {
        // Arrange
        this.http.Respond("{}");

        // Act
        await this.tools.Move("5", "6", "after");

        // Assert
        Assert.HasCount(1, this.http.Requests);
        StringAssert.EndsWith(this.http.LastRequest.Path, "content/5/move/after/6", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a new page is a private draft by default.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_ByDefault_SavesPrivateDraft()
    {
        // Arrange
        this.http.Respond("""{"results":[{"id":"9","key":"DOCS"}]}""").Respond("""{"id":"1","title":"T","status":"draft","_links":{"webui":"/pages/resumedraft.action?draftId=1"}}""");

        // Act
        string result = await this.tools.Create("T", "# Hello", spaceKey: "DOCS");

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("wiki/api/v2/pages?private=true", request.Path);
        Assert.AreEqual("draft", request.Body!["status"]!.GetValue<string>());
        Assert.AreEqual("9", request.Body["spaceId"]!.GetValue<string>());
        Assert.AreEqual("atlas_doc_format", request.Body["body"]!["representation"]!.GetValue<string>());
        JsonNode created = JsonNode.Parse(result)!;
        Assert.IsTrue(created["private"]!.GetValue<bool>());
        Assert.AreEqual("https://example.atlassian.net/wiki/pages/resumedraft.action?draftId=1", created["url"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that publishing a new page sends it as current and not private.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithPublish_SendsCurrentWithoutPrivate()
    {
        // Arrange
        this.http.Respond("""{"id":"1","title":"T","status":"current"}""");

        // Act
        await this.tools.Create("T", "Hello", spaceKey: "9", publish: true);

        // Assert
        Assert.AreEqual(1, this.http.Requests.Count);
        Assert.AreEqual("wiki/api/v2/pages", this.http.LastRequest.Path);
        Assert.AreEqual("current", this.http.LastRequest.Body!["status"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a page published as private is created with the private flag.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithPublishAndPrivate_SendsPrivate()
    {
        // Arrange
        this.http.Respond("""{"id":"1","title":"T","status":"current"}""");

        // Act
        string result = await this.tools.Create("T", "Hello", spaceKey: "9", publish: true, restrictions: "private");

        // Assert
        Assert.AreEqual("wiki/api/v2/pages?private=true", this.http.LastRequest.Path);
        Assert.AreEqual("private", JsonNode.Parse(result)!["restrictions"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an edit-restricted page is created without the private flag, then restricted so
    /// that only the current user can edit it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithEditRestricted_RestrictsUpdateToCurrentUser()
    {
        // Arrange
        this.http
            .Respond("""{"id":"1","title":"T","status":"current"}""")
            .Respond("""{"accountId":"me"}""")
            .Respond("{}");

        // Act
        await this.tools.Create("T", "Hello", spaceKey: "9", publish: true, restrictions: "edit-restricted");

        // Assert
        Assert.AreEqual("wiki/api/v2/pages", this.http.Requests[0].Path);
        RecordedRequest restriction = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Put, restriction.Method);
        StringAssert.EndsWith(restriction.Path, "content/1/restriction", StringComparison.Ordinal);
        JsonArray operations = restriction.Body!.AsArray();
        Assert.HasCount(2, operations);
        Assert.AreEqual("read", operations[0]!["operation"]!.GetValue<string>());
        Assert.HasCount(0, operations[0]!["restrictions"]!["user"]!.AsArray());
        Assert.AreEqual("update", operations[1]!["operation"]!.GetValue<string>());
        Assert.AreEqual("me", operations[1]!["restrictions"]!["user"]![0]!["accountId"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an unknown restrictions value is refused before anything is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithUnknownRestrictions_ThrowsAndSendsNothing()
    {
        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.tools.Create("T", "Hello", spaceKey: "9", restrictions: "public"));

        // Assert
        StringAssert.Contains(exception.Message, "open, edit-restricted, or private", StringComparison.Ordinal);
        Assert.HasCount(0, this.http.Requests);
    }

    /// <summary>
    /// Verifies that an update with restrictions private restricts viewing and editing to the current user.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithPrivate_RestrictsReadAndUpdateToCurrentUser()
    {
        // Arrange
        this.http
            .Respond(PageJson("current", 4, "old"))
            .Respond(PageJson("current", 4, "old"))
            .Respond(PageJson("draft", 1, "new"))
            .Respond("""{"accountId":"me"}""")
            .Respond("{}");

        // Act
        await this.tools.Update("1", body: "new", restrictions: "private");

        // Assert
        JsonArray operations = this.http.LastRequest.Body!.AsArray();
        CollectionAssert.AreEqual(
            new[] { "read", "update" },
            operations.Select(operation => operation!["operation"]!.GetValue<string>()).ToArray());
    }

    /// <summary>
    /// Verifies that an update without restrictions leaves them unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithoutRestrictions_DoesNotChangeThem()
    {
        // Arrange
        this.http.Respond(PageJson("current", 4, "old")).Respond(PageJson("current", 4, "old")).Respond(PageJson("draft", 1, "new"));

        // Act
        await this.tools.Update("1", body: "new");

        // Assert
        Assert.HasCount(3, this.http.Requests);
        Assert.IsFalse(this.http.Requests.Any(request => request.Path.Contains("restriction", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies that an update is saved as a draft with version 1 and the default message.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_ByDefault_SavesDraftWithVersionOne()
    {
        // Arrange
        this.http.Respond(PageJson("current", 4, "old")).Respond(PageJson("current", 4, "old")).Respond(PageJson("draft", 1, "new"));

        // Act
        await this.tools.Update("1", body: "new");

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Put, request.Method);
        Assert.AreEqual("wiki/api/v2/pages/1", request.Path);
        Assert.AreEqual("draft", request.Body!["status"]!.GetValue<string>());
        Assert.AreEqual(1, request.Body["version"]!["number"]!.GetValue<int>());
        Assert.AreEqual(PageTools.DefaultVersionMessage, request.Body["version"]!["message"]!.GetValue<string>());
        Assert.AreEqual("Page", request.Body["title"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a page with a smart link can be updated with Markdown, because the smart link is read
    /// as an autolink and written back as the same smart link.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithMarkdownOverSmartLink_WritesAndKeepsIt()
    {
        // Arrange
        string page = PageJson("current", 4, "See <https://example.atlassian.net/wiki/spaces/X/pages/1>.");
        this.http.Respond(page).Respond(page).Respond(PageJson("draft", 1, "x"));

        // Act
        await this.tools.Update("1", body: "See <https://example.atlassian.net/wiki/spaces/X/pages/1>, now edited.");

        // Assert
        JsonNode document = JsonNode.Parse(this.http.LastRequest.Body!["body"]!["value"]!.GetValue<string>())!;
        Assert.AreEqual("inlineCard", document["content"]![0]!["content"]![1]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a section update can add a smart link, written as an autolink.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task UpdateSection_WithAutolink_AddsSmartLink()
    {
        // Arrange
        string page = PageJson("current", 4, "## A\n\nold\n\n## B\n\nb");
        this.http.Respond(page).Respond(page).Respond(PageJson("draft", 1, "x"));

        // Act
        await this.tools.UpdateSection("1", "A", "See <https://example.atlassian.net/browse/ABC-1>.");

        // Assert
        JsonNode document = JsonNode.Parse(this.http.LastRequest.Body!["body"]!["value"]!.GetValue<string>())!;
        Assert.AreEqual("## A\n\nSee <https://example.atlassian.net/browse/ABC-1>.\n\n## B\n\nb", AdfToMarkdown.Convert(document));
    }

    /// <summary>
    /// Verifies that a Markdown update keeps the localId of each node of the published page, so
    /// that Confluence compares the versions by their text instead of showing every node as changed.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithMarkdown_KeepsTheLocalIdsOfThePublishedPage()
    {
        // Arrange
        JsonObject stored = AdfNodes.AsStoredByConfluence(MarkdownToAdf.Convert("# A\n\nold\n\n## B\n\nb"));
        this.http.Respond(PageJson("current", 4, stored)).Respond(PageJson("current", 4, stored)).Respond(PageJson("draft", 1, "x"));

        // Act
        await this.tools.Update("1", body: "# A\n\nnew\n\n## B\n\nb");

        // Assert
        var sent = (JsonObject)JsonNode.Parse(this.http.LastRequest.Body!["body"]!["value"]!.GetValue<string>())!;
        CollectionAssert.AreEqual(AdfNodes.Ids(stored), AdfNodes.Ids(sent));
    }

    /// <summary>
    /// Verifies that a section update keeps the localIds of the rest of the page, and gives the new
    /// section those of the nodes it replaces.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task UpdateSection_WithMarkdown_KeepsTheLocalIds()
    {
        // Arrange
        JsonObject stored = AdfNodes.AsStoredByConfluence(MarkdownToAdf.Convert("## A\n\nold\n\n## B\n\nb"));
        this.http.Respond(PageJson("current", 4, stored)).Respond(PageJson("current", 4, stored)).Respond(PageJson("draft", 1, "x"));

        // Act
        await this.tools.UpdateSection("1", "A", "new");

        // Assert
        var sent = (JsonObject)JsonNode.Parse(this.http.LastRequest.Body!["body"]!["value"]!.GetValue<string>())!;
        CollectionAssert.AreEqual(AdfNodes.Ids(stored), AdfNodes.Ids(sent));
    }

    /// <summary>
    /// Verifies that publishing an update creates the next version of the published page.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithPublish_UsesNextVersion()
    {
        // Arrange
        this.http.Respond(PageJson("current", 4, "old")).Respond(PageJson("current", 4, "old")).Respond(PageJson("current", 5, "new"));

        // Act
        await this.tools.Update("1", body: "new", versionMessage: "Fix typo", publish: true);

        // Assert
        JsonNode body = this.http.LastRequest.Body!;
        Assert.AreEqual("current", body["status"]!.GetValue<string>());
        Assert.AreEqual(5, body["version"]!["number"]!.GetValue<int>());
        Assert.AreEqual("Fix typo", body["version"]!["message"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a Markdown body is refused when the page has a macro, and nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithMarkdownOverMacro_ThrowsWithoutWriting()
    {
        // Arrange
        this.http.Respond(PageJson("current", 4, "old", Macro)).Respond(PageJson("current", 4, "old", Macro));

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Update("1", body: "new"));

        // Assert
        StringAssert.Contains(exception.Message, "1 extension", StringComparison.Ordinal);
        Assert.AreEqual(2, this.http.Requests.Count);
    }

    /// <summary>
    /// Verifies that a Markdown body may replace a macro when the loss is allowed.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithMarkdownOverMacroAndAllowContentLoss_Writes()
    {
        // Arrange
        this.http.Respond(PageJson("current", 4, "old", Macro)).Respond(PageJson("current", 4, "old", Macro)).Respond(PageJson("draft", 1, "new"));

        // Act
        await this.tools.Update("1", body: "new", allowContentLoss: true);

        // Assert
        Assert.AreEqual(HttpMethod.Put, this.http.LastRequest.Method);
    }

    /// <summary>
    /// Verifies that a section update builds on the existing draft, keeps the macro outside the
    /// section, and saves a draft.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task UpdateSection_WithExistingDraft_KeepsMacroAndSavesDraft()
    {
        // Arrange
        string published = PageJson("current", 4, "## A\n\npublished-old\n\n## B\n\nb", Macro);
        string draft = PageJson("draft", 1, "## A\n\ndraft-old\n\n## B\n\nb", Macro);
        this.http.Respond(published).Respond(draft).Respond(PageJson("draft", 1, "x"));

        // Act
        await this.tools.UpdateSection("1", "A", "new text");

        // Assert
        JsonNode body = this.http.LastRequest.Body!;
        Assert.AreEqual("draft", body["status"]!.GetValue<string>());
        Assert.AreEqual(1, body["version"]!["number"]!.GetValue<int>());
        JsonNode document = JsonNode.Parse(body["body"]!["value"]!.GetValue<string>())!;
        string markdown = AdfToMarkdown.Convert(document);
        StringAssert.Contains(markdown, "## A\n\nnew text\n\n## B", StringComparison.Ordinal);
        Assert.IsFalse(markdown.Contains("old", StringComparison.Ordinal));
        Assert.AreEqual(1, AdfInspector.FindLossyContent(document)["extension"]);
    }

    /// <summary>
    /// Verifies that publishing uses the draft body and the next version of the published page.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Publish_WithPublishedPage_UsesDraftBodyAndNextVersion()
    {
        // Arrange
        this.http.Respond(PageJson("draft", 1, "draft text")).Respond(PageJson("current", 3, "old")).Respond(PageJson("current", 4, "draft text"));

        // Act
        await this.tools.Publish("1");

        // Assert
        JsonNode body = this.http.LastRequest.Body!;
        Assert.AreEqual("current", body["status"]!.GetValue<string>());
        Assert.AreEqual(4, body["version"]!["number"]!.GetValue<int>());
        StringAssert.Contains(body["body"]!["value"]!.GetValue<string>(), "draft text", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that publishing with restrictions open removes every restriction of the page.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Publish_WithOpen_RemovesRestrictions()
    {
        // Arrange
        this.http
            .Respond(PageJson("draft", 1, "draft text"))
            .Fail(new AtlassianApiException(HttpStatusCode.NotFound, "Not found"))
            .Respond(PageJson("current", 1, "draft text"))
            .Respond(null);

        // Act
        string result = await this.tools.Publish("1", restrictions: "open");

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Delete, request.Method);
        StringAssert.EndsWith(request.Path, "content/1/restriction", StringComparison.Ordinal);
        Assert.AreEqual("open", JsonNode.Parse(result)!["restrictions"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that discarding makes the collaborative editing call that the editor's Revert to last
    /// published version makes, and does not write the page.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task DiscardDraft_WithPublishedPage_DeletesUnpublishedChanges()
    {
        // Arrange
        this.http.Respond(PageJson("current", 4, "published text")).Respond(null);

        // Act
        string result = await this.tools.DiscardDraft("1");

        // Assert
        Assert.HasCount(2, this.http.Requests);
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Delete, request.Method);
        Assert.AreEqual("wiki/rest/collab/1.0/content/1/changes/unpublished", request.Path);
        Assert.AreEqual("1", request.Body!["contentId"]!.GetValue<string>());
        StringAssert.Contains(result, "published version 4", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a page that was never published is refused, because there is nothing to revert to.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task DiscardDraft_WithNeverPublishedPage_Throws()
    {
        // Arrange
        this.http.Fail(new AtlassianApiException(HttpStatusCode.NotFound, "Not found"));

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.DiscardDraft("1"));

        // Assert
        StringAssert.Contains(exception.Message, "never been published", StringComparison.Ordinal);
        Assert.HasCount(1, this.http.Requests);
    }

    /// <summary>
    /// Verifies that publishing a page that was never published creates version 1.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Publish_WithNeverPublishedPage_UsesVersionOne()
    {
        // Arrange
        this.http
            .Respond(PageJson("draft", 1, "draft text"))
            .Fail(new AtlassianApiException(HttpStatusCode.NotFound, "Not found"))
            .Respond(PageJson("current", 1, "draft text"));

        // Act
        await this.tools.Publish("1");

        // Assert
        Assert.AreEqual(1, this.http.LastRequest.Body!["version"]!["number"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies that a page is returned as Markdown with its unrepresentable content counted.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Get_ByDefault_ReturnsMarkdownAndCountsMacros()
    {
        // Arrange
        this.http.Respond(PageJson("current", 2, "Hello", Macro));

        // Act
        string result = await this.tools.Get("1");

        // Assert
        Assert.AreEqual("wiki/api/v2/pages/1?body-format=atlas_doc_format", this.http.LastRequest.Path);
        JsonNode page = JsonNode.Parse(result)!;
        StringAssert.Contains(page["body"]!.GetValue<string>(), "Hello", StringComparison.Ordinal);
        StringAssert.Contains(page["body"]!.GetValue<string>(), "adf:extension", StringComparison.Ordinal);
        Assert.AreEqual(1, page["contentMarkdownCannotRepresent"]!["extension"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies that deleting a page that was never published deletes the draft.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithNeverPublishedPage_DeletesDraft()
    {
        // Arrange
        this.http.Fail(new AtlassianApiException(HttpStatusCode.NotFound, "Not found")).Respond(null);

        // Act
        await this.tools.Delete("1");

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("wiki/api/v2/pages/1?draft=true", this.http.LastRequest.Path);
    }

    private static string PageJson(string status, int version, string markdown, string? extraNode = null)
    {
        JsonObject document = MarkdownToAdf.Convert(markdown);
        if (extraNode is not null)
        {
            document["content"]!.AsArray().Add(JsonNode.Parse(extraNode));
        }

        return PageJson(status, version, document);
    }

    private static string PageJson(string status, int version, JsonObject document)
    {
        var page = new JsonObject
        {
            ["id"] = "1",
            ["status"] = status,
            ["title"] = "Page",
            ["spaceId"] = "9",
            ["version"] = new JsonObject { ["number"] = version },
            ["body"] = new JsonObject { ["atlas_doc_format"] = new JsonObject { ["value"] = document.ToJsonString() } },
            ["_links"] = new JsonObject { ["webui"] = "/spaces/X/pages/1", ["editui"] = "/pages/resumedraft.action?draftId=1" },
        };

        return page.ToJsonString();
    }
}
