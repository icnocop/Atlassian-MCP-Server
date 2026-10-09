// <copyright file="FakeUploadApproval.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Common.Attachments;

namespace Atlassian.Mcp.Server.Tests.TestDoubles;

/// <summary>
/// An <see cref="IUploadApproval"/> that records every question and gives a set answer.
/// </summary>
internal sealed class FakeUploadApproval : IUploadApproval
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FakeUploadApproval"/> class.
    /// </summary>
    /// <param name="decision">The answer to give.</param>
    public FakeUploadApproval(UploadDecision decision = UploadDecision.Unavailable)
    {
        this.Decision = decision;
    }

    /// <summary>Gets or sets the answer to give.</summary>
    public UploadDecision Decision { get; set; }

    /// <summary>Gets the questions asked so far, in order.</summary>
    public List<UploadApprovalRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public Task<UploadDecision> RequestAsync(UploadApprovalRequest request, CancellationToken cancellationToken)
    {
        this.Requests.Add(request);
        return Task.FromResult(this.Decision);
    }
}
