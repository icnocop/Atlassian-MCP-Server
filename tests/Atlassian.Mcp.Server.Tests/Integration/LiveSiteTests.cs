// <copyright file="LiveSiteTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Attachments;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Confluence;
using Atlassian.Mcp.Server.Confluence.Pages;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Fields;
using Atlassian.Mcp.Server.Jira.Issues;
using Atlassian.Mcp.Server.Jira.Search;

namespace Atlassian.Mcp.Server.Tests.Integration;

/// <summary>
/// Tests that call a real Atlassian site. They run only with the Integration category and the
/// settings described in Example.runsettings, and are inconclusive otherwise. The Confluence test
/// writes only a private draft in the configured space, and deletes it again.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LiveSiteTests : IDisposable
{
    private HttpClient httpClient = null!;
    private AtlassianHttpClient http = null!;
    private JiraMetadataCache metadata = null!;
    private AtlassianOptions options = null!;

    /// <summary>
    /// Creates the clients from the environment, or marks the test inconclusive.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        AtlassianOptions? configured = AtlassianOptions.FromEnvironment(Environment.GetEnvironmentVariable, out IReadOnlyList<string> errors);
        if (configured is null)
        {
            Assert.Inconclusive("The live site is not configured: " + string.Join(" ", errors));
        }

        this.options = configured;
        this.httpClient = new HttpClient();
        AtlassianHttpClient.Configure(this.httpClient, this.options);
        this.http = new AtlassianHttpClient(this.httpClient);
        this.metadata = new JiraMetadataCache();
    }

    /// <summary>
    /// Verifies that the connection check succeeds.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task CheckConnection_WithConfiguredSite_ReportsOk()
    {
        // Act
        string result = await new SiteTools(new JiraClient(this.http), this.options, new AttachmentFolderStore(AttachmentFolderStore.DefaultFilePath)).CheckConnection();

        // Assert
        Assert.IsTrue(JsonNode.Parse(result)!["ok"]!.GetValue<bool>());
    }

    /// <summary>
    /// Verifies that the configured test issue can be read and found by search.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetIssueAndSearch_WithTestIssue_FindTheIssue()
    {
        // Arrange
        string issueKey = Require("ATLASSIAN_TEST_JIRA_ISSUE_KEY");
        string projectKey = Require("ATLASSIAN_TEST_JIRA_PROJECT_KEY");
        var jira = new JiraClient(this.http);

        // Act
        JsonNode issue = JsonNode.Parse(await new IssueTools(jira, this.metadata).Get(issueKey, fields: "summary,description"))!;
        JsonNode page = JsonNode.Parse(await new SearchTools(jira).Issues($"project = \"{projectKey}\" ORDER BY created DESC", maxResults: 1))!;

        // Assert
        Assert.AreEqual(issueKey, issue["key"]!.GetValue<string>(), ignoreCase: true);
        Assert.AreEqual(1, page["issues"]!.AsArray().Count);
    }

    /// <summary>
    /// Verifies that a new page is saved as a private draft, can be read back, and can be deleted.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task CreatePage_WithDefaults_SavesAPrivateDraft()
    {
        // Arrange
        string spaceKey = Require("ATLASSIAN_TEST_CONFLUENCE_SPACE_KEY");
        var pages = new PageTools(new ConfluenceClient(this.http));
        string title = $"Atlassian MCP Server test {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        // Act
        JsonNode created = JsonNode.Parse(await pages.Create(title, "## Test\n\nCreated by an integration test.", spaceKey))!;
        string id = created["id"]!.GetValue<string>();
        try
        {
            JsonNode page = JsonNode.Parse(await pages.Get(id, includeDraft: true))!;

            // Assert
            Assert.AreEqual("draft", created["status"]!.GetValue<string>());
            Assert.IsTrue(created["private"]!.GetValue<bool>());
            Assert.AreEqual(title, page["title"]!.GetValue<string>());
            StringAssert.Contains(page["body"]!.GetValue<string>(), "Created by an integration test.");
        }
        finally
        {
            await pages.Delete(id);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.metadata?.Dispose();
        this.httpClient?.Dispose();
    }

    private static string Require(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive($"Set {name} to run this test.");
        }

        return value;
    }
}
