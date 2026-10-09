// <copyright file="AttachmentToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using Atlassian.Mcp.Server.Common.Attachments;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Confluence;
using Atlassian.Mcp.Server.Confluence.Attachments;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Confluence.Attachments;

/// <summary>
/// Tests for <see cref="AttachmentTools"/>.
/// </summary>
[TestClass]
public sealed class AttachmentToolsTests
{
    private RecordingHttpClient http = null!;
    private AttachmentTools tools = null!;
    private string root = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client and a temporary folder for the attachment reader's settings.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.root = Directory.CreateTempSubdirectory("ConfluenceAttachmentToolsTests").FullName;
        this.http = new RecordingHttpClient();
        var options = new AtlassianOptions(
            new Uri("https://example.atlassian.net/"),
            "user@example.com",
            "token",
            new HashSet<string>(),
            new HashSet<string>(),
            readOnly: false,
            attachmentFolders: []);
        var reader = new AttachmentReader(options, new AttachmentFolderStore(Path.Combine(this.root, "attachment-folders.json")));
        this.tools = new AttachmentTools(new ConfluenceClient(this.http), reader, _ => new FakeUploadApproval());
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
    /// Verifies that deleting an attachment sends a single DELETE request to the version 2 API, without purging it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithAttachmentId_MovesItToTheTrash()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        string result = await this.tools.Delete(" att1744338945 ");

        // Assert
        Assert.AreEqual(1, this.http.Requests.Count);
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("wiki/api/v2/attachments/att1744338945", this.http.LastRequest.Path);
        StringAssert.Contains(result, "Moved attachment att1744338945 to the trash.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that an attachment ID is escaped as a single path segment.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithReservedCharactersInId_EscapesThem()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Delete("att1/../2");

        // Assert
        Assert.AreEqual("wiki/api/v2/attachments/att1%2F..%2F2", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that an error from Confluence, such as an unknown attachment, is not reported as a success.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WhenConfluenceRejectsIt_Throws()
    {
        // Arrange
        this.http.Fail(new AtlassianApiException(HttpStatusCode.NotFound, "Not found"));

        // Act and assert
        await Assert.ThrowsExactlyAsync<AtlassianApiException>(() => this.tools.Delete("att404"));
    }
}
