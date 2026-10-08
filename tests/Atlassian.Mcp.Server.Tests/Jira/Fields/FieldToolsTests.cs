// <copyright file="FieldToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Fields;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Fields;

/// <summary>
/// Tests for <see cref="FieldTools"/> and <see cref="MetadataTools"/>.
/// </summary>
[TestClass]
public sealed class FieldToolsTests
{
    private RecordingHttpClient http = null!;

    /// <summary>
    /// Creates the recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
    }

    /// <summary>
    /// Verifies that getting custom fields returns only the custom ones.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetCustom_WithMixedFields_ReturnsOnlyCustomFields()
    {
        // Arrange
        this.http.Respond("""[{"id":"summary","custom":false},{"id":"customfield_10010","custom":true}]""");
        var tools = new FieldTools(new JiraClient(this.http));

        // Act
        string result = await tools.GetCustom();

        // Assert
        Assert.AreEqual("rest/api/3/field", this.http.LastRequest.Path);
        Assert.AreEqual("""[{"id":"customfield_10010","custom":true}]""", result);
    }

    /// <summary>
    /// Verifies that the metadata tools request the expected endpoints.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task MetadataTools_EachTool_RequestsItsEndpoint()
    {
        // Arrange
        this.http.Respond("[]").Respond("[]").Respond("[]").Respond("[]");
        var tools = new MetadataTools(new JiraClient(this.http));

        // Act
        await tools.GetIssueTypes();
        await tools.GetPriorities();
        await tools.GetStatuses();
        await tools.GetResolutions();

        // Assert
        CollectionAssert.AreEqual(
            new[] { "rest/api/3/issuetype", "rest/api/3/priority", "rest/api/3/status", "rest/api/3/resolution" },
            this.http.Requests.Select(request => request.Path).ToArray());
    }
}
