// <copyright file="UploadApprovalRequest.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Attachments;

/// <summary>
/// What a question about uploading a file shows the user.
/// </summary>
/// <param name="FilePath">The full path of the file, as resolved by the server rather than as written by the model.</param>
/// <param name="Length">The size of the file, in bytes.</param>
/// <param name="Folder">The folder that "always allow" would add.</param>
/// <param name="Destination">Where the file would go, such as "Jira issue PROJ-1".</param>
public sealed record UploadApprovalRequest(string FilePath, long Length, string Folder, string Destination);
