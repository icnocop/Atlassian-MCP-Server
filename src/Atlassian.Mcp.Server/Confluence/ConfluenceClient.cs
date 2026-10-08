// <copyright file="ConfluenceClient.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Http;

namespace Atlassian.Mcp.Server.Confluence;

/// <summary>
/// Sends requests to the Confluence Cloud REST API: version 2 for content, and version 1 where
/// version 2 has no equivalent, such as CQL search.
/// </summary>
public class ConfluenceClient
{
    /// <summary>The path of the version 2 REST API, relative to the site root.</summary>
    public const string V2Path = "wiki/api/v2/";

    /// <summary>The path of the version 1 REST API, relative to the site root.</summary>
    public const string V1Path = "wiki/rest/api/";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfluenceClient"/> class.
    /// </summary>
    /// <param name="http">The HTTP client for the site.</param>
    public ConfluenceClient(AtlassianHttpClient http)
    {
        this.Http = http;
    }

    /// <summary>Gets the HTTP client for the site.</summary>
    public AtlassianHttpClient Http { get; }

    /// <summary>
    /// Sends a GET request to the version 2 REST API.
    /// </summary>
    /// <param name="path">The path relative to <see cref="V2Path"/>, including any query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> GetAsync(string path, CancellationToken cancellationToken)
        => this.Http.SendAsync(HttpMethod.Get, V2Path + path, body: null, cancellationToken);

    /// <summary>
    /// Sends a request to the version 2 REST API.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path relative to <see cref="V2Path"/>, including any query string.</param>
    /// <param name="body">The JSON body, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        => this.Http.SendAsync(method, V2Path + path, body, cancellationToken);

    /// <summary>
    /// Sends a GET request to the version 1 REST API.
    /// </summary>
    /// <param name="path">The path relative to <see cref="V1Path"/>, including any query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> GetV1Async(string path, CancellationToken cancellationToken)
        => this.Http.SendAsync(HttpMethod.Get, V1Path + path, body: null, cancellationToken);

    /// <summary>
    /// Sends a request to the version 1 REST API.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path relative to <see cref="V1Path"/>, including any query string.</param>
    /// <param name="body">The JSON body, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> SendV1Async(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        => this.Http.SendAsync(method, V1Path + path, body, cancellationToken);

    /// <summary>
    /// Builds the browser URL of a path that Confluence returns in a <c>_links</c> object.
    /// </summary>
    /// <param name="relativePath">The path, such as <c>/spaces/DOC/pages/123/Title</c>.</param>
    /// <returns>The absolute URL.</returns>
    public Uri WebUrl(string relativePath)
        => new(this.Http.SiteUrl, "wiki/" + relativePath.TrimStart('/'));
}
