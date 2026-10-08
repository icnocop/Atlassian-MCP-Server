// <copyright file="TransitionToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Issues;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Issues;

/// <summary>
/// Tests for <see cref="TransitionTools"/>.
/// </summary>
[TestClass]
public sealed class TransitionToolsTests
{
    private const string Transitions = """
        {"transitions":[
          {"id":"31","name":"Done","to":{"name":"Closed"}},
          {"id":"11","name":"Start","to":{"name":"In Progress"}}
        ]}
        """;

    private RecordingHttpClient http = null!;
    private TransitionTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new TransitionTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that a transition name is resolved, ignoring case, and the resolution is set.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithTransitionName_ResolvesIdAndSetsResolution()
    {
        // Arrange
        this.http.Respond(Transitions).Respond(null);

        // Act
        await this.tools.Apply("PROJ-1", "done", resolution: "Fixed");

        // Assert
        Assert.AreEqual("rest/api/3/issue/PROJ-1/transitions", this.http.Requests[0].Path);
        JsonNode body = this.http.LastRequest.Body!;
        Assert.AreEqual("31", body["transition"]!["id"]!.GetValue<string>());
        Assert.AreEqual("Fixed", body["fields"]!["resolution"]!["name"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a target status name is resolved, and a comment is added as ADF.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithTargetStatusAndComment_AddsAdfComment()
    {
        // Arrange
        this.http.Respond(Transitions).Respond(null);

        // Act
        await this.tools.Apply("PROJ-1", "in progress", comment: "**Started**");

        // Assert
        JsonNode body = this.http.LastRequest.Body!;
        Assert.AreEqual("11", body["transition"]!["id"]!.GetValue<string>());
        Assert.AreEqual("doc", body["update"]!["comment"]![0]!["add"]!["body"]!["type"]!.GetValue<string>());
        Assert.IsNull(body["fields"]);
    }

    /// <summary>
    /// Verifies that an unavailable transition is reported with the available ones.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithUnknownTransition_ThrowsListingAvailable()
    {
        // Arrange
        this.http.Respond(Transitions);

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Apply("PROJ-1", "Reopen"));

        // Assert
        StringAssert.Contains(exception.Message, "Done (31)", StringComparison.Ordinal);
        Assert.AreEqual(1, this.http.Requests.Count);
    }
}
