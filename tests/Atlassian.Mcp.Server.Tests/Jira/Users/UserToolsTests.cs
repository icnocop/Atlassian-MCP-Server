// <copyright file="UserToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Users;
using Atlassian.Mcp.Server.Tests.TestDoubles;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Jira.Users;

/// <summary>
/// Tests for <see cref="UserTools"/> and <see cref="GroupTools"/>.
/// </summary>
[TestClass]
public sealed class UserToolsTests
{
    private RecordingHttpClient http = null!;
    private UserTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new UserTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that getting several users repeats the accountId parameter.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetMany_WithAccountIds_SendsEachId()
    {
        // Arrange
        this.http.Respond("""{"values":[]}""");

        // Act
        await this.tools.GetMany("a1, a2", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/user/bulk?accountId=a1&accountId=a2", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that an empty list of account IDs is rejected.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetMany_WithEmptyList_ThrowsMcpException()
    {
        // Act and assert
        await Assert.ThrowsExactlyAsync<McpException>(() => this.tools.GetMany(" , ", cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// Verifies that checking my permissions without a list checks the default permissions.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task GetMyPermissions_WithoutPermissions_UsesTheDefaults()
    {
        // Arrange
        this.http.Respond("""{"permissions":{}}""");

        // Act
        await this.tools.GetMyPermissions(projectKey: "PROJ", cancellationToken: CancellationToken.None);

        // Assert
        string expected = "rest/api/3/mypermissions?permissions=" + Uri.EscapeDataString(UserTools.DefaultPermissions) + "&projectKey=PROJ";
        Assert.AreEqual(expected, this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that finding users with permissions passes the permissions and scope.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task FindWithPermissions_WithIssueScope_SendsPermissionsAndIssueKey()
    {
        // Arrange
        this.http.Respond("[]");

        // Act
        await this.tools.FindWithPermissions("EDIT_ISSUES,BROWSE_PROJECTS", issueKey: "PROJ-1", cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/user/permission/search?permissions=EDIT_ISSUES%2CBROWSE_PROJECTS&issueKey=PROJ-1", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that adding a user to a group posts the account ID.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AddMember_WithAccountId_PostsAccountId()
    {
        // Arrange
        this.http.Respond("""{"name":"developers"}""");
        var groups = new GroupTools(new JiraClient(this.http));

        // Act
        await groups.AddMember("jira developers", "abc-123", CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("rest/api/3/group/user?groupname=jira%20developers", request.Path);
        Assert.AreEqual("abc-123", request.Body!["accountId"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that removing a user from a group sends the group and account ID.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RemoveMember_WithAccountId_SendsDeleteRequest()
    {
        // Arrange
        this.http.Respond(null);
        var groups = new GroupTools(new JiraClient(this.http));

        // Act
        await groups.RemoveMember("developers", "abc-123", CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/group/user?groupname=developers&accountId=abc-123", this.http.LastRequest.Path);
    }
}
