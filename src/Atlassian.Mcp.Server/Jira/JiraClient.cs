// <copyright file="JiraClient.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;

namespace Atlassian.Mcp.Server.Jira;

/// <summary>
/// Sends requests to the Jira Cloud platform REST API (version 3), the Jira Software (Agile) REST
/// API, and the board report endpoints.
/// </summary>
public class JiraClient
{
    /// <summary>The path of the platform REST API, relative to the site root.</summary>
    public const string PlatformPath = "rest/api/3/";

    /// <summary>The path of the Jira Software REST API, relative to the site root.</summary>
    public const string AgilePath = "rest/agile/1.0/";

    /// <summary>
    /// The path of the board report endpoints, relative to the site root. The sprint report, burndown,
    /// and velocity data that boards display are only available here, not in the public Agile API.
    /// </summary>
    public const string ReportsPath = "rest/greenhopper/1.0/";

    /// <summary>
    /// Initializes a new instance of the <see cref="JiraClient"/> class.
    /// </summary>
    /// <param name="http">The HTTP client for the site.</param>
    public JiraClient(AtlassianHttpClient http)
    {
        this.Http = http;
    }

    /// <summary>Gets the HTTP client for the site.</summary>
    public AtlassianHttpClient Http { get; }

    /// <summary>
    /// Escapes a value for use as one segment of a request path.
    /// </summary>
    /// <param name="value">The value, such as an issue key.</param>
    /// <returns>The escaped value.</returns>
    public static string Segment(string value) => Uri.EscapeDataString(value.Trim());

    /// <summary>
    /// Converts optional Markdown text to an ADF document.
    /// </summary>
    /// <param name="markdown">The Markdown text, or <see langword="null"/>.</param>
    /// <returns>The document, or <see langword="null"/> when <paramref name="markdown"/> is <see langword="null"/>.</returns>
    public static JsonObject? ToAdf(string? markdown) => markdown is null ? null : MarkdownToAdf.Convert(markdown);

    /// <summary>
    /// Sends a GET request to the platform REST API.
    /// </summary>
    /// <param name="path">The path relative to <see cref="PlatformPath"/>, including any query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> GetAsync(string path, CancellationToken cancellationToken)
        => this.Http.SendAsync(HttpMethod.Get, PlatformPath + path, body: null, cancellationToken);

    /// <summary>
    /// Sends a request to the platform REST API.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path relative to <see cref="PlatformPath"/>, including any query string.</param>
    /// <param name="body">The JSON body, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        => this.Http.SendAsync(method, PlatformPath + path, body, cancellationToken);

    /// <summary>
    /// Sends a GET request to the Jira Software REST API.
    /// </summary>
    /// <param name="path">The path relative to <see cref="AgilePath"/>, including any query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> GetAgileAsync(string path, CancellationToken cancellationToken)
        => this.Http.SendAsync(HttpMethod.Get, AgilePath + path, body: null, cancellationToken);

    /// <summary>
    /// Sends a request to the Jira Software REST API.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path relative to <see cref="AgilePath"/>, including any query string.</param>
    /// <param name="body">The JSON body, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> SendAgileAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        => this.Http.SendAsync(method, AgilePath + path, body, cancellationToken);

    /// <summary>
    /// Sends a GET request to the board report endpoints.
    /// </summary>
    /// <param name="path">The path relative to <see cref="ReportsPath"/>, including any query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public Task<JsonNode?> GetReportAsync(string path, CancellationToken cancellationToken)
        => this.Http.SendAsync(HttpMethod.Get, ReportsPath + path, body: null, cancellationToken);
}
