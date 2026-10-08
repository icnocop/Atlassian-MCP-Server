// <copyright file="LabelToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Issues;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Issues;

/// <summary>
/// Tests for <see cref="LabelTools"/>.
/// </summary>
[TestClass]
public sealed class LabelToolsTests
{
    private RecordingHttpClient http = null!;
    private LabelTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new LabelTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that adding labels sends an add operation for each label.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithLabels_SendsAddOperations()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Add("PROJ-1", "a, b");

        // Assert
        Assert.AreEqual("rest/api/3/issue/PROJ-1", this.http.LastRequest.Path);
        Assert.AreEqual("""{"update":{"labels":[{"add":"a"},{"add":"b"}]}}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that removing labels sends a remove operation for each label.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Remove_WithLabel_SendsRemoveOperation()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Remove("PROJ-1", "old");

        // Assert
        Assert.AreEqual("""{"update":{"labels":[{"remove":"old"}]}}""", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that an empty label list is rejected.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithNoLabels_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.Add("PROJ-1", " , "));
    }
}
