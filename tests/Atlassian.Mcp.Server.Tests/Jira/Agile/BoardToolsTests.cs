// <copyright file="BoardToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Agile;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Agile;

/// <summary>
/// Tests for <see cref="BoardTools"/>.
/// </summary>
[TestClass]
public sealed class BoardToolsTests
{
    private RecordingHttpClient http = null!;
    private BoardTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new BoardTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that listing boards sends only the filters that are given.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_WithFilters_SendsOnlyGivenParameters()
    {
        // Arrange
        this.http.Respond("""{"values":[{"id":1,"name":"Team board"}]}""");

        // Act
        string result = await this.tools.GetAll(name: "Team", type: "scrum", maxResults: 10, cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/board?name=Team&type=scrum&maxResults=10", this.http.LastRequest.Path);
        Assert.AreEqual("""{"values":[{"id":1,"name":"Team board"}]}""", result);
    }

    /// <summary>
    /// Verifies that getting board issues sends the JQL and the normalized field list.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetIssues_WithJqlAndFields_SendsQuery()
    {
        // Arrange
        this.http.Respond("""{"issues":[]}""");

        // Act
        await this.tools.GetIssues(7, jql: "status = Done", fields: "summary, status", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/board/7/issue?jql=status%20%3D%20Done&fields=summary%2Cstatus", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that getting the backlog uses the backlog endpoint of the board.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetBacklog_WithBoardId_UsesBacklogEndpoint()
    {
        // Arrange
        this.http.Respond("""{"issues":[]}""");

        // Act
        await this.tools.GetBacklog(7, cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/board/7/backlog", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that moving issues to the backlog posts the issue keys.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MoveToBacklog_WithIssueKeys_PostsTheKeys()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        string result = await this.tools.MoveToBacklog("PROJ-1, PROJ-2", CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Post, this.http.LastRequest.Method);
        Assert.AreEqual("rest/agile/1.0/backlog/issue", this.http.LastRequest.Path);
        Assert.AreEqual("""{"issues":["PROJ-1","PROJ-2"]}""", this.http.LastRequest.Body!.ToJsonString());
        StringAssert.Contains(result, "PROJ-1, PROJ-2");
    }

    /// <summary>
    /// Verifies that an empty issue key list is rejected before any request is sent.
    /// </summary>
    [TestMethod]
    public void RequireKeys_WithEmptyList_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => BoardTools.RequireKeys(" , "));
    }
}
