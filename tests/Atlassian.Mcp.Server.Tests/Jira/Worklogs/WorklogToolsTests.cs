// <copyright file="WorklogToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Worklogs;
using Atlassian.Mcp.Server.Tests.Jira.Projects;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Worklogs;

/// <summary>
/// Tests for <see cref="WorklogTools"/> and <see cref="TimeTrackingTools"/>.
/// </summary>
[TestClass]
public sealed class WorklogToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    private RecordingHttpClient http = null!;
    private WorklogTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client and a fixed clock.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new WorklogTools(new JiraClient(this.http)) { Clock = new FixedTimeProvider(Now) };
    }

    /// <summary>
    /// Verifies that a start time with an offset is converted to the UTC format that Jira requires.
    /// </summary>
    [TestMethod]
    public void FormatStarted_WithOffset_ReturnsUtcInJiraFormat()
    {
        // Act
        string result = WorklogTools.FormatStarted("2026-01-15T11:30:00+02:00", TimeProvider.System);

        // Assert
        Assert.AreEqual("2026-01-15T09:30:00.000+0000", result);
    }

    /// <summary>
    /// Verifies that a start time that is not a date is rejected.
    /// </summary>
    [TestMethod]
    public void FormatStarted_WithText_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => WorklogTools.FormatStarted("yesterday", TimeProvider.System));
    }

    /// <summary>
    /// Verifies that adding a worklog defaults the start time to now and converts the comment to ADF.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithoutStarted_UsesNowAndConvertsTheComment()
    {
        // Arrange
        this.http.Respond("""{"id":"10500"}""");

        // Act
        await this.tools.Add("PROJ-1", "2h", comment: "Fixed **it**", cancellationToken: CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/worklog", request.Path);
        Assert.AreEqual("2026-01-15T09:30:00.000+0000", request.Body!["started"]!.GetValue<string>());
        Assert.AreEqual("doc", request.Body["comment"]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that the manual adjustment passes the reduceBy query parameter.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithManualAdjustment_PassesReduceBy()
    {
        // Arrange
        this.http.Respond("""{"id":"10500"}""");

        // Act
        await this.tools.Add("PROJ-1", "1h", adjustEstimate: "manual", reduceBy: "30m", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/issue/PROJ-1/worklog?adjustEstimate=manual&reduceBy=30m", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that the new adjustment requires a new estimate.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithNewAdjustmentAndNoEstimate_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(
            () => this.tools.Add("PROJ-1", "1h", adjustEstimate: "new", cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// Verifies that deleting a worklog with the new adjustment passes the new estimate.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithNewAdjustment_PassesNewEstimate()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Delete("PROJ-1", "10500", "new", "3h", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/worklog/10500?adjustEstimate=new&newEstimate=3h", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that getting worklogs by IDs posts the numeric IDs.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetByIds_WithIds_PostsNumericIds()
    {
        // Arrange
        this.http.Respond("[]");

        // Act
        await this.tools.GetByIds("1, 2", CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("rest/api/3/worklog/list", request.Path);
        Assert.AreEqual("""{"ids":[1,2]}""", request.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that setting the estimate sends only the estimates that are passed.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task SetEstimate_WithRemainingOnly_SendsOnlyTheRemainingEstimate()
    {
        // Arrange
        this.http.Respond(null);
        var timeTracking = new TimeTrackingTools(new JiraClient(this.http));

        // Act
        await timeTracking.SetEstimate("PROJ-1", remainingEstimate: "4h", cancellationToken: CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Put, request.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1", request.Path);
        Assert.AreEqual("""{"fields":{"timetracking":{"remainingEstimate":"4h"}}}""", request.Body!.ToJsonString());
    }
}
