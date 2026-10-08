// <copyright file="AtlassianOptionsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Configuration;

namespace Atlassian.Mcp.Server.Tests.Configuration;

/// <summary>
/// Tests for <see cref="AtlassianOptions"/>.
/// </summary>
[TestClass]
public sealed class AtlassianOptionsTests
{
    /// <summary>
    /// Verifies that each missing required variable is reported.
    /// </summary>
    [TestMethod]
    public void FromEnvironment_WithNoVariables_ReportsThreeErrors()
    {
        // Act
        AtlassianOptions? options = AtlassianOptions.FromEnvironment(_ => null, out IReadOnlyList<string> errors);

        // Assert
        Assert.IsNull(options);
        Assert.AreEqual(3, errors.Count);
    }

    /// <summary>
    /// Verifies that a site URL that is not https is rejected.
    /// </summary>
    [TestMethod]
    public void FromEnvironment_WithHttpSiteUrl_ReportsError()
    {
        // Act
        AtlassianOptions? options = AtlassianOptions.FromEnvironment(Variables(siteUrl: "http://example.atlassian.net"), out IReadOnlyList<string> errors);

        // Assert
        Assert.IsNull(options);
        Assert.AreEqual(1, errors.Count);
        StringAssert.Contains(errors[0], "https", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a URL copied from the browser is trimmed to the site root.
    /// </summary>
    [TestMethod]
    public void FromEnvironment_WithPagePath_TrimsToSiteRoot()
    {
        // Act
        AtlassianOptions? options = AtlassianOptions.FromEnvironment(Variables(siteUrl: "https://example.atlassian.net/wiki/home"), out _);

        // Assert
        Assert.AreEqual("https://example.atlassian.net/", options!.SiteUrl.ToString());
    }

    /// <summary>
    /// Verifies that the default toolsets are all of them except the opt-in ones.
    /// </summary>
    [TestMethod]
    public void FromEnvironment_WithoutToolsets_EnablesAllExceptOptIn()
    {
        // Act
        AtlassianOptions options = AtlassianOptions.FromEnvironment(Variables(), out _)!;

        // Assert
        Assert.IsTrue(options.Toolsets.Contains(Toolsets.JiraIssues));
        Assert.IsTrue(options.Toolsets.Contains(Toolsets.Confluence));
        Assert.IsFalse(options.Toolsets.Contains(Toolsets.ConfluenceAdmin));
        Assert.AreEqual(Toolsets.Known.Count - 1, options.Toolsets.Count);
        Assert.IsFalse(options.ReadOnly);
        Assert.AreEqual(0, options.EnabledTools.Count);
    }

    /// <summary>
    /// Verifies that listed toolsets, enabled tools, and read-only mode are read.
    /// </summary>
    [TestMethod]
    public void FromEnvironment_WithOptionalVariables_ReadsThem()
    {
        // Act
        AtlassianOptions options = AtlassianOptions.FromEnvironment(
            Variables(toolsets: "jira-issues, confluence-admin", enabledTools: "a, b", readOnly: "TRUE"),
            out _)!;

        // Assert
        CollectionAssert.AreEquivalent(new[] { "jira-issues", "confluence-admin" }, options.Toolsets.ToArray());
        CollectionAssert.AreEquivalent(new[] { "a", "b" }, options.EnabledTools.ToArray());
        Assert.IsTrue(options.ReadOnly);
    }

    /// <summary>
    /// Verifies that an unknown toolset is reported.
    /// </summary>
    [TestMethod]
    public void FromEnvironment_WithUnknownToolset_ReportsError()
    {
        // Act
        AtlassianOptions? options = AtlassianOptions.FromEnvironment(Variables(toolsets: "bogus"), out IReadOnlyList<string> errors);

        // Assert
        Assert.IsNull(options);
        StringAssert.Contains(errors[0], "unknown toolset 'bogus'", StringComparison.Ordinal);
    }

    private static Func<string, string?> Variables(
        string siteUrl = "https://example.atlassian.net",
        string? toolsets = null,
        string? enabledTools = null,
        string? readOnly = null)
    {
        var values = new Dictionary<string, string?>
        {
            [AtlassianOptions.SiteUrlVariable] = siteUrl,
            [AtlassianOptions.EmailVariable] = "user@example.com",
            [AtlassianOptions.ApiTokenVariable] = "token",
            [AtlassianOptions.ToolsetsVariable] = toolsets,
            [AtlassianOptions.EnabledToolsVariable] = enabledTools,
            [AtlassianOptions.ReadOnlyVariable] = readOnly,
        };

        return name => values.GetValueOrDefault(name);
    }
}
