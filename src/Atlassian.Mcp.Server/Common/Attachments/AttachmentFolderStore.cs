// <copyright file="AttachmentFolderStore.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Common.Attachments;

/// <summary>
/// The folders the user chose to always allow uploads from, saved in a file this server owns.
/// </summary>
/// <remarks>
/// The folders are not written to the MCP client's configuration: every client keeps it in a different
/// file and format, that file also holds the API token, and a change there applies only after a
/// restart. This file is shared by every client and session of the user, and is read on every upload,
/// so a folder allowed in one session is allowed in all of them at once. The user can edit it by hand.
/// </remarks>
public sealed class AttachmentFolderStore
{
    private const string FoldersProperty = "attachmentFolders";

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentFolderStore"/> class.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    public AttachmentFolderStore(string filePath)
    {
        this.FilePath = filePath;
    }

    /// <summary>
    /// Gets the default file: <c>%APPDATA%\Atlassian.Mcp.Server\attachment-folders.json</c> on Windows, and
    /// <c>~/.config/Atlassian.Mcp.Server/attachment-folders.json</c> elsewhere.
    /// </summary>
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        "Atlassian.Mcp.Server",
        "attachment-folders.json");

    /// <summary>Gets the full path of the file.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Reads the saved folders.
    /// </summary>
    /// <returns>The full paths of the folders; empty when the file does not exist.</returns>
    /// <exception cref="McpException">The file cannot be read or is not valid.</exception>
    public IReadOnlyList<string> Load()
    {
        string text;
        try
        {
            text = File.ReadAllText(this.FilePath);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new McpException($"The file of allowed attachment folders, '{this.FilePath}', cannot be read: {exception.Message}", exception);
        }

        try
        {
            JsonArray? folders = JsonNode.Parse(text)?[FoldersProperty]?.AsArray();
            return folders is null
                ? []
                : folders
                    .Select(folder => folder?.GetValue<string>())
                    .Where(folder => !string.IsNullOrWhiteSpace(folder) && Path.IsPathFullyQualified(folder))
                    .Select(folder => Normalize(folder!))
                    .ToList();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new McpException(
                $"The file of allowed attachment folders, '{this.FilePath}', is not valid. It must look like {{\"{FoldersProperty}\": [\"C:\\\\Screenshots\"]}}. Fix or delete it.",
                exception);
        }
    }

    /// <summary>
    /// Adds a folder, unless it is already saved.
    /// </summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <param name="comparison">How to compare paths.</param>
    public void Add(string folder, StringComparison comparison)
    {
        string normalized = Normalize(folder);
        List<string> folders = [.. this.Load()];
        if (folders.Any(saved => string.Equals(saved, normalized, comparison)))
        {
            return;
        }

        folders.Add(normalized);

        var document = new JsonObject { [FoldersProperty] = new JsonArray([.. folders.Select(saved => JsonValue.Create(saved))]) };
        Directory.CreateDirectory(Path.GetDirectoryName(this.FilePath)!);

        // Written to a temporary file and moved over the old one, so that a session reading the file never
        // sees it half written. Two sessions saving a folder at the same moment can still lose one of the
        // two folders; the user is then simply asked again.
        string temporary = this.FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, this.FilePath, overwrite: true);
    }

    private static string Normalize(string folder)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}
