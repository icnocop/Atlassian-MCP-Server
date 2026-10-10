// <copyright file="AttachmentReference.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// The attachment that an ADF media node shows, as <see cref="AdfToMarkdown"/> needs it to write
/// <c>![name](attachment:ID)</c>.
/// </summary>
/// <param name="AttachmentId">The attachment ID.</param>
/// <param name="FileName">The file name, or an empty string when it is not known.</param>
public sealed record AttachmentReference(string AttachmentId, string FileName);
