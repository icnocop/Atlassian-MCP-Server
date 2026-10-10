// <copyright file="MentionResolver.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// Resolves the names of mentions written without an account ID (<c>@[Display Name]</c>) to
/// account IDs, by searching for users, so that <see cref="MarkdownToAdf"/> can turn them into
/// mentions that notify the user.
/// </summary>
public static class MentionResolver
{
    /// <summary>
    /// The largest number of candidates listed in the message for an ambiguous mention.
    /// </summary>
    private const int ListedCandidates = 10;

    /// <summary>
    /// Resolves every mention name in <paramref name="markdownTexts"/>, searching once for each
    /// distinct name.
    /// <para>
    /// A name resolves to the one user whose display name matches it, ignoring case; or, when no
    /// display name matches, to the only user the search found (for example when the name is an
    /// email address). Anything else is an error, so content is never posted with a mention of the
    /// wrong user, or with a mention left as text that notifies nobody.
    /// </para>
    /// </summary>
    /// <param name="markdownTexts">The Markdown texts; <see langword="null"/> entries are skipped.</param>
    /// <param name="search">Searches for the users matching a name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account ID of each name, keyed by the name as written.</returns>
    /// <exception cref="McpException">A name matches no user, or more than one.</exception>
    public static async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        IEnumerable<string?> markdownTexts,
        Func<string, CancellationToken, Task<IReadOnlyList<UserCandidate>>> search,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(markdownTexts);
        ArgumentNullException.ThrowIfNull(search);

        var accountIds = new Dictionary<string, string>(StringComparer.Ordinal);
        IEnumerable<string> names = markdownTexts
            .Where(text => !string.IsNullOrEmpty(text))
            .SelectMany(MarkdownToAdf.FindMentionNames)
            .Distinct(StringComparer.Ordinal);

        foreach (string name in names)
        {
            if (name.Length == 0)
            {
                throw new McpException("A mention must name a user: write @[Display Name] or @[Display Name](accountid:ID).");
            }

            IReadOnlyList<UserCandidate> candidates = await search(name, cancellationToken);
            accountIds[name] = Choose(name, candidates);
        }

        return accountIds;
    }

    /// <summary>
    /// Picks the account ID for <paramref name="name"/> from the search results.
    /// </summary>
    /// <param name="name">The mentioned name.</param>
    /// <param name="candidates">The users the search found.</param>
    /// <returns>The account ID.</returns>
    /// <exception cref="McpException">No user, or more than one, matches.</exception>
    internal static string Choose(string name, IReadOnlyList<UserCandidate> candidates)
    {
        List<UserCandidate> exact = candidates
            .Where(candidate => string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1)
        {
            return exact[0].AccountId;
        }

        if (exact.Count == 0 && candidates.Count == 1)
        {
            return candidates[0].AccountId;
        }

        if (candidates.Count == 0)
        {
            throw new McpException(
                $"The mention @[{name}] matches no user. Check the name, or write @[{name}](accountid:ID) with the account ID.");
        }

        List<UserCandidate> listed = exact.Count > 1 ? exact : candidates.ToList();
        string list = string.Join(", ", listed.Take(ListedCandidates).Select(candidate => $"{candidate.DisplayName} ({candidate.AccountId})"));
        string more = listed.Count > ListedCandidates ? $", and {listed.Count - ListedCandidates} more" : string.Empty;
        throw new McpException(
            $"The mention @[{name}] matches {listed.Count} users: {list}{more}. Write @[Display Name](accountid:ID) with the account ID of the user you mean.");
    }
}
