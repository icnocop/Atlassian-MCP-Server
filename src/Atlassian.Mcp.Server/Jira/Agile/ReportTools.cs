// <copyright file="ReportTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Agile;

/// <summary>
/// Tools for the sprint report, burndown, and velocity data that Jira Software boards display.
/// These come from the board report endpoints, because the public Agile API does not offer them.
/// The responses are condensed to the values a model needs, to save context.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraAgile)]
public sealed class ReportTools
{
    /// <summary>The sprint report lists that hold issues, and the names they get in the result.</summary>
    private static readonly (string Source, string Target)[] SprintReportLists =
    [
        ("completedIssues", "completed"),
        ("issuesNotCompletedInCurrentSprint", "notCompleted"),
        ("puntedIssues", "removed"),
        ("issuesCompletedInAnotherSprint", "completedInAnotherSprint"),
    ];

    /// <summary>The sprint report totals, and the names they get in the result.</summary>
    private static readonly (string Source, string Target)[] SprintReportTotals =
    [
        ("completedIssuesEstimateSum", "completed"),
        ("issuesNotCompletedEstimateSum", "notCompleted"),
        ("puntedIssuesEstimateSum", "removed"),
        ("allIssuesEstimateSum", "all"),
    ];

    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public ReportTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets the sprint report for the specified sprint.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The condensed sprint report.</returns>
    [McpServerTool(Name = "atlassian_jira_get_sprint_report", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the sprint report for the specified sprint, as the board shows it: the issues completed, not completed, removed from the sprint, and added during it, with their estimates and the totals.")]
    public async Task<string> GetSprintReport(
        [Description("The ID of the board that the sprint belongs to.")] int boardId,
        [Description("The sprint ID.")] int sprintId,
        CancellationToken cancellationToken = default)
    {
        JsonNode? report = await this.jira.GetReportAsync($"rapid/charts/sprintreport?rapidViewId={boardId}&sprintId={sprintId}", cancellationToken);
        return ToolResult.Json(CondenseSprintReport(report));
    }

    /// <summary>
    /// Gets the burndown data for the specified sprint.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The condensed burndown data.</returns>
    [McpServerTool(Name = "atlassian_jira_get_burndown_chart", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the burndown data for the specified sprint, as the board's burndown chart shows it: the sprint dates and every scope and estimate change, in time order.")]
    public async Task<string> GetBurndown(
        [Description("The ID of the board that the sprint belongs to.")] int boardId,
        [Description("The sprint ID.")] int sprintId,
        CancellationToken cancellationToken = default)
    {
        JsonNode? chart = await this.jira.GetReportAsync($"rapid/charts/scopechangeburndownchart?rapidViewId={boardId}&sprintId={sprintId}", cancellationToken);
        return ToolResult.Json(CondenseBurndown(chart));
    }

    /// <summary>
    /// Gets the velocity data for the specified board.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The condensed velocity data.</returns>
    [McpServerTool(Name = "atlassian_jira_get_velocity_chart", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the velocity data for the specified board, as its velocity chart shows it: the committed and completed estimates of each recent sprint.")]
    public async Task<string> GetVelocity(
        [Description("The board ID.")] int boardId,
        CancellationToken cancellationToken = default)
    {
        JsonNode? chart = await this.jira.GetReportAsync($"rapid/charts/velocity?rapidViewId={boardId}", cancellationToken);
        return ToolResult.Json(CondenseVelocity(chart));
    }

    /// <summary>
    /// Condenses a sprint report to the sprint, its issue lists, and its totals.
    /// </summary>
    /// <param name="report">The sprint report response.</param>
    /// <returns>The condensed report.</returns>
    internal static JsonObject CondenseSprintReport(JsonNode? report)
    {
        JsonNode? contents = report?["contents"];
        JsonNode? sprint = report?["sprint"];

        var result = new JsonObject
        {
            ["sprint"] = Pick(sprint, "id", "name", "state", "goal", "startDate", "endDate", "completeDate"),
        };

        foreach ((string source, string target) in SprintReportLists)
        {
            result[target] = new JsonArray((contents?[source] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(issue => (JsonNode)new JsonObject
                {
                    ["key"] = issue["key"]?.DeepClone(),
                    ["summary"] = issue["summary"]?.DeepClone(),
                    ["type"] = issue["typeName"]?.DeepClone(),
                    ["status"] = issue["statusName"]?.DeepClone(),
                    ["assignee"] = issue["assigneeName"]?.DeepClone(),
                    ["estimate"] = (issue["currentEstimateStatistic"] ?? issue["estimateStatistic"])?["statFieldValue"]?["value"]?.DeepClone(),
                })
                .ToArray());
        }

        if (contents?["issueKeysAddedDuringSprint"] is JsonObject added)
        {
            result["addedDuringSprint"] = new JsonArray(added.Select(pair => (JsonNode)pair.Key).ToArray());
        }

        var totals = new JsonObject();
        foreach ((string source, string target) in SprintReportTotals)
        {
            totals[target] = contents?[source]?["value"]?.DeepClone();
        }

        result["totals"] = totals;
        return result;
    }

    /// <summary>
    /// Condenses burndown data to the sprint dates and a time-ordered list of changes.
    /// </summary>
    /// <param name="chart">The burndown chart response.</param>
    /// <returns>The condensed data.</returns>
    internal static JsonObject CondenseBurndown(JsonNode? chart)
    {
        var events = new List<(long Time, JsonObject Event)>();

        if (chart?["changes"] is JsonObject changes)
        {
            foreach ((string timestamp, JsonNode? entries) in changes)
            {
                long time = long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;

                foreach (JsonObject change in (entries as JsonArray ?? []).OfType<JsonObject>())
                {
                    var item = new JsonObject
                    {
                        ["time"] = FormatTime(time),
                        ["key"] = change["key"]?.DeepClone(),
                    };

                    if (change["added"] is JsonValue addedValue && addedValue.TryGetValue(out bool wasAdded))
                    {
                        item[wasAdded ? "addedToSprint" : "removedFromSprint"] = true;
                    }

                    if (change["statC"] is JsonObject statistic)
                    {
                        item["estimateFrom"] = statistic["oldValue"]?.DeepClone();
                        item["estimateTo"] = statistic["newValue"]?.DeepClone();
                    }

                    if (change["column"]?["done"] is JsonValue doneValue && doneValue.TryGetValue(out bool done))
                    {
                        item["done"] = done;
                    }

                    events.Add((time, item));
                }
            }
        }

        return new JsonObject
        {
            ["startTime"] = FormatTime(chart?["startTime"]),
            ["endTime"] = FormatTime(chart?["endTime"]),
            ["completeTime"] = FormatTime(chart?["completeTime"]),
            ["statistic"] = chart?["statisticField"]?["name"]?.DeepClone(),
            ["changes"] = new JsonArray(events.OrderBy(entry => entry.Time).Select(entry => (JsonNode)entry.Event).ToArray()),
        };
    }

    /// <summary>
    /// Condenses velocity data to one entry per sprint.
    /// </summary>
    /// <param name="chart">The velocity chart response.</param>
    /// <returns>The condensed data.</returns>
    internal static JsonObject CondenseVelocity(JsonNode? chart)
    {
        JsonNode? entries = chart?["velocityStatEntries"];

        var sprints = (chart?["sprints"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(sprint =>
            {
                string id = sprint["id"]?.ToString() ?? string.Empty;
                return (JsonNode)new JsonObject
                {
                    ["sprintId"] = sprint["id"]?.DeepClone(),
                    ["name"] = sprint["name"]?.DeepClone(),
                    ["state"] = sprint["state"]?.DeepClone(),
                    ["committed"] = entries?[id]?["estimated"]?["value"]?.DeepClone(),
                    ["completed"] = entries?[id]?["completed"]?["value"]?.DeepClone(),
                };
            })
            .ToArray();

        return new JsonObject { ["sprints"] = new JsonArray(sprints) };
    }

    private static JsonObject Pick(JsonNode? source, params string[] names)
    {
        var target = new JsonObject();
        foreach (string name in names)
        {
            target[name] = source?[name]?.DeepClone();
        }

        return target;
    }

    private static JsonValue? FormatTime(JsonNode? milliseconds)
        => milliseconds is JsonValue value && value.TryGetValue(out long time) ? FormatTime(time) : null;

    private static JsonValue? FormatTime(long milliseconds)
        => milliseconds <= 0
            ? null
            : JsonValue.Create(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
}
