// <copyright file="VersionToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Projects;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Projects;

/// <summary>
/// Tests for <see cref="VersionTools"/> and <see cref="ComponentTools"/>.
/// </summary>
[TestClass]
public sealed class VersionToolsTests
{
    private RecordingHttpClient http = null!;
    private VersionTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client and a fixed clock.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new VersionTools(new JiraClient(this.http))
        {
            Clock = new FixedTimeProvider(new DateTimeOffset(2026, 3, 4, 23, 30, 0, TimeSpan.Zero)),
        };
    }

    /// <summary>
    /// Verifies that releasing a version without a date uses today's UTC date.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Release_WithoutDate_UsesTodayInUtc()
    {
        // Arrange
        this.http.Respond("""{"id":"10200"}""");

        // Act
        await this.tools.Release("10200", cancellationToken: CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Put, request.Method);
        Assert.AreEqual("rest/api/3/version/10200", request.Path);
        Assert.IsTrue(request.Body!["released"]!.GetValue<bool>());
        Assert.AreEqual("2026-03-04", request.Body["releaseDate"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that creating a version with a project key resolves the project ID first.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithProjectKey_ResolvesTheProjectId()
    {
        // Arrange
        this.http.Respond("""{"id":"10000","key":"PROJ"}""").Respond("""{"id":"10200"}""");

        // Act
        await this.tools.Create("PROJ", "2.1.0", releaseDate: "2026-04-01", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/project/PROJ", this.http.Requests[0].Path);
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("rest/api/3/version", request.Path);
        Assert.AreEqual(10000L, request.Body!["projectId"]!.GetValue<long>());
        Assert.AreEqual("2026-04-01", request.Body["releaseDate"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that deleting a version swaps its references to the replacement versions.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithReplacements_PostsRemoveAndSwap()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Delete("10200", "10201", null, CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("rest/api/3/version/10200/removeAndSwap", request.Path);
        Assert.AreEqual(10201L, request.Body!["moveFixIssuesTo"]!.GetValue<long>());
        Assert.IsNull(request.Body["moveAffectedIssuesTo"]);
    }

    /// <summary>
    /// Verifies that deleting a component passes the replacement component.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task ComponentDelete_WithReplacement_PassesMoveIssuesTo()
    {
        // Arrange
        this.http.Respond(null);
        var components = new ComponentTools(new JiraClient(this.http));

        // Act
        await components.Delete("10300", "10301", CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/component/10300?moveIssuesTo=10301", this.http.LastRequest.Path);
    }
}
