// <copyright file="WorkflowRulesTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Issues;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Issues;

/// <summary>
/// Tests for <see cref="WorkflowRules"/>.
/// </summary>
[TestClass]
public sealed class WorkflowRulesTests : IDisposable
{
    private const string IssueProjectAndType = """{"fields":{"project":{"id":"1"},"issuetype":{"id":"2"}}}""";

    private RecordingHttpClient http = null!;
    private JiraMetadataCache metadata = null!;

    /// <summary>
    /// Creates a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.metadata = new JiraMetadataCache();
    }

    /// <summary>
    /// Disposes the metadata cache.
    /// </summary>
    public void Dispose() => this.metadata.Dispose();

    /// <summary>
    /// Verifies that the conditions of an ANY group, with a nested ALL group, are joined with "or" and
    /// "and", and that a field's compared values and an unknown rule are described.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task DescribeConditions_WithNestedGroupsAndValues_JoinsThemWithOrAndAnd()
    {
        // Arrange
        const string Workflow = """
            {"workflows":[{"transitions":[{"id":"5","conditions":{"operation":"ANY",
              "conditions":[{"ruleKey":"system:check-field-value","parameters":{"fieldId":"priority","fieldValue":"[\"1\",\"2\"]","comparator":"=","comparisonType":"OPTIONID"}}],
              "conditionGroups":[{"operation":"ALL","conditionGroups":[],"conditions":[
                {"ruleKey":"system:restrict-issue-transition","parameters":{"roleIds":"10002"}},
                {"ruleKey":"system:check-field-value","parameters":{"fieldId":"customfield_1","fieldValue":"[\"\"]","comparator":"="}}]}]}}]}]}
            """;
        this.http.Respond(IssueProjectAndType).Respond(Workflow).Respond("""[{"id":"priority","name":"Priority"},{"id":"customfield_1","name":"Team"}]""");

        // Act
        WorkflowRules rules = await WorkflowRules.LoadAsync(new JiraClient(this.http), this.metadata, "PROJ-1", CancellationToken.None);

        // Assert
        Assert.AreEqual(
            "Priority (priority) = 1, 2 (option IDs) or (system:restrict-issue-transition {\"roleIds\":\"10002\"} and Team (customfield_1) is empty)",
            rules.DescribeConditions("5"));
        Assert.IsNull(rules.Unavailable);
    }

    /// <summary>
    /// Verifies that a transition without conditions, or one the workflow does not have, has none described.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task DescribeConditions_WithoutConditions_ReturnsNull()
    {
        // Arrange
        this.http.Respond(IssueProjectAndType)
            .Respond("""{"workflows":[{"transitions":[{"id":"5","conditions":{"operation":"ALL","conditions":[],"conditionGroups":[]}}]}]}""")
            .Respond("[]");

        // Act
        WorkflowRules rules = await WorkflowRules.LoadAsync(new JiraClient(this.http), this.metadata, "PROJ-1", CancellationToken.None);

        // Assert
        Assert.IsNull(rules.DescribeConditions("5"));
        Assert.IsNull(rules.DescribeConditions("99"));
        Assert.IsEmpty(rules.DescribeValidators("99"));
    }

    /// <summary>
    /// Verifies that the rules are still described, with field IDs, when the field names cannot be read.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task DescribeConditions_WhenFieldNamesCannotBeRead_UsesFieldIds()
    {
        // Arrange
        this.http.Respond(IssueProjectAndType)
            .Respond("""{"workflows":[{"transitions":[{"id":"5","conditions":{"operation":"ALL","conditionGroups":[],"conditions":[{"ruleKey":"system:check-field-value","parameters":{"fieldId":"customfield_1","fieldValue":"[\"\"]","comparator":"!="}}]}}]}]}""")
            .Fail(new Atlassian.Mcp.Server.Common.Http.AtlassianApiException(System.Net.HttpStatusCode.Forbidden, "No."));

        // Act
        WorkflowRules rules = await WorkflowRules.LoadAsync(new JiraClient(this.http), this.metadata, "PROJ-1", CancellationToken.None);

        // Assert
        Assert.AreEqual("customfield_1 is not empty", rules.DescribeConditions("5"));
    }
}
