// <copyright file="MediaTypes.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Http;

/// <summary>
/// Maps file names to media types for uploads.
/// </summary>
internal static class MediaTypes
{
    /// <summary>The media type for content of an unknown type.</summary>
    public const string Binary = "application/octet-stream";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".bmp"] = "image/bmp",
        [".csv"] = "text/csv",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".gif"] = "image/gif",
        [".gz"] = "application/gzip",
        [".htm"] = "text/html",
        [".html"] = "text/html",
        [".jpeg"] = "image/jpeg",
        [".jpg"] = "image/jpeg",
        [".json"] = "application/json",
        [".log"] = "text/plain",
        [".md"] = "text/markdown",
        [".mp4"] = "video/mp4",
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".svg"] = "image/svg+xml",
        [".txt"] = "text/plain",
        [".webp"] = "image/webp",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".xml"] = "application/xml",
        [".zip"] = "application/zip",
    };

    /// <summary>
    /// Returns the media type for <paramref name="fileName"/>, or <see cref="Binary"/> when the
    /// extension is not known.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The media type.</returns>
    public static string FromFileName(string fileName)
        => ByExtension.TryGetValue(Path.GetExtension(fileName), out string? mediaType) ? mediaType : Binary;

    /// <summary>
    /// Returns a value indicating whether content of <paramref name="mediaType"/> is text that can
    /// be returned to the model as a string.
    /// </summary>
    /// <param name="mediaType">The media type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for text types.</returns>
    public static bool IsText(string? mediaType)
        => mediaType is not null
           && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
               || mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase)
               || mediaType.EndsWith("xml", StringComparison.OrdinalIgnoreCase));
}
