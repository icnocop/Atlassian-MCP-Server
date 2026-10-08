// <copyright file="SprintToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Agile;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Agile;

/// <summary>
/// Tests for <see cref="SprintTools"/>.
/// </summary>
[TestClass]
public sealed class SprintToolsTests
{
    private RecordingHttpClient http = null!;
    private SprintTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client and a fixed clock.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new SprintTools(new JiraClient(this.http), new FixedTimeProvider(new DateTimeOffset(2026, 1, 5, 9, 30, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Verifies that listing sprints sends the state filter without spaces.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_WithStates_SendsStateFilter()
    {
        // Arrange
        this.http.Respond("""{"values":[]}""");

        // Act
        await this.tools.GetAll(7, state: "active, future", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/board/7/sprint?state=active%2Cfuture", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that creating a sprint sends only the values that are given.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithNameAndGoal_PostsSprint()
    {
        // Arrange
        this.http.Respond("""{"id":42}""");

        // Act
        await this.tools.Create("Sprint 1", 7, goal: "Ship it", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/sprint", this.http.LastRequest.Path);
        Assert.AreEqual("""{"name":"Sprint 1","originBoardId":7,"goal":"Ship it"}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that updating a sprint posts a partial update.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithGoal_PostsPartialUpdate()
    {
        // Arrange
        this.http.Respond("""{"id":42}""");

        // Act
        await this.tools.Update(42, goal: "New goal", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Post, this.http.LastRequest.Method);
        Assert.AreEqual("rest/agile/1.0/sprint/42", this.http.LastRequest.Path);
        Assert.AreEqual("""{"goal":"New goal"}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that an update with nothing to change is rejected.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithNoValues_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Update(42, cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// Verifies that starting a sprint without a start date uses the current time.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Start_WithoutStartDate_UsesNow()
    {
        // Arrange
        this.http.Respond("""{"id":42,"state":"active"}""");

        // Act
        await this.tools.Start(42, "2026-01-19T17:00:00.000Z", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/sprint/42", this.http.LastRequest.Path);
        Assert.AreEqual(
            """{"state":"active","startDate":"2026-01-05T09:30:00.000Z","endDate":"2026-01-19T17:00:00.000Z"}""",
            this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that starting a sprint without an end date is rejected.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Start_WithoutEndDate_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Start(42, " ", cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// Verifies that closing a sprint without a completion date uses the current time.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Close_WithoutCompleteDate_UsesNow()
    {
        // Arrange
        this.http.Respond("""{"id":42,"state":"closed"}""");

        // Act
        await this.tools.Close(42, cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("""{"state":"closed","completeDate":"2026-01-05T09:30:00.000Z"}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that closing a sprint with a completion date sends it unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Close_WithCompleteDate_SendsTheDate()
    {
        // Arrange
        this.http.Respond("""{"id":42}""");

        // Act
        await this.tools.Close(42, "2026-01-18T12:00:00.000Z", CancellationToken.None);

        // Assert
        Assert.AreEqual("""{"state":"closed","completeDate":"2026-01-18T12:00:00.000Z"}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that deleting a sprint sends a DELETE request.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithSprintId_SendsDeleteRequest()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        string result = await this.tools.Delete(42, CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/agile/1.0/sprint/42", this.http.LastRequest.Path);
        StringAssert.Contains(result, "Deleted sprint 42.");
    }

    /// <summary>
    /// Verifies that moving issues to a sprint posts the issue keys.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MoveIssues_WithIssueKeys_PostsTheKeys()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.MoveIssues(42, "PROJ-1", CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/agile/1.0/sprint/42/issue", this.http.LastRequest.Path);
        Assert.AreEqual("""{"issues":["PROJ-1"]}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// A clock that always returns the same time.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedTimeProvider"/> class.
        /// </summary>
        /// <param name="now">The time to return.</param>
        public FixedTimeProvider(DateTimeOffset now)
        {
            this.now = now;
        }

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => this.now;
    }
}
