// <copyright file="AtlassianHttpClientTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Http;

namespace Atlassian.Mcp.Server.Tests.Common.Http;

/// <summary>
/// Tests for <see cref="AtlassianHttpClient"/>.
/// </summary>
[TestClass]
public sealed class AtlassianHttpClientTests
{
    /// <summary>
    /// Verifies that Jira error messages and field errors are both reported.
    /// </summary>
    [TestMethod]
    public void FormatError_WithJiraErrors_ListsMessagesAndFieldErrors()
    {
        // Act
        string message = AtlassianHttpClient.FormatError(
            HttpStatusCode.BadRequest,
            "Bad Request",
            """{"errorMessages":["Issue does not exist"],"errors":{"summary":"Field required"}}""");

        // Assert
        StringAssert.StartsWith(message, "Atlassian returned 400 Bad Request: ", StringComparison.Ordinal);
        StringAssert.Contains(message, "Issue does not exist", StringComparison.Ordinal);
        StringAssert.Contains(message, "summary: Field required", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the Confluence version 2 error array is reported.
    /// </summary>
    [TestMethod]
    public void FormatError_WithConfluenceV2Errors_ListsTitleAndDetail()
    {
        // Act
        string message = AtlassianHttpClient.FormatError(
            HttpStatusCode.NotFound,
            "Not Found",
            """{"errors":[{"status":404,"title":"Not Found","detail":"Page 1"}]}""");

        // Assert
        StringAssert.Contains(message, "Not Found: Page 1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a version 1 message is reported.
    /// </summary>
    [TestMethod]
    public void FormatError_WithV1Message_ReportsTheMessage()
    {
        // Act
        string message = AtlassianHttpClient.FormatError(HttpStatusCode.Forbidden, "Forbidden", """{"statusCode":403,"message":"No permission"}""");

        // Assert
        StringAssert.EndsWith(message, ": No permission", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a long body that is not JSON is truncated.
    /// </summary>
    [TestMethod]
    public void FormatError_WithLongNonJsonBody_TruncatesIt()
    {
        // Arrange
        string body = "<html>" + new string('x', 600) + "</html>";

        // Act
        string message = AtlassianHttpClient.FormatError(HttpStatusCode.BadGateway, "Bad Gateway", body);

        // Assert
        StringAssert.EndsWith(message, "...", StringComparison.Ordinal);
        Assert.IsTrue(message.Length < 600);
    }

    /// <summary>
    /// Verifies that a throttled request is retried and the retry's response is returned.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task SendAsync_WithTooManyRequestsThenSuccess_RetriesAndReturnsBody()
    {
        // Arrange
        using var handler = new StubHandler();
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        handler.Responses.Enqueue(throttled);
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json") });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.atlassian.net/") };
        var client = new AtlassianHttpClient(httpClient);

        // Act
        JsonNode? result = await client.SendAsync(HttpMethod.Get, "rest/api/3/myself", body: null, CancellationToken.None);

        // Assert
        Assert.AreEqual(2, handler.Calls);
        Assert.IsTrue(result!["ok"]!.GetValue<bool>());
    }

    /// <summary>
    /// Verifies that a client error throws an exception that carries the status code.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task SendAsync_WithBadRequest_ThrowsAtlassianApiException()
    {
        // Arrange
        using var handler = new StubHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"errorMessages":["Bad JQL"]}""") });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.atlassian.net/") };
        var client = new AtlassianHttpClient(httpClient);

        // Act
        AtlassianApiException exception = await Assert.ThrowsExactlyAsync<AtlassianApiException>(
            () => client.SendAsync(HttpMethod.Post, "rest/api/3/search/jql", new { jql = "x" }, CancellationToken.None));

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
        StringAssert.Contains(exception.Message, "Bad JQL", StringComparison.Ordinal);
        Assert.AreEqual(1, handler.Calls);
    }

    /// <summary>
    /// An HTTP handler that answers with queued responses.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        /// <summary>Gets the responses to return, in order.</summary>
        public Queue<HttpResponseMessage> Responses { get; } = new();

        /// <summary>Gets the number of requests received.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Calls++;
            return Task.FromResult(this.Responses.Dequeue());
        }
    }
}
