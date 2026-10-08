// <copyright file="RankToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Agile;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Agile;

/// <summary>
/// Tests for <see cref="RankTools"/>.
/// </summary>
[TestClass]
public sealed class RankToolsTests
{
    private RecordingHttpClient http = null!;
    private RankTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new RankTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that ranking issues before an anchor sends the keys and the anchor.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MoveIssues_WithBeforeAnchor_PutsRank()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.MoveIssues("PROJ-1,PROJ-2", rankBeforeIssue: "PROJ-9", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Put, this.http.LastRequest.Method);
        Assert.AreEqual("rest/agile/1.0/issue/rank", this.http.LastRequest.Path);
        Assert.AreEqual("""{"issues":["PROJ-1","PROJ-2"],"rankBeforeIssue":"PROJ-9"}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that ranking issues with both anchors is rejected before any request is sent.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MoveIssues_WithBothAnchors_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.MoveIssues("PROJ-1", "PROJ-8", "PROJ-9", CancellationToken.None));
        Assert.AreEqual(0, this.http.Requests.Count);
    }

    /// <summary>
    /// Verifies that ranking issues without an anchor is rejected.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MoveIssues_WithNoAnchor_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.MoveIssues("PROJ-1", cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// Verifies that ranking an epic after another sends the after anchor to the epic rank endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MoveEpic_WithAfterAnchor_PutsRank()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.MoveEpic("PROJ-10", rankAfterEpic: "PROJ-20", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/epic/PROJ-10/rank", this.http.LastRequest.Path);
        Assert.AreEqual("""{"rankAfterEpic":"PROJ-20"}""", this.http.LastRequest.Body!.ToJsonString());
    }
}
