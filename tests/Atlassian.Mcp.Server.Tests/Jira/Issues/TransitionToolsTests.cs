// <copyright file="TransitionToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Issues;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Issues;

/// <summary>
/// Tests for <see cref="TransitionTools"/>.
/// </summary>
[TestClass]
public sealed class TransitionToolsTests : IDisposable
{
    private const string Transitions = """
        {"transitions":[
          {"id":"31","name":"Done","to":{"name":"Closed"}},
          {"id":"11","name":"Start","to":{"name":"In Progress"}}
        ]}
        """;

    /// <summary>The transitions with the unavailable ones, where "In Review" is hidden by a condition.</summary>
    private const string TransitionsWithUnavailable = """
        {"transitions":[
          {"id":"11","name":"To Do","to":{"name":"To Do"},"isAvailable":true},
          {"id":"31","name":"In Review","to":{"name":"In Review"},"isAvailable":false,"isConditional":true}
        ]}
        """;

    private const string IssueProjectAndType = """{"fields":{"project":{"id":"11800"},"issuetype":{"id":"10902"}}}""";

    /// <summary>The workflow: "In Review" requires the Severity field, as a condition and as a validator.</summary>
    private const string Workflow = """
        {"workflows":[{"name":"Software Workflow","transitions":[
          {"id":"11","name":"To Do","conditions":{"operation":"ALL","conditions":[],"conditionGroups":[]}},
          {"id":"31","name":"In Review",
           "conditions":{"operation":"ALL","conditionGroups":[],"conditions":[
             {"ruleKey":"system:check-field-value","parameters":{"fieldId":"customfield_10050","fieldValue":"[\"\"]","comparator":"!=","comparisonType":"OPTIONID"}}]},
           "validators":[
             {"ruleKey":"system:validate-field-value","parameters":{"ruleType":"fieldRequired","fieldsRequired":"customfield_10050","errorMessage":"Set a severity first."}}]}
        ]}]}
        """;

    private const string Fields = """[{"id":"customfield_10050","name":"Severity","schema":{"type":"option"}}]""";

