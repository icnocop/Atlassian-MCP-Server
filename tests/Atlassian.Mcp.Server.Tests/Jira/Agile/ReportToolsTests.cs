// <copyright file="ReportToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Agile;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Agile;

/// <summary>
/// Tests for <see cref="ReportTools"/>.
/// </summary>
[TestClass]
public sealed class ReportToolsTests
{
    private RecordingHttpClient http = null!;
    private ReportTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new ReportTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that the sprint report is requested from the board report endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetSprintReport_WithIds_UsesReportEndpoint()
    {
        // Arrange
        this.http.Respond("""{"contents":{},"sprint":{"id":42}}""");

        // Act
        await this.tools.GetSprintReport(7, 42, CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/greenhopper/1.0/rapid/charts/sprintreport?rapidViewId=7&sprintId=42", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that the burndown and velocity data are requested from the board report endpoints.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetBurndownAndVelocity_WithIds_UseReportEndpoints()
    {
        // Arrange
        this.http.Respond("{}").Respond("{}");

        // Act
        await this.tools.GetBurndown(7, 42, CancellationToken.None);
        await this.tools.GetVelocity(7, CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/greenhopper/1.0/rapid/charts/scopechangeburndownchart?rapidViewId=7&sprintId=42", this.http.Requests[0].Path);
        Assert.AreEqual("rest/greenhopper/1.0/rapid/charts/velocity?rapidViewId=7", this.http.Requests[1].Path);
    }

    /// <summary>
    /// Verifies that the sprint report is condensed to the issue lists, the added keys, and the totals.
    /// </summary>
    [TestMethod]
    public void CondenseSprintReport_WithFullReport_KeepsOnlyTheSummary()
    {
        // Arrange
        JsonNode report = JsonNode.Parse("""
            {
              "contents": {
                "completedIssues": [
                  { "id": 1, "key": "PROJ-1", "summary": "Done work", "typeName": "Story", "statusName": "Done",
                    "assigneeName": "Ada", "hidden": false, "priorityUrl": "https://x/p.svg",
                    "estimateStatistic": { "statFieldId": "customfield_10016", "statFieldValue": { "value": 3.0 } },
                    "currentEstimateStatistic": { "statFieldId": "customfield_10016", "statFieldValue": { "value": 5.0 } } }
                ],
                "issuesNotCompletedInCurrentSprint": [ { "key": "PROJ-2", "summary": "Open work" } ],
                "puntedIssues": [],
                "issuesCompletedInAnotherSprint": [],
                "completedIssuesEstimateSum": { "value": 5.0, "text": "5.0" },
                "issuesNotCompletedEstimateSum": { "text": "null" },
                "allIssuesEstimateSum": { "value": 5.0, "text": "5.0" },
                "issueKeysAddedDuringSprint": { "PROJ-2": true }
              },
              "sprint": { "id": 42, "name": "Sprint 1", "state": "CLOSED", "linkedPagesCount": 0 }
            }
            """)!;

        // Act
        JsonObject result = ReportTools.CondenseSprintReport(report);

        // Assert
        Assert.AreEqual("Sprint 1", result["sprint"]!["name"]!.GetValue<string>());
        Assert.IsNull(result["sprint"]!["linkedPagesCount"]);
        JsonNode completed = result["completed"]![0]!;
        Assert.AreEqual("PROJ-1", completed["key"]!.GetValue<string>());
        Assert.AreEqual(5.0, completed["estimate"]!.GetValue<double>());
        Assert.IsNull(completed["priorityUrl"]);
        Assert.AreEqual("PROJ-2", result["notCompleted"]![0]!["key"]!.GetValue<string>());
        Assert.AreEqual("PROJ-2", result["addedDuringSprint"]![0]!.GetValue<string>());
        Assert.AreEqual(5.0, result["totals"]!["completed"]!.GetValue<double>());
        Assert.IsNull(result["totals"]!["notCompleted"]);
    }

    /// <summary>
    /// Verifies that burndown changes are flattened and ordered by time.
    /// </summary>
    [TestMethod]
    public void CondenseBurndown_WithChanges_OrdersEventsByTime()
    {
        // Arrange
        JsonNode chart = JsonNode.Parse("""
            {
              "startTime": 1767600000000,
              "endTime": 1768809600000,
              "statisticField": { "name": "Story Points" },
              "changes": {
                "1767700000000": [ { "key": "PROJ-2", "column": { "done": true } } ],
                "1767600000000": [ { "key": "PROJ-1", "added": true, "statC": { "newValue": 3.0 } } ]
              }
            }
            """)!;

        // Act
        JsonObject result = ReportTools.CondenseBurndown(chart);

        // Assert
        Assert.AreEqual("2026-01-05T08:00:00Z", result["startTime"]!.GetValue<string>());
        Assert.AreEqual("Story Points", result["statistic"]!.GetValue<string>());
        JsonArray changes = result["changes"]!.AsArray();
        Assert.AreEqual("PROJ-1", changes[0]!["key"]!.GetValue<string>());
        Assert.IsTrue(changes[0]!["addedToSprint"]!.GetValue<bool>());
        Assert.AreEqual(3.0, changes[0]!["estimateTo"]!.GetValue<double>());
        Assert.IsTrue(changes[1]!["done"]!.GetValue<bool>());
    }

    /// <summary>
    /// Verifies that velocity data is condensed to one entry per sprint.
    /// </summary>
    [TestMethod]
    public void CondenseVelocity_WithEntries_JoinsSprintsAndValues()
    {
        // Arrange
        JsonNode chart = JsonNode.Parse("""
            {
              "sprints": [ { "id": 42, "name": "Sprint 1", "state": "CLOSED", "goal": "x" } ],
              "velocityStatEntries": { "42": { "estimated": { "value": 13.0, "text": "13.0" }, "completed": { "value": 8.0, "text": "8.0" } } }
            }
            """)!;

        // Act
        JsonObject result = ReportTools.CondenseVelocity(chart);

        // Assert
        JsonNode sprint = result["sprints"]![0]!;
        Assert.AreEqual(42, sprint["sprintId"]!.GetValue<int>());
        Assert.AreEqual(13.0, sprint["committed"]!.GetValue<double>());
        Assert.AreEqual(8.0, sprint["completed"]!.GetValue<double>());
        Assert.IsNull(sprint["goal"]);
    }
}
