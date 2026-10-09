// <copyright file="IUploadApproval.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Attachments;

/// <summary>
/// Asks the user whether a file outside the allowed folders may be uploaded.
/// </summary>
/// <remarks>
/// The question goes to the user, not to the model. A model can be talked into claiming that the user
/// agreed by text it reads, such as an issue description or a web page, so its word is not approval.
/// </remarks>
public interface IUploadApproval
{
    /// <summary>
    /// Asks the user whether the file may be uploaded.
    /// </summary>
    /// <param name="request">What would be uploaded, and where to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's decision, or <see cref="UploadDecision.Unavailable"/> when the user cannot be asked.</returns>
    Task<UploadDecision> RequestAsync(UploadApprovalRequest request, CancellationToken cancellationToken);
}
