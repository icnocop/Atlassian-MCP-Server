// <copyright file="SearchToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Search;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Search;

/// <summary>
/// Tests for <see cref="SearchTools"/>.
/// </summary>
[TestClass]
public sealed class SearchToolsTests
{
    private RecordingHttpClient http = null!;
    private SearchTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new SearchTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that a search sends the default fields and passes the paging details through.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Issues_WithDefaults_SendsDefaultFieldsAndReturnsPaging()
    {
        // Arrange
        this.http.Respond("""{"issues":[{"key":"P-1","fields":{"summary":"S"}}],"nextPageToken":"t2","isLast":false}""");

        // Act
        string result = await this.tools.Issues("project = P");

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("rest/api/3/search/jql", request.Path);
        Assert.AreEqual("project = P", request.Body!["jql"]!.GetValue<string>());
        Assert.AreEqual(SearchTools.DefaultFields.Split(',').Length, request.Body["fields"]!.AsArray().Count);
        Assert.AreEqual(50, request.Body["maxResults"]!.GetValue<int>());
        Assert.IsNull(request.Body["nextPageToken"]);

        JsonNode page = JsonNode.Parse(result)!;
        Assert.AreEqual("t2", page["nextPageToken"]!.GetValue<string>());
        Assert.IsFalse(page["isLast"]!.GetValue<bool>());
        Assert.AreEqual("P-1", page["issues"]![0]!["key"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that the page size is capped and the page token is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Issues_WithLargePageAndToken_CapsSizeAndSendsToken()
    {
        // Arrange
        this.http.Respond("""{"issues":[],"isLast":true}""");

        // Act
        await this.tools.Issues("project = P", fields: "summary", maxResults: 500, nextPageToken: "t1");

        // Assert
        JsonNode body = this.http.LastRequest.Body!;
        Assert.AreEqual(100, body["maxResults"]!.GetValue<int>());
        Assert.AreEqual("t1", body["nextPageToken"]!.GetValue<string>());
        Assert.AreEqual("""["summary"]""", body["fields"]!.ToJsonString());
    }

    /// <summary>
    /// Verifies that the approximate total is requested separately and returned.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Issues_WithApproximateTotal_RequestsTheCount()
    {
        // Arrange
        this.http.Respond("""{"issues":[{"key":"P-1"}],"isLast":true}""").Respond("""{"count":42}""");

        // Act
        string result = await this.tools.Issues("project = P", includeApproximateTotal: true);

        // Assert
        Assert.AreEqual("rest/api/3/search/approximate-count", this.http.LastRequest.Path);
        Assert.AreEqual(42, JsonNode.Parse(result)!["approximateTotal"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies that JQL errors are reported as invalid.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Validate_WithErrors_ReturnsInvalid()
    {
        // Arrange
        this.http.Respond("""{"queries":[{"query":"x","errors":["Field 'x' does not exist."]}]}""");

        // Act
        string result = await this.tools.Validate("x = 1");

        // Assert
        Assert.AreEqual("rest/api/3/jql/parse?validation=strict", this.http.LastRequest.Path);
        JsonNode validation = JsonNode.Parse(result)!;
        Assert.IsFalse(validation["valid"]!.GetValue<bool>());
        Assert.AreEqual(1, validation["errors"]!.AsArray().Count);
    }
}
