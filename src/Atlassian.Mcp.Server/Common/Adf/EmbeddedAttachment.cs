// <copyright file="EmbeddedAttachment.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// An attachment to embed in rich text, as <see cref="MarkdownToAdf"/> needs it.
/// </summary>
/// <param name="MediaId">The ID of the file in the Atlassian media service, which ADF media nodes use.</param>
/// <param name="FileName">The file name.</param>
/// <param name="MediaType">The media type that Atlassian recorded, or <see langword="null"/>.</param>
public sealed record EmbeddedAttachment(string MediaId, string FileName, string? MediaType)
{
    private static readonly HashSet<string> ImageAndVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".tif", ".tiff", ".heic",
        ".mp4", ".mov", ".webm", ".m4v", ".avi", ".mkv", ".wmv",
    };

    /// <summary>
    /// Gets a value indicating whether the attachment is an image or a video, which Jira shows
    /// inline (in a <c>mediaSingle</c> node) rather than as a file card (in a <c>mediaGroup</c>).
    /// <para>
    /// The extension decides first, because Jira sometimes records an image's media type as
    /// <c>binary/octet-stream</c>.
    /// </para>
    /// </summary>
    public bool IsImageOrVideo
        => ImageAndVideoExtensions.Contains(Path.GetExtension(this.FileName))
           || (this.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)
           || (this.MediaType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ?? false);
}
