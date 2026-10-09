// <copyright file="AttachmentToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Attachments;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Attachments;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Tests.Jira.Attachments;

/// <summary>
/// Tests for <see cref="AttachmentTools"/>.
/// </summary>
[TestClass]
public sealed class AttachmentToolsTests
{
    private RecordingHttpClient http = null!;
    private FakeUploadApproval approval = null!;
    private AttachmentTools tools = null!;
    private string root = null!;
    private string folder = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client, allowing uploads by path from a folder in a new temporary folder.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.root = Directory.CreateTempSubdirectory("AttachmentToolsTests").FullName;
        this.folder = Directory.CreateDirectory(Path.Combine(this.root, "allowed")).FullName;
        this.http = new RecordingHttpClient();
        this.approval = new FakeUploadApproval();
        var options = new AtlassianOptions(
            new Uri("https://example.atlassian.net/"),
            "user@example.com",
            "token",
            new HashSet<string>(),
            new HashSet<string>(),
            readOnly: false,
            attachmentFolders: [this.folder]);
        var reader = new AttachmentReader(options, new AttachmentFolderStore(Path.Combine(this.root, "attachment-folders.json")));
        this.tools = new AttachmentTools(new JiraClient(this.http), reader, _ => this.approval);
    }

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(this.root, recursive: true);
    }

    /// <summary>
    /// Verifies that the MCP server parameter of the add tool is supplied by the SDK, and never shown to the model as an argument.
    /// </summary>
    [TestMethod]
    public void Add_InputSchema_DoesNotExposeTheServer()
    {
        // Arrange
        MethodInfo method = typeof(AttachmentTools).GetMethod(nameof(AttachmentTools.Add))!;

        // Act
        McpServerTool tool = McpServerTool.Create(method, this.tools);

        // Assert
        JsonElement properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
        Assert.IsTrue(properties.TryGetProperty("filePath", out _));
        Assert.IsFalse(properties.TryGetProperty("server", out _));
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
        await this.tools.Add("PROJ-1", "notes.txt", base64, cancellationToken: CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("rest/api/3/issue/PROJ-1/attachments", request.Path);
        Assert.AreEqual("notes.txt", request.FileName);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(request.Content!));
    }

    /// <summary>
    /// Verifies that adding an attachment by path uploads the file's bytes under the file's own name.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithFilePath_UploadsTheFileUnderItsOwnName()
    {
        // Arrange
        this.http.Respond("""[{"id":"10003","filename":"screenshot.png"}]""");
        string path = Path.Combine(this.folder, "screenshot.png");
        await File.WriteAllBytesAsync(path, [137, 80, 78, 71]);

        // Act
        await this.tools.Add("PROJ-1", filePath: path);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("rest/api/3/issue/PROJ-1/attachments", request.Path);
        Assert.AreEqual("screenshot.png", request.FileName);
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71 }, request.Content);
    }

    /// <summary>
    /// Verifies that the user is asked about a file outside the allowed folders, told which issue it goes to,
    /// and that nothing is uploaded when the user denies it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithFilePathOutsideTheAllowedFoldersAndDenied_UploadsNothing()
    {
        // Arrange
        string outside = Path.Combine(this.root, "outside.txt");
        await File.WriteAllTextAsync(outside, "secret");
        this.approval.Decision = UploadDecision.Denied;

        // Act
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Add(" PROJ-1 ", filePath: outside));

        // Assert
        Assert.AreEqual("Jira issue PROJ-1", this.approval.Requests.Single().Destination);
        Assert.AreEqual(0, this.http.Requests.Count);
    }

    /// <summary>
    /// Verifies that a file outside the allowed folders is uploaded once the user approves it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithFilePathOutsideTheAllowedFoldersAndApproved_UploadsTheFile()
    {
        // Arrange
        this.http.Respond("""[{"id":"10004","filename":"outside.txt"}]""");
        string outside = Path.Combine(this.root, "outside.txt");
        await File.WriteAllTextAsync(outside, "shared");
        this.approval.Decision = UploadDecision.AllowOnce;

        // Act
        await this.tools.Add("PROJ-1", filePath: outside);

        // Assert
        Assert.AreEqual("shared", Encoding.UTF8.GetString(this.http.LastRequest.Content!));
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
