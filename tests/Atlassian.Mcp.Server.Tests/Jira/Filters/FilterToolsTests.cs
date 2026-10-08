// <copyright file="FilterToolsTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Jira;
using Atlassian.Mcp.Server.Jira.Filters;
using Atlassian.Mcp.Server.Tests.TestDoubles;

namespace Atlassian.Mcp.Server.Tests.Jira.Filters;

/// <summary>
/// Tests for <see cref="FilterTools"/>.
/// </summary>
[TestClass]
public sealed class FilterToolsTests
{
    private RecordingHttpClient http = null!;
    private FilterTools tools = null!;

    /// <summary>
    /// Creates the tools with a recording HTTP client.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        this.http = new RecordingHttpClient();
        this.tools = new FilterTools(new JiraClient(this.http));
    }

    /// <summary>
    /// Verifies that creating a filter sends the API's spelling of the favorite flag.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Create_WithFavorite_SendsFavouriteFlag()
    {
        // Arrange
        this.http.Respond("""{"id":"10000"}""");

        // Act
        await this.tools.Create("My bugs", "assignee = currentUser()", favorite: true, cancellationToken: CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual("rest/api/3/filter", request.Path);
        Assert.AreEqual("""{"name":"My bugs","jql":"assignee = currentUser()","favourite":true}""", request.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that updating a filter without a name reads the current name first.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Update_WithoutName_KeepsTheCurrentName()
    {
        // Arrange
        this.http.Respond("""{"id":"10000","name":"My bugs"}""").Respond("""{"id":"10000"}""");

        // Act
        await this.tools.Update("10000", jql: "project = PROJ", cancellationToken: CancellationToken.None);

        // Assert
        RecordedRequest request = this.http.LastRequest;
        Assert.AreEqual(HttpMethod.Put, request.Method);
        Assert.AreEqual("rest/api/3/filter/10000", request.Path);
        Assert.AreEqual("""{"name":"My bugs","jql":"project = PROJ"}""", request.Body!.ToJsonString());
    }

    /// <summary>
    /// Verifies that adding a filter to the favorites uses the favourite endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task AddToFavorites_WithFilterId_SendsPut()
    {
        // Arrange
        this.http.Respond("""{"id":"10000"}""");

        // Act
        await this.tools.AddToFavorites("10000", CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Put, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/filter/10000/favourite", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that removing a filter from the favorites uses the favourite endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RemoveFromFavorites_WithFilterId_SendsDelete()
    {
        // Arrange
        this.http.Respond("""{"id":"10000"}""");

        // Act
        await this.tools.RemoveFromFavorites("10000", CancellationToken.None);

        // Assert
        Assert.AreEqual(HttpMethod.Delete, this.http.LastRequest.Method);
        Assert.AreEqual("rest/api/3/filter/10000/favourite", this.http.LastRequest.Path);
    }

    /// <summary>
    /// Verifies that searching filters passes the owner as an account ID.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task Search_WithOwner_PassesAccountId()
    {
        // Arrange
        this.http.Respond("""{"values":[]}""");

        // Act
        await this.tools.Search("bugs", "abc-123", maxResults: 20, cancellationToken: CancellationToken.None);

        // Assert
        Assert.AreEqual("rest/api/3/filter/search?filterName=bugs&accountId=abc-123&maxResults=20", this.http.LastRequest.Path);
    }
}
