// <copyright file="SiteToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Fields;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Fields;

/// <summary>
/// Tests for <see cref="SiteTools"/>.
/// </summary>
[TestClass]
public sealed class SiteToolsTests
{
    private const string SecretToken = "secret-token-value";

    private RecordingHttpClient http = null!;
    private SiteTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client and test options.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        var options = new AtlassianOptions(
            new Uri("https://example.atlassian.net/"),
            "user@example.com",
            SecretToken,
            new HashSet<string> { Toolsets.JiraFields, Toolsets.JiraIssues },
            new HashSet<string>(),
            readOnly: true);
        this.tools = new SiteTools(new JiraClient(this.http), options);
    }

    /// <summary>
    /// Verifies that the configuration never includes the API token.
    /// </summary>
    [TestMethod]
    public void GetConfiguration_WithToken_DoesNotIncludeTheToken()
    {
        // Act
        string result = this.tools.GetConfiguration();

        // Assert
        Assert.IsFalse(result.Contains(SecretToken, StringComparison.Ordinal));
        JsonNode configuration = JsonNode.Parse(result)!;
        Assert.IsTrue(configuration["apiTokenConfigured"]!.GetValue<bool>());
        Assert.AreEqual("https://example.atlassian.net/", configuration["siteUrl"]!.GetValue<string>());
        Assert.AreEqual("user@example.com", configuration["email"]!.GetValue<string>());
        Assert.IsTrue(configuration["readOnly"]!.GetValue<bool>());
        Assert.AreEqual(2, configuration["toolsets"]!.AsArray().Count);
    }

    /// <summary>
    /// Verifies that checking the connection reports the signed-in account and the site.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task CheckConnection_WithReachableSite_ReportsAccount()
    {
        // Arrange
        this.http
            .Respond("""{"accountId":"abc123","displayName":"Test User"}""")
            .Respond("""{"serverTitle":"Jira","deploymentType":"Cloud"}""");

        // Act
        string result = await this.tools.CheckConnection();

        // Assert
        Assert.AreEqual("rest/api/3/myself", this.http.Requests[0].Path);
        Assert.AreEqual("rest/api/3/serverInfo", this.http.Requests[1].Path);
        JsonNode status = JsonNode.Parse(result)!;
        Assert.IsTrue(status["ok"]!.GetValue<bool>());
        Assert.AreEqual("abc123", status["accountId"]!.GetValue<string>());
        Assert.AreEqual("Test User", status["displayName"]!.GetValue<string>());
        Assert.AreEqual("Cloud", status["deploymentType"]!.GetValue<string>());
    }
}
