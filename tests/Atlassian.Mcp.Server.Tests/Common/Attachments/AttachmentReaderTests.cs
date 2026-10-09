// <copyright file="AttachmentReaderTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text;
using Atlassian.Mcp.Server.Common.Attachments;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Common.Attachments;

/// <summary>
/// Tests for <see cref="AttachmentReader"/>.
/// </summary>
[TestClass]
public sealed class AttachmentReaderTests
{
    private const string Destination = "Jira issue PROJ-1";

    private string root = null!;
    private string allowed = null!;
    private string outside = null!;
    private AttachmentFolderStore store = null!;
    private FakeUploadApproval approval = null!;

    /// <summary>
    /// Creates a temporary folder holding an allowed folder, a folder outside it, and the saved folders file.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.root = Directory.CreateTempSubdirectory("AttachmentReaderTests").FullName;
        this.allowed = Directory.CreateDirectory(Path.Combine(this.root, "allowed")).FullName;
        this.outside = Directory.CreateDirectory(Path.Combine(this.root, "outside")).FullName;
        this.store = new AttachmentFolderStore(Path.Combine(this.root, "settings", "attachment-folders.json"));
        this.approval = new FakeUploadApproval();
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
    /// Verifies that base64 content is decoded and keeps the given file name.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithBase64Content_DecodesIt()
    {
        // Act
        (string fileName, byte[] content) = await this.Reader().ReadAsync(
            " notes.txt ", Convert.ToBase64String(Encoding.UTF8.GetBytes("hello")), null, Destination, this.approval, CancellationToken.None);

        // Assert
        Assert.AreEqual("notes.txt", fileName);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(content));
    }

    /// <summary>
    /// Verifies that base64 content without a file name is refused.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithBase64ContentAndNoFileName_ThrowsMcpException()
    {
        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader().ReadAsync(null, "aGVsbG8=", null, Destination, this.approval, CancellationToken.None));

        // Assert
        StringAssert.Contains(exception.Message, "fileName", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that passing both sources is refused, so that the model's intent is never guessed.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithBothSources_ThrowsMcpException()
    {
        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader().ReadAsync("a.txt", "aGVsbG8=", Path.Combine(this.allowed, "a.txt"), Destination, this.approval, CancellationToken.None));

        // Assert
        StringAssert.Contains(exception.Message, "not both", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that passing neither source is refused.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithNeitherSource_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader().ReadAsync("a.txt", " ", null, Destination, this.approval, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a file in a configured folder is read without asking, and is named after itself by default.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithFileInConfiguredFolder_ReadsItWithoutAsking()
    {
        // Arrange
        string path = CreateFile(this.allowed, "log.txt", "line 1");

        // Act
        (string fileName, byte[] content) = await this.Reader(this.allowed).ReadAsync(null, null, path, Destination, this.approval, CancellationToken.None);

        // Assert
        Assert.AreEqual("log.txt", fileName);
        Assert.AreEqual("line 1", Encoding.UTF8.GetString(content));
        Assert.AreEqual(0, this.approval.Requests.Count);
    }

    /// <summary>
    /// Verifies that a file in a subfolder of a configured folder is read without asking.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithFileInSubfolder_ReadsItWithoutAsking()
    {
        // Arrange
        string path = CreateFile(Directory.CreateDirectory(Path.Combine(this.allowed, "sub")).FullName, "a.txt");

        // Act
        await this.Reader(this.allowed).ReadAsync(null, null, path, Destination, this.approval, CancellationToken.None);

        // Assert
        Assert.AreEqual(0, this.approval.Requests.Count);
    }

    /// <summary>
    /// Verifies that a file name passed with a path overrides the file's own name.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithFileAndFileName_UsesTheFileName()
    {
        // Arrange
        string path = CreateFile(this.allowed, "1.png");

        // Act
        (string fileName, _) = await this.Reader(this.allowed).ReadAsync("dialog.png", null, path, Destination, this.approval, CancellationToken.None);

        // Assert
        Assert.AreEqual("dialog.png", fileName);
    }

    /// <summary>
    /// Verifies that the user is asked about a file outside the allowed folders, and is shown the resolved path, its folder, its size, and where it goes.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WithFileOutsideAllowedFolders_AsksTheUser()
    {
        // Arrange
        string path = CreateFile(this.outside, "a.txt", "abc");
        this.approval.Decision = UploadDecision.AllowOnce;

        // Act
        await this.Reader(this.allowed).ReadAsync(null, null, path, Destination, this.approval, CancellationToken.None);

        // Assert
        Assert.AreEqual(new UploadApprovalRequest(path, 3, this.outside, Destination), this.approval.Requests.Single());
    }

    /// <summary>
    /// Verifies that "allow once" reads the file without saving its folder, so the user is asked again next time.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WhenAllowedOnce_ReadsTheFileAndAsksAgainNextTime()
    {
        // Arrange
        string path = CreateFile(this.outside, "a.txt", "abc");
        this.approval.Decision = UploadDecision.AllowOnce;
        AttachmentReader reader = this.Reader();

        // Act
        (_, byte[] content) = await reader.ReadAsync(null, null, path, Destination, this.approval, CancellationToken.None);
        await reader.ReadAsync(null, null, path, Destination, this.approval, CancellationToken.None);

        // Assert
        Assert.AreEqual("abc", Encoding.UTF8.GetString(content));
        Assert.AreEqual(2, this.approval.Requests.Count);
        Assert.AreEqual(0, this.store.Load().Count);
    }

    /// <summary>
    /// Verifies that "always allow" saves the folder, so that later files there are read without asking.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WhenFolderAlwaysAllowed_SavesTheFolderAndStopsAsking()
    {
        // Arrange
        string first = CreateFile(this.outside, "1.png");
        string second = CreateFile(this.outside, "2.png");
        this.approval.Decision = UploadDecision.AlwaysAllowFolder;

        // Act
        await this.Reader().ReadAsync(null, null, first, Destination, this.approval, CancellationToken.None);
        await this.Reader().ReadAsync(null, null, second, Destination, this.approval, CancellationToken.None);

        // Assert
        Assert.AreEqual(1, this.approval.Requests.Count);
        CollectionAssert.AreEqual(new[] { this.outside }, this.store.Load().ToArray());
    }

    /// <summary>
    /// Verifies that a denied upload is reported without reading the file.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WhenDenied_ThrowsMcpException()
    {
        // Arrange
        string path = CreateFile(this.outside, "a.txt");
        this.approval.Decision = UploadDecision.Denied;

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader().ReadAsync(null, null, path, Destination, this.approval, CancellationToken.None));

        // Assert
        StringAssert.Contains(exception.Message, "did not allow", StringComparison.Ordinal);
        Assert.AreEqual(0, this.store.Load().Count);
    }

    /// <summary>
    /// Verifies that when the user cannot be asked, the refusal says what to do instead of falling back to base64.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ReadAsync_WhenUserCannotBeAsked_ThrowsMcpExceptionNamingTheWaysToAllowIt()
    {
        // Arrange
        string path = CreateFile(this.outside, "a.txt");

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader().ReadAsync(null, null, path, Destination, this.approval, CancellationToken.None));

        // Assert
        StringAssert.Contains(exception.Message, "cannot ask the user", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, AtlassianOptions.AttachmentFoldersVariable, StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, this.store.FilePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a relative path is refused, since it would depend on the server's working folder.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AuthorizeAsync_WithRelativePath_ThrowsMcpException()
    {
        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader(this.allowed).AuthorizeAsync("a.txt", Destination, this.approval, CancellationToken.None));

        // Assert
        StringAssert.Contains(exception.Message, "full path", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that ".." segments cannot climb out of an allowed folder without the user being asked about the resolved path.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AuthorizeAsync_WithParentSegments_AsksAboutTheResolvedPath()
    {
        // Arrange
        string secret = CreateFile(this.root, "secret.txt");
        string path = this.allowed + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "secret.txt";

        // Act
        await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader(this.allowed).AuthorizeAsync(path, Destination, this.approval, CancellationToken.None));

        // Assert
        Assert.AreEqual(secret, this.approval.Requests.Single().FilePath);
    }

    /// <summary>
    /// Verifies that a sibling folder whose name starts with the allowed folder's name is not treated as inside it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AuthorizeAsync_WithSiblingFolderSharingThePrefix_AsksTheUser()
    {
        // Arrange
        string path = CreateFile(Directory.CreateDirectory(this.allowed + "-other").FullName, "a.txt");

        // Act
        await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader(this.allowed).AuthorizeAsync(path, Destination, this.approval, CancellationToken.None));

        // Assert
        Assert.AreEqual(1, this.approval.Requests.Count);
    }

    /// <summary>
    /// Verifies that a missing file is reported before the user is asked anything.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AuthorizeAsync_WithMissingFile_ThrowsMcpExceptionWithoutAsking()
    {
        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader().AuthorizeAsync(Path.Combine(this.outside, "missing.txt"), Destination, this.approval, CancellationToken.None));

        // Assert
        StringAssert.Contains(exception.Message, "does not exist", StringComparison.Ordinal);
        Assert.AreEqual(0, this.approval.Requests.Count);
    }

    /// <summary>
    /// Verifies that a folder link inside an allowed folder is not followed out of it.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AuthorizeAsync_ThroughFolderLinkInAllowedFolder_ThrowsMcpException()
    {
        // Arrange
        CreateFile(this.outside, "secret.txt");
        string link = CreateFolderLink(Path.Combine(this.allowed, "link"), this.outside);

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader(this.allowed).AuthorizeAsync(Path.Combine(link, "secret.txt"), Destination, this.approval, CancellationToken.None));

        // Assert
        StringAssert.Contains(exception.Message, "link", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the user is never asked about a path that goes through a link, since the path shown would not be the file uploaded.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AuthorizeAsync_ThroughFolderLinkOutsideAllowedFolders_ThrowsMcpExceptionWithoutAsking()
    {
        // Arrange
        CreateFile(this.allowed, "secret.txt");
        string link = CreateFolderLink(Path.Combine(this.outside, "screenshots"), this.allowed);
        this.approval.Decision = UploadDecision.AllowOnce;

        // Act
        await Assert.ThrowsExactlyAsync<McpException>(
            () => this.Reader().AuthorizeAsync(Path.Combine(link, "secret.txt"), Destination, this.approval, CancellationToken.None));

        // Assert
        Assert.AreEqual(0, this.approval.Requests.Count);
    }

    /// <summary>
    /// Verifies that a data URL prefix is removed before decoding.
    /// </summary>
    [TestMethod]
    public void DecodeBase64_WithDataUrlPrefix_DecodesTheContent()
    {
        // Act
        byte[] bytes = AttachmentReader.DecodeBase64("data:text/plain;base64," + Convert.ToBase64String([1, 2, 3]));

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
        Assert.ThrowsExactly<McpException>(() => AttachmentReader.DecodeBase64("not base64!"));
    }

    private static string CreateFile(string folder, string name, string content = "a")
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string CreateFolderLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // Creating a symbolic link on Windows needs Developer Mode or elevation.
            Assert.Inconclusive($"A symbolic link could not be created: {exception.Message}");
        }

        return link;
    }

    private AttachmentReader Reader(params string[] configuredFolders)
        => new(
            new AtlassianOptions(
                new Uri("https://example.atlassian.net/"),
                "user@example.com",
                "token",
                new HashSet<string>(),
                new HashSet<string>(),
                readOnly: false,
                attachmentFolders: configuredFolders),
            this.store);
}