    private RecordingHttpClient http = null!;
    private JiraMetadataCache metadata = null!;
    private TransitionTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.metadata = new JiraMetadataCache();
        this.tools = new TransitionTools(new JiraClient(this.http), this.metadata);
    }

    /// <summary>
    /// Disposes the metadata cache.
    /// </summary>
    public void Dispose() => this.metadata.Dispose();

    /// <summary>
    /// Verifies that the transitions are listed as before by default: only the available ones, with no
    /// workflow lookup and no availability or rules.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_ByDefault_ListsAvailableTransitionsOnly()
    {
        // Arrange
        this.http.Respond(Transitions);

        // Act
        JsonArray result = JsonNode.Parse(await this.tools.GetAll("PROJ-1"))!.AsArray();

        // Assert
        Assert.AreEqual(1, this.http.Requests.Count);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/transitions?expand=transitions.fields", this.http.Requests[0].Path);
        Assert.HasCount(2, result);
        Assert.IsNull(result[0]!["isAvailable"]);
        Assert.IsNull(result[0]!["conditions"]);
    }

    /// <summary>
    /// Verifies that the hidden transitions are listed on request, each with its availability and its
    /// conditions and validators described with field names.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_IncludingUnavailable_DescribesConditionsAndValidators()
    {
        // Arrange
        this.http.Respond(TransitionsWithUnavailable).Respond(IssueProjectAndType).Respond(Workflow).Respond(Fields);

        // Act
        JsonArray result = JsonNode.Parse(await this.tools.GetAll("PROJ-1", includeUnavailable: true))!.AsArray();

        // Assert
        StringAssert.Contains(this.http.Requests[0].Path, "includeUnavailableTransitions=true", StringComparison.Ordinal);
        Assert.AreEqual("rest/api/3/issue/PROJ-1?fields=project,issuetype", this.http.Requests[1].Path);
        Assert.AreEqual(HttpMethod.Post, this.http.Requests[2].Method);
        Assert.AreEqual("11800", this.http.Requests[2].Body!["projectAndIssueTypes"]![0]!["projectId"]!.GetValue<string>());

        JsonNode readyForQa = result.Single(transition => (string?)transition!["id"] == "31")!;
        Assert.IsFalse(readyForQa["isAvailable"]!.GetValue<bool>());
        Assert.AreEqual("Severity (customfield_10050) is not empty", readyForQa["conditions"]!.GetValue<string>());
        Assert.AreEqual("Severity (customfield_10050) must have a value (\"Set a severity first.\")", readyForQa["validators"]![0]!.GetValue<string>());
        Assert.IsNull(readyForQa["rulesUnavailable"]);

        JsonNode toDo = result.Single(transition => (string?)transition!["id"] == "11")!;
        Assert.IsTrue(toDo["isAvailable"]!.GetValue<bool>());
        Assert.IsNull(toDo["conditions"]);
    }

    /// <summary>
    /// Verifies that the hidden transitions are still listed when the workflow cannot be read, with the reason.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetAll_IncludingUnavailableWithoutWorkflowPermission_ReportsWhyRulesAreMissing()
    {
        // Arrange
        this.http.Respond(TransitionsWithUnavailable).Respond(IssueProjectAndType)
            .Fail(new AtlassianApiException(HttpStatusCode.Forbidden, "You are not an administrator."));

        // Act
        JsonArray result = JsonNode.Parse(await this.tools.GetAll("PROJ-1", includeUnavailable: true))!.AsArray();

        // Assert
        JsonNode readyForQa = result.Single(transition => (string?)transition!["id"] == "31")!;
        Assert.IsFalse(readyForQa["isAvailable"]!.GetValue<bool>());
        Assert.IsNull(readyForQa["conditions"]);
        StringAssert.Contains(readyForQa["rulesUnavailable"]!.GetValue<string>(), "You are not an administrator.", StringComparison.Ordinal);
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
    /// Verifies that a target status name is resolved, and the comment is posted as ADF on its own after
    /// the transition, not as an update in the transition request, which Jira ignores for a transition
    /// that has no screen.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithTargetStatusAndComment_PostsAdfCommentAfterTransition()
    {
        // Arrange
        this.http.Respond(Transitions).Respond(null).Respond("""{"id":"10001"}""");

        // Act
        string result = await this.tools.Apply("PROJ-1", "in progress", comment: "**Started**");

        // Assert
        Assert.HasCount(3, this.http.Requests);
        RecordedRequest transition = this.http.Requests[1];
        Assert.AreEqual("rest/api/3/issue/PROJ-1/transitions", transition.Path);
        Assert.AreEqual("11", transition.Body!["transition"]!["id"]!.GetValue<string>());
        Assert.IsNull(transition.Body["update"]);
        Assert.IsNull(transition.Body["fields"]);

        RecordedRequest comment = this.http.Requests[2];
        Assert.AreEqual(HttpMethod.Post, comment.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/comment", comment.Path);
        Assert.AreEqual("doc", comment.Body!["body"]!["type"]!.GetValue<string>());
        StringAssert.Contains(result, "added comment 10001", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a comment that cannot be added is reported as such, saying that the transition was
    /// still applied, rather than as success or as a failed transition.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WhenCommentFails_ThrowsSayingTransitionWasApplied()
    {
        // Arrange
        this.http.Respond(Transitions).Respond(null)
            .Fail(new AtlassianApiException(HttpStatusCode.Forbidden, "You cannot add comments."));

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Apply("PROJ-1", "Done", comment: "Closing."));

        // Assert
        StringAssert.Contains(exception.Message, "Applied transition 'Done' to PROJ-1, but the comment could not be added", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "You cannot add comments.", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "atlassian_jira_add_comment", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that without a comment, only the transition is posted.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithoutComment_PostsNoComment()
    {
        // Arrange
        this.http.Respond(Transitions).Respond(null);

        // Act
        string result = await this.tools.Apply("PROJ-1", "Done");

        // Assert
        Assert.HasCount(2, this.http.Requests);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/transitions", this.http.LastRequest.Path);
        StringAssert.Contains(result, "Applied transition 'Done' to PROJ-1.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a transition that does not exist is reported with the available ones, without
    /// reading the workflow.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithUnknownTransition_ThrowsListingAvailable()
    {
        // Arrange
        this.http.Respond(Transitions).Respond(Transitions);

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Apply("PROJ-1", "Reopen"));

        // Assert
        StringAssert.Contains(exception.Message, "Done (31)", StringComparison.Ordinal);
        Assert.HasCount(2, this.http.Requests);
        StringAssert.Contains(this.http.Requests[1].Path, "includeUnavailableTransitions=true", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a transition that a workflow condition hides is reported with the condition to meet,
    /// rather than as not existing, and nothing is applied.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithTransitionHiddenByCondition_ThrowsNamingTheUnmetCondition()
    {
        // Arrange
        const string Available = """{"transitions":[{"id":"11","name":"To Do","to":{"name":"To Do"}}]}""";
        this.http.Respond(Available).Respond(TransitionsWithUnavailable).Respond(IssueProjectAndType).Respond(Workflow).Respond(Fields);

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Apply("PROJ-1", "In Review"));

        // Assert
        StringAssert.Contains(exception.Message, "'In Review' (31)", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "It requires: Severity (customfield_10050) is not empty.", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "To Do (11)", StringComparison.Ordinal);
        Assert.IsFalse(this.http.Requests.Any(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/transitions", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies that an unknown transition is still reported as not available when looking for hidden
    /// transitions fails.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Apply_WithUnknownTransitionWhenHiddenLookupFails_ThrowsListingAvailable()
    {
        // Arrange
        this.http.Respond(Transitions).Fail(new AtlassianApiException(HttpStatusCode.BadRequest, "Bad request."));

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Apply("PROJ-1", "Reopen"));

        // Assert
        StringAssert.Contains(exception.Message, "is not available for this issue", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "Done (31)", StringComparison.Ordinal);
    }
}
