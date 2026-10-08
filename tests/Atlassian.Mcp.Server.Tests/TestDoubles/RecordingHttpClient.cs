// <copyright file="RecordingHttpClient.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Common.Json;

namespace Atlassian.Mcp.Server.Tests.TestDoubles;

/// <summary>
/// An <see cref="AtlassianHttpClient"/> that records every request and answers with queued
/// responses, so tests can check the exact path and body that a tool sends.
/// </summary>
internal sealed class RecordingHttpClient : AtlassianHttpClient
{
    private readonly Queue<Func<RecordedRequest, JsonNode?>> responses = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingHttpClient"/> class.
    /// </summary>
    public RecordingHttpClient()
        : base(new HttpClient { BaseAddress = new Uri("https://example.atlassian.net/") })
    {
    }

    /// <summary>Gets the requests sent so far, in order.</summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Gets the last request sent.</summary>
    public RecordedRequest LastRequest => this.Requests[^1];

    /// <summary>
    /// Queues the response for the next request.
    /// </summary>
    /// <param name="json">The response body as JSON text, or <see langword="null"/> for an empty body.</param>
    /// <returns>This instance.</returns>
    public RecordingHttpClient Respond(string? json)
    {
        this.responses.Enqueue(_ => json is null ? null : JsonNode.Parse(json));
        return this;
    }

    /// <summary>
    /// Queues a failure for the next request.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>This instance.</returns>
    public RecordingHttpClient Fail(Exception exception)
    {
        this.responses.Enqueue(_ => throw exception);
        return this;
    }

    /// <inheritdoc/>
    public override Task<JsonNode?> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        JsonNode? bodyNode = body switch
        {
            null => null,
            JsonNode node => node.DeepClone(),
            _ => JsonSerializer.SerializeToNode(body, JsonDefaults.Options),
        };

        return Task.FromResult(this.Answer(new RecordedRequest(method, path, bodyNode, FileName: null, Content: null, FormFields: null)));
    }

    /// <inheritdoc/>
    public override Task<JsonNode?> UploadAsync(
        string path,
        string fileName,
        byte[] content,
        IReadOnlyDictionary<string, string>? formFields,
        CancellationToken cancellationToken)
        => Task.FromResult(this.Answer(new RecordedRequest(HttpMethod.Post, path, Body: null, fileName, content, formFields)));

    /// <inheritdoc/>
    public override Task<(byte[] Content, string? MediaType)> DownloadAsync(string path, long maxBytes, CancellationToken cancellationToken)
    {
        JsonNode? response = this.Answer(new RecordedRequest(HttpMethod.Get, path, Body: null, FileName: null, Content: null, FormFields: null));
        string mediaType = response?["mediaType"]?.GetValue<string>() ?? MediaTypesForTests.Binary;
        byte[] bytes = Convert.FromBase64String(response?["base64"]?.GetValue<string>() ?? string.Empty);
        return Task.FromResult<(byte[], string?)>((bytes, mediaType));
    }

    private JsonNode? Answer(RecordedRequest request)
    {
        this.Requests.Add(request);

        if (this.responses.Count == 0)
        {
            throw new InvalidOperationException($"No response queued for {request.Method} {request.Path}.");
        }

        return this.responses.Dequeue()(request);
    }

    /// <summary>The media types used by <see cref="DownloadAsync"/>.</summary>
    private static class MediaTypesForTests
    {
        /// <summary>The media type for binary content.</summary>
        public const string Binary = "application/octet-stream";
    }
}
