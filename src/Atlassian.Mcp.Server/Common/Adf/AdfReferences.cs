// <copyright file="AdfReferences.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// The values that Markdown refers to but does not contain, resolved before
/// <see cref="MarkdownToAdf"/> converts it: the account IDs of mentioned users, and the media of
/// embedded attachments.
/// </summary>
/// <param name="MentionAccountIds">The account ID of each mentioned name, keyed by the name as written.</param>
/// <param name="Attachments">The media of each embedded attachment, keyed by attachment ID.</param>
public sealed record AdfReferences(
    IReadOnlyDictionary<string, string> MentionAccountIds,
    IReadOnlyDictionary<string, EmbeddedAttachment> Attachments)
{
    /// <summary>Gets references that resolve nothing.</summary>
    public static AdfReferences None { get; } = new(new Dictionary<string, string>(), new Dictionary<string, EmbeddedAttachment>());

    /// <summary>
    /// Creates references that resolve only mentions.
    /// </summary>
    /// <param name="mentionAccountIds">The account ID of each mentioned name.</param>
    /// <returns>The references.</returns>
    public static AdfReferences ForMentions(IReadOnlyDictionary<string, string> mentionAccountIds)
        => new(mentionAccountIds, None.Attachments);
}
