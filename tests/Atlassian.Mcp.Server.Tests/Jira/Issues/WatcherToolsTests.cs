// <copyright file="WatcherToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Issues;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Issues;

/// <summary>
/// Tests for <see cref="WatcherTools"/>.
/// </summary>
[TestClass]
public sealed class WatcherToolsTests
{
    private RecordingHttpClient http = null!;
    private WatcherTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new WatcherTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that watching without an account ID adds the current user, sent as a bare JSON string.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Add_WithoutAccountId_AddsCurrentUser()
    {
        // Arrange
        this.http.Respond("""{"accountId":"abc"}""").Respond(null);

        // Act
        await this.tools.Add("PROJ-1");

        // Assert
        Assert.AreEqual("rest/api/3/myself", this.http.Requests[0].Path);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/watchers", this.http.LastRequest.Path);
        Assert.AreEqual("\"abc\"", this.http.LastRequest.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that removing a watcher passes the account ID as a query parameter.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Remove_WithAccountId_SendsDeleteWithQuery()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Remove("PROJ-1", "xyz");

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/issue/PROJ-1/watchers?accountId=xyz", this.http.LastRequest.Path);
    }
}
