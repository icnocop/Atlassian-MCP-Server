// <copyright file="EpicToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Agile;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Agile;

/// <summary>
/// Tests for <see cref="EpicTools"/>.
/// </summary>
[TestClass]
public sealed class EpicToolsTests
{
    private RecordingHttpClient http = null!;
    private EpicTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new EpicTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that listing epics sends the done filter.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_WithDoneFilter_SendsDone()
    {
        // Arrange
        this.http.Respond("""{"values":[]}""");

        // Act
        await this.tools.GetAll(7, done: false, cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/board/7/epic?done=false", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that getting the issues of an epic uses the epic issue endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetIssues_WithEpicKey_UsesEpicIssueEndpoint()
    {
        // Arrange
        this.http.Respond("""{"issues":[]}""");

        // Act
        await this.tools.GetIssues("PROJ-10", maxResults: 5, cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/epic/PROJ-10/issue?maxResults=5", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that moving issues to an epic posts the keys to that epic.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MoveIssues_WithEpicAndKeys_PostsToTheEpic()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.MoveIssues("PROJ-10", "PROJ-1,PROJ-2", CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/epic/PROJ-10/issue", this.http.LastRequest.Path);
        Assert.AreEqual("""{"issues":["PROJ-1","PROJ-2"]}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that removing issues from their epics posts to the "none" epic.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RemoveIssues_WithKeys_PostsToNoneEpic()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.RemoveIssues("PROJ-1", CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Post, this.http.LastRequest.Method);
        Assert.AreEqual("rest/agile/1.0/epic/none/issue", this.http.LastRequest.Path);
    }
}
