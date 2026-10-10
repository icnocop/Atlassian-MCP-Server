// <copyright file="MentionResolverTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Common.Adf;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Common.Adf;

/// <summary>
/// Tests for <see cref="MentionResolver"/>.
/// </summary>
[TestClass]
public sealed class MentionResolverTests
{
    /// <summary>
    /// Verifies that the one user whose display name matches, ignoring case, is chosen among several results.
    /// </summary>
    [TestMethod]
    public void Choose_WithOneExactMatchAmongSeveral_ReturnsIt()
    {
        // Arrange
        UserCandidate[] candidates = [new("1", "Jane Doe"), new("2", "Jane Doe-Smith")];

        // Act
        string accountId = MentionResolver.Choose("jane doe", candidates);

        // Assert
        Assert.AreEqual("1", accountId);
    }

    /// <summary>
    /// Verifies that two users with the same display name are refused, listing only those two.
    /// </summary>
    [TestMethod]
    public void Choose_WithSeveralExactMatches_ThrowsListingThem()
    {
        // Arrange
        UserCandidate[] candidates = [new("1", "Jane Doe"), new("2", "Jane Doe-Smith"), new("3", "JANE DOE")];

        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(() => MentionResolver.Choose("Jane Doe", candidates));

        // Assert
        StringAssert.Contains(exception.Message, "matches 2 users: Jane Doe (1), JANE DOE (3).", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the only result is chosen when no display name matches, such as for an email address.
    /// </summary>
    [TestMethod]
    public void Choose_WithSingleCandidateThatDoesNotMatchExactly_ReturnsIt()
    {
        // Act
        string accountId = MentionResolver.Choose("jane@example.com", [new UserCandidate("1", "Jane Doe")]);

        // Assert
        Assert.AreEqual("1", accountId);
    }

    /// <summary>
    /// Verifies that several results without an exact match are refused, listing them with their account IDs.
    /// </summary>
    [TestMethod]
    public void Choose_WithSeveralCandidatesAndNoExactMatch_ThrowsListingThem()
    {
        // Arrange
        UserCandidate[] candidates = [new("1", "Jane Doe"), new("2", "Jane Smith")];

        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(() => MentionResolver.Choose("Jane", candidates));

        // Assert
        StringAssert.Contains(exception.Message, "@[Jane] matches 2 users: Jane Doe (1), Jane Smith (2)", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "accountid:", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a name that matches no user is refused.
    /// </summary>
    [TestMethod]
    public void Choose_WithNoCandidates_ThrowsMatchesNoUser()
    {
        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(() => MentionResolver.Choose("Nobody", []));

        // Assert
        StringAssert.Contains(exception.Message, "@[Nobody] matches no user", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that each distinct name in all the texts is searched once, and that null texts and
    /// mentions with an explicit account ID cause no search.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ResolveAsync_WithRepeatedNames_SearchesEachNameOnce()
    {
        // Arrange
        var searched = new List<string>();
        Task<IReadOnlyList<UserCandidate>> Search(string name, CancellationToken cancellationToken)
        {
            searched.Add(name);
            return Task.FromResult<IReadOnlyList<UserCandidate>>([new UserCandidate("id-" + name, name)]);
        }

        // Act
        IReadOnlyDictionary<string, string> accountIds = await MentionResolver.ResolveAsync(
            ["@[Jane] and @[Bob]", null, "@[Jane] @[Eve](accountid:e)"],
            Search,
            CancellationToken.None);

        // Assert
        CollectionAssert.AreEqual(new[] { "Jane", "Bob" }, searched);
        Assert.AreEqual("id-Jane", accountIds["Jane"]);
        Assert.AreEqual("id-Bob", accountIds["Bob"]);
    }

    /// <summary>
    /// Verifies that a mention with an empty name is refused without a search.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ResolveAsync_WithEmptyName_Throws()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => MentionResolver.ResolveAsync(
            ["@[ ]"],
            (_, _) => throw new AssertFailedException("No search was expected."),
            CancellationToken.None));
    }
}
