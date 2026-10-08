// <copyright file="IssueToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Issues;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Issues;

/// <summary>
/// Tests for <see cref="IssueTools"/>.
/// </summary>
[TestClass]
public sealed class IssueToolsTests : IDisposable
{
    private const string FieldDefinitions = """
        [
          {"id":"customfield_10050","name":"Steps","schema":{"type":"string","custom":"com.atlassian.jira.plugin.system.customfieldtypes:textarea"}},
          {"id":"customfield_10060","name":"Code","schema":{"type":"string","custom":"com.atlassian.jira.plugin.system.customfieldtypes:textfield"}},
          {"id":"description","name":"Description","schema":{"type":"string","system":"description"}}
        ]
        """;

    private const string IssueTypes = """{"issueTypes":[{"id":"1","name":"Bug","subtask":false},{"id":"2","name":"Task","subtask":false}]}""";

    private const string IssueWithRichText = """
        {"key":"PROJ-1","fields":{
          "summary":"S",
          "description":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"Hello","marks":[{"type":"strong"}]}]}]},
          "comment":{"comments":[{"id":"1","body":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"Hi"}]}]}}]}
        }}
        """;

    private RecordingHttpClient http = null!;
    private JiraMetadataCache metadata = null!;
    private IssueTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.metadata = new JiraMetadataCache();
        this.tools = new IssueTools(new JiraClient(this.http), this.metadata);
    }

    /// <summary>
    /// Disposes the metadata cache.
    /// </summary>
    public void Dispose() => this.metadata.Dispose();

    /// <summary>
    /// Verifies that creating an issue builds every field from the parameters, merges the additional
    /// fields over them, and converts Markdown only for rich-text custom fields.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithAllParameters_BuildsFieldsAndConvertsRichText()
    {
        // Arrange
        this.http.Respond(FieldDefinitions).Respond("""{"id":"10001","key":"PROJ-1"}""");

        // Act
        string result = await this.tools.Create(
            "PROJ",
            "Bug",
            "Summary",
            description: "**Broken**",
            environment: "Windows 11",
            labels: "a, b",
            components: "UI,API",
            dueDate: "2026-12-31",
            originalEstimate: "2h",
            parentKey: "PROJ-0",
            additionalFields: """{"customfield_10050":"1. Open\n2. Click","customfield_10060":"plain","summary":"Override"}""");

        // Assert
        Assert.AreEqual("rest/api/3/field", this.http.Requests[0].Path);
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("rest/api/3/issue", request.Path);
        JsonNode fields = request.Body!["fields"]!;
        Assert.AreEqual("PROJ", fields["project"]!["key"]!.GetValue<string>());
        Assert.AreEqual("Bug", fields["issuetype"]!["name"]!.GetValue<string>());
        Assert.AreEqual("Override", fields["summary"]!.GetValue<string>());
        Assert.AreEqual("doc", fields["description"]!["type"]!.GetValue<string>());
        Assert.AreEqual("doc", fields["environment"]!["type"]!.GetValue<string>());
        Assert.AreEqual("""["a","b"]""", fields["labels"]!.ToJsonString());
        Assert.AreEqual("API", fields["components"]![1]!["name"]!.GetValue<string>());
        Assert.AreEqual("2026-12-31", fields["duedate"]!.GetValue<string>());
        Assert.AreEqual("2h", fields["timetracking"]!["originalEstimate"]!.GetValue<string>());
        Assert.AreEqual("PROJ-0", fields["parent"]!["key"]!.GetValue<string>());
        Assert.AreEqual("orderedList", fields["customfield_10050"]!["content"]![0]!["type"]!.GetValue<string>());
        Assert.AreEqual("plain", fields["customfield_10060"]!.GetValue<string>());

        JsonNode created = JsonNode.Parse(result)!;
        Assert.AreEqual("PROJ-1", created["key"]!.GetValue<string>());
        Assert.AreEqual("https://example.atlassian.net/browse/PROJ-1", created["url"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that numeric project and issue type values are sent as IDs.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithNumericProjectAndType_SendsIds()
    {
        // Arrange
        this.http.Respond("""{"id":"1","key":"P-1"}""");

        // Act
        await this.tools.Create("10000", "10002", "Summary");

        // Assert
        JsonNode fields = this.http.LastRequest.Body!["fields"]!;
        Assert.AreEqual("10000", fields["project"]!["id"]!.GetValue<string>());
        Assert.AreEqual("10002", fields["issuetype"]!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that the field definitions are not requested when no additional field is a string.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithAdditionalFieldsWithoutStrings_DoesNotRequestFieldDefinitions()
    {
        // Arrange
        this.http.Respond("""{"id":"1","key":"P-1"}""");

        // Act
        await this.tools.Create("PROJ", "Task", "Summary", additionalFields: """{"fixVersions":[{"name":"1.0"}]}""");

        // Assert
        Assert.AreEqual(1, this.http.Requests.Count);
        Assert.AreEqual("1.0", this.http.LastRequest.Body!["fields"]!["fixVersions"]![0]!["name"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an update with nothing to change is rejected without a request.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithNoFields_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Update("PROJ-1"));
        Assert.AreEqual(0, this.http.Requests.Count);
    }

    /// <summary>
    /// Verifies that turning off notifications adds the query parameter.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithNotifyUsersFalse_AddsQueryParameter()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Update("PROJ-1", summary: "New", notifyUsers: false);

        // Assert
        Assert.AreEqual(HttpMethod.Put, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1?notifyUsers=false", this.http.LastRequest.Path);
        Assert.AreEqual("New", this.http.LastRequest.Body!["fields"]!["summary"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that rich text in an issue is returned as Markdown by default.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Get_ByDefault_ConvertsRichTextToMarkdown()
    {
        // Arrange
        this.http.Respond(IssueWithRichText);

        // Act
        string result = await this.tools.Get("PROJ-1", fields: "summary,description");

        // Assert
        Assert.AreEqual("rest/api/3/issue/PROJ-1?fields=summary%2Cdescription", this.http.LastRequest.Path);
        JsonNode fields = JsonNode.Parse(result)!["fields"]!;
        Assert.AreEqual("**Hello**", fields["description"]!.GetValue<string>());
        Assert.AreEqual("Hi", fields["comment"]!["comments"]![0]!["body"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that rich text is left as ADF when asked.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Get_WithAdfFormat_KeepsAdf()
    {
        // Arrange
        this.http.Respond(IssueWithRichText);

        // Act
        string result = await this.tools.Get("PROJ-1", richTextFormat: "adf");

        // Assert
        Assert.AreEqual("doc", JsonNode.Parse(result)!["fields"]!["description"]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that without an issue type, the project's issue types are listed.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetCreateMeta_WithoutIssueType_ListsIssueTypes()
    {
        // Arrange
        this.http.Respond(IssueTypes);

        // Act
        string result = await this.tools.GetCreateMeta("PROJ");

        // Assert
        Assert.AreEqual("rest/api/3/issue/createmeta/PROJ/issuetypes?maxResults=200", this.http.LastRequest.Path);
        Assert.AreEqual(2, JsonNode.Parse(result)!["issueTypes"]!.AsArray().Count);
    }

    /// <summary>
    /// Verifies that the issue type is found by name, ignoring case, and that allowed values are capped.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetCreateMeta_WithIssueTypeName_ResolvesItAndCapsAllowedValues()
    {
        // Arrange
        string allowed = string.Join(",", Enumerable.Range(1, 30).Select(i => $$"""{"id":"{{i}}","value":"Option {{i}}"}"""));
        this.http.Respond(IssueTypes).Respond($$"""
            {"fields":[{"fieldId":"customfield_10010","name":"Choice","required":true,"schema":{"type":"option"},"allowedValues":[{{allowed}}]}]}
            """);

        // Act
        string result = await this.tools.GetCreateMeta("PROJ", "bug");

        // Assert
        Assert.AreEqual("rest/api/3/issue/createmeta/PROJ/issuetypes/1?maxResults=200", this.http.LastRequest.Path);
        JsonNode field = JsonNode.Parse(result)!["fields"]![0]!;
        Assert.AreEqual(25, field["allowedValues"]!.AsArray().Count);
        Assert.AreEqual(5, field["allowedValuesTruncated"]!.GetValue<int>());
        Assert.IsTrue(field["required"]!.GetValue<bool>());
    }

    /// <summary>
    /// Verifies that an unknown issue type is reported with the project's issue types.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetCreateMeta_WithUnknownIssueType_ThrowsListingTypes()
    {
        // Arrange
        this.http.Respond(IssueTypes);

        // Act
        McpException exception = await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.GetCreateMeta("PROJ", "Epic"));

        // Assert
        StringAssert.Contains(exception.Message, "Bug, Task", StringComparison.Ordinal);
    }
}
