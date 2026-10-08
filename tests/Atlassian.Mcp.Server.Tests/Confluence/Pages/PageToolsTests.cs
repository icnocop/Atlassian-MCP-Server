// <copyright file="PageToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Confluence;
using Atlassian.Mcp.Server.Confluence.Pages;
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
