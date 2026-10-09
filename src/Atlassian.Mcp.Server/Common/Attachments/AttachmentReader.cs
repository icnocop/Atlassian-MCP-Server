// <copyright file="AttachmentReader.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Common.Attachments;

/// <summary>
/// Reads the content of a file to attach, either from base64 passed by the model or from a local file.
/// </summary>
/// <remarks>
/// Base64 suits small, generated content. A screenshot or a log on disk does not: its base64 runs to
/// hundreds of kilobytes that the model would have to reproduce character for character, and a single
/// wrong character still decodes, into a corrupted file that the site accepts. Reading the file here
/// avoids that, but it would let the model upload any file this process can read. So a file is read
/// only from a folder the user allowed — in <see cref="AtlassianOptions.AttachmentFoldersVariable"/>, or
/// by choosing "always allow" earlier — or after the user approves that one file.
/// </remarks>
public sealed class AttachmentReader
{
    /// <summary>The largest file read from disk, so that a mistaken path cannot exhaust memory.</summary>
    internal const long MaxFileBytes = 100 * 1024 * 1024;

    // Windows and macOS file systems are case-insensitive by default; Linux file systems are not.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private readonly AtlassianOptions options;
    private readonly AttachmentFolderStore store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentReader"/> class.
    /// </summary>
    /// <param name="options">The server options, with the folders configured by environment variable.</param>
    /// <param name="store">The folders the user chose to always allow.</param>
    public AttachmentReader(AtlassianOptions options, AttachmentFolderStore store)
    {
        this.options = options;
        this.store = store;
    }

    /// <summary>
    /// Reads the content to attach from exactly one of <paramref name="base64Content"/> and <paramref name="filePath"/>.
    /// </summary>
    /// <param name="fileName">The file name to give the attachment. Required with <paramref name="base64Content"/>; defaults to the file's own name with <paramref name="filePath"/>.</param>
    /// <param name="base64Content">The content, encoded as base64, or <see langword="null"/>.</param>
    /// <param name="filePath">The full path of a local file, or <see langword="null"/>.</param>
    /// <param name="destination">Where the file goes, such as "Jira issue PROJ-1", to show the user.</param>
    /// <param name="approval">Asks the user about a file outside the allowed folders.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file name and the content.</returns>
    /// <exception cref="McpException">The arguments are missing or invalid, or the file may not be read.</exception>
    public async Task<(string FileName, byte[] Content)> ReadAsync(
        string? fileName,
        string? base64Content,
        string? filePath,
        string destination,
        IUploadApproval approval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);

        bool hasContent = !string.IsNullOrWhiteSpace(base64Content);
        bool hasPath = !string.IsNullOrWhiteSpace(filePath);

        if (hasContent && hasPath)
        {
            throw new McpException("Pass either filePath or base64Content, not both.");
        }

        if (hasContent)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new McpException("The fileName parameter is required with base64Content.");
            }

            return (fileName.Trim(), DecodeBase64(base64Content!));
        }

        if (!hasPath)
        {
            throw new McpException("Pass filePath, the full path of a local file, or base64Content, the content encoded as base64.");
        }

        string path = await this.AuthorizeAsync(filePath!.Trim(), destination, approval, cancellationToken);

        byte[] content = await File.ReadAllBytesAsync(path, cancellationToken);
        return (string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(path) : fileName.Trim(), content);
    }

    /// <summary>
    /// Decodes base64 content, accepting a data URL prefix such as <c>data:image/png;base64,</c>.
    /// </summary>
    /// <param name="base64Content">The content.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="McpException">The content is not valid base64.</exception>
    internal static byte[] DecodeBase64(string base64Content)
    {
        string text = base64Content.Trim();
        int comma = text.IndexOf(',', StringComparison.Ordinal);
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
        {
            text = text[(comma + 1)..];
        }

        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException exception)
        {
            throw new McpException("The base64Content parameter is not valid base64.", exception);
        }
    }

    /// <summary>
    /// Checks that a file may be uploaded, asking the user when it is outside the allowed folders.
    /// </summary>
    /// <param name="filePath">The path passed by the model.</param>
    /// <param name="destination">Where the file goes, to show the user.</param>
    /// <param name="approval">Asks the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The full path of the file.</returns>
    /// <exception cref="McpException">The file may not be uploaded.</exception>
    internal async Task<string> AuthorizeAsync(string filePath, string destination, IUploadApproval approval, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new McpException($"The filePath parameter must be a full path. '{filePath}' is not.");
        }

        // GetFullPath removes "." and ".." segments, so "allowed\..\secret.txt" is checked as "secret.txt".
        string fullPath = Path.GetFullPath(filePath);

        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new McpException($"The file '{fullPath}' does not exist.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new McpException($"The file '{fullPath}' is {file.Length:N0} bytes, larger than the {MaxFileBytes:N0} bytes this server uploads.");
        }

        IReadOnlyList<string> allowedFolders = [.. this.options.AttachmentFolders, .. this.store.Load()];
        string? allowedFolder = allowedFolders.FirstOrDefault(folder => IsInside(fullPath, folder));

        // The folder check is on the path as written, and the user is shown the path as written. A symbolic
        // link or junction could make either stand for a different file, so a file reached through one is
        // refused rather than followed: up to the allowed folder, or else along the whole path.
        RejectLinks(file, allowedFolder);

        if (allowedFolder is not null)
        {
            return fullPath;
        }

        string folder = file.DirectoryName!;
        UploadDecision decision = await approval.RequestAsync(
            new UploadApprovalRequest(fullPath, file.Length, folder, destination),
            cancellationToken);

        switch (decision)
        {
            case UploadDecision.AllowOnce:
                return fullPath;

            case UploadDecision.AlwaysAllowFolder:
                this.store.Add(folder, PathComparison);
                return fullPath;

            case UploadDecision.Denied:
                throw new McpException($"The user did not allow uploading '{fullPath}'. Do not retry it; ask the user what to do instead.");

            default:
                string allowed = allowedFolders.Count == 0 ? "none" : string.Join(", ", allowedFolders);
                throw new McpException(
                    $"The file '{fullPath}' is not in a folder this server may upload from (allowed: {allowed}), and this MCP client cannot ask the user to approve it. " +
                    $"Do not fall back to base64Content for a file on disk. Ask the user to attach it themselves, or to add its folder to the {AtlassianOptions.AttachmentFoldersVariable} environment variable or to '{this.store.FilePath}'.");
        }
    }

    private static void RejectLinks(FileInfo file, string? allowedFolder)
    {
        for (FileSystemInfo? item = file; item is not null; item = item is FileInfo info ? info.Directory : ((DirectoryInfo)item).Parent)
        {
            if (allowedFolder is not null && IsSameFolder(item.FullName, allowedFolder))
            {
                return;
            }

            if (item.LinkTarget is not null)
            {
                throw new McpException($"The file '{file.FullName}' is reached through the link '{item.FullName}', which this server does not follow.");
            }
        }
    }

    private static bool IsInside(string fullPath, string folder)
    {
        // A root folder such as "C:\" already ends with a separator; any other folder is stored without one.
        string prefix = Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, PathComparison);
    }

    private static bool IsSameFolder(string path, string folder)
        => string.Equals(Path.TrimEndingDirectorySeparator(path), Path.TrimEndingDirectorySeparator(folder), PathComparison);
}
