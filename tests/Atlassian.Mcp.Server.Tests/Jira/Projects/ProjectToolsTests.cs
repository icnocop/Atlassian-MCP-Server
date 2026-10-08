// <copyright file="ProjectToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Projects;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Projects;

/// <summary>
/// Tests for <see cref="ProjectTools"/>.
/// </summary>
[TestClass]
public sealed class ProjectToolsTests
{
    private RecordingHttpClient http = null!;
    private ProjectTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new ProjectTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that listing projects repeats the keys parameter for each key.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task List_WithKeysAndPaging_SendsEachKey()
    {
        // Arrange
        this.http.Respond("""{"values":[]}""");

        // Act
        await this.tools.List("PROJ, OPS", "lead", 10, 5, CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/project/search?keys=PROJ&keys=OPS&expand=lead&startAt=10&maxResults=5", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that the roles are returned with the IDs taken from their URLs.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetRoles_WithRoleUrls_ReturnsNamesAndIds()
    {
        // Arrange
        this.http.Respond("""{"Developers":"https://example.atlassian.net/rest/api/3/project/10000/role/10001"}""");

        // Act
        string result = await this.tools.GetRoles("PROJ", CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/project/PROJ/role", this.http.LastRequest.Path);
        Assert.AreEqual("""[{"name":"Developers","id":"10001"}]""", result);
    }

    /// <summary>
    /// Verifies that creating a project without a lead uses the current user as the lead.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithoutLead_UsesTheCurrentUser()
    {
        // Arrange
        this.http.Respond("""{"accountId":"abc-123"}""").Respond("""{"id":10000,"key":"NEW"}""");

        // Act
        await this.tools.Create("NEW", "New project", "software", categoryId: "10100", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/myself", this.http.Requests[0].Path);
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("rest/api/3/project", request.Path);
        Assert.AreEqual("abc-123", request.Body!["leadAccountId"]!.GetValue<string>());
        Assert.AreEqual(10100L, request.Body["categoryId"]!.GetValue<long>());
    }

    /// <summary>
    /// Verifies that deleting a project moves it to the trash.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Delete_WithProjectKey_EnablesUndo()
    {
        // Arrange
        this.http.Respond(null);

        // Act
        await this.tools.Delete("PROJ", CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/project/PROJ?enableUndo=true", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that a category ID that is not a number is rejected.
    /// </summary>
    [TestMethod]
    public void ParseOptionalId_WithText_ThrowsMcpException()
    {
        // Act and assert
        Assert.ThrowsExactly<McpException>(() => ProjectTools.ParseOptionalId("abc", "categoryId"));
    }
}
