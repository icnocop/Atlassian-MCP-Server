// <copyright file="AttachmentFolderStoreTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Common.Attachments;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Common.Attachments;

/// <summary>
/// Tests for <see cref="AttachmentFolderStore"/>.
/// </summary>
[TestClass]
public sealed class AttachmentFolderStoreTests
{
    private string root = null!;
    private AttachmentFolderStore store = null!;

    /// <summary>
    /// Creates a store whose file does not exist yet, in a folder that does not exist yet.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.root = Directory.CreateTempSubdirectory("AttachmentFolderStoreTests").FullName;
        this.store = new AttachmentFolderStore(Path.Combine(this.root, "settings", "attachment-folders.json"));
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
    /// Verifies that a missing file means no saved folders.
    /// </summary>
    [TestMethod]
    public void Load_WithoutFile_ReturnsNoFolders()
    {
        // Act
        IReadOnlyList<string> folders = this.store.Load();

        // Assert
        Assert.AreEqual(0, folders.Count);
    }

    /// <summary>
    /// Verifies that added folders are saved, normalized, once each, and read back by another instance.
    /// </summary>
    [TestMethod]
    public void Add_WithFolders_SavesEachOnceForOtherInstances()
    {
        // Arrange
        string first = Path.Combine(this.root, "first");
        string second = Path.Combine(this.root, "second");

        // Act
        this.store.Add(first + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        this.store.Add(second, StringComparison.OrdinalIgnoreCase);
        this.store.Add(first, StringComparison.OrdinalIgnoreCase);

        // Assert
        CollectionAssert.AreEqual(new[] { first, second }, new AttachmentFolderStore(this.store.FilePath).Load().ToArray());
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(this.store.FilePath)!, "*.tmp").Length);
    }

    /// <summary>
    /// Verifies that relative and blank entries in a hand-edited file are ignored.
    /// </summary>
    [TestMethod]
    public void Load_WithRelativeAndBlankEntries_IgnoresThem()
    {
        // Arrange
        string folder = Path.Combine(this.root, "screenshots");
        this.WriteFile($$"""{"attachmentFolders":["relative", " ", {{System.Text.Json.JsonSerializer.Serialize(folder)}}]}""");

        // Act
        IReadOnlyList<string> folders = this.store.Load();

        // Assert
        CollectionAssert.AreEqual(new[] { folder }, folders.ToArray());
    }

    /// <summary>
    /// Verifies that a file that is not valid is reported with its path, rather than silently ignored.
    /// </summary>
    [TestMethod]
    public void Load_WithInvalidFile_ThrowsMcpExceptionNamingTheFile()
    {
        // Arrange
        this.WriteFile("""{"attachmentFolders": "C:\\not-a-list"}""");

        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(() => this.store.Load());

        // Assert
        StringAssert.Contains(exception.Message, this.store.FilePath, StringComparison.Ordinal);
    }

    private void WriteFile(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(this.store.FilePath)!);
        File.WriteAllText(this.store.FilePath, json);
    }
}
