// <copyright file="AtlassianHttpClient.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Json;
using Atlassian.Mcp.Server.Configuration;

namespace Atlassian.Mcp.Server.Common.Http;

/// <summary>
/// Sends authenticated requests to the Atlassian Cloud REST APIs of one site, and turns error
/// responses into <see cref="AtlassianApiException"/>.
/// </summary>
public class AtlassianHttpClient
{
    /// <summary>The number of times a throttled request is retried.</summary>
    private const int MaxRetries = 3;

    /// <summary>The longest wait honored from a Retry-After header.</summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AtlassianHttpClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client, configured by <see cref="Configure"/>.</param>
    public AtlassianHttpClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>Gets the site URL, which always ends with a slash.</summary>
    public virtual Uri SiteUrl => this.httpClient.BaseAddress ?? throw new InvalidOperationException("The HTTP client has no base address.");

    /// <summary>
    /// Configures an HTTP client for the site and account in <paramref name="options"/>.
    /// </summary>
    /// <param name="httpClient">The client to configure.</param>
    /// <param name="options">The options.</param>
    public static void Configure(HttpClient httpClient, AtlassianOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Email}:{options.ApiToken}"));

        httpClient.BaseAddress = options.SiteUrl;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("atlassian-mcp-server", ServerInfo.Version));
    }

    /// <summary>
    /// Sends a GET request.
    /// </summary>
    /// <param name="path">The path relative to the site root, including any query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body, or <see langword="null"/> when it is empty.</returns>
    public Task<JsonNode?> GetAsync(string path, CancellationToken cancellationToken)
        => this.SendAsync(HttpMethod.Get, path, body: null, cancellationToken);

    /// <summary>
    /// Sends a request with an optional JSON body.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path relative to the site root, including any query string.</param>
    /// <param name="body">The object to serialize as the JSON body, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body, or <see langword="null"/> when it is empty.</returns>
    public virtual async Task<JsonNode?> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await this.SendWithRetryAsync(
            () =>
            {
                var request = new HttpRequestMessage(method, path);
                if (body is not null)
                {
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(body, JsonDefaults.Options),
                        Encoding.UTF8,
                        "application/json");
                }

                return request;
            },
            cancellationToken);

        return await ReadJsonAsync(response, cancellationToken);
    }

    /// <summary>
    /// Uploads a file as multipart form data, as the attachment endpoints require.
    /// </summary>
    /// <param name="path">The path relative to the site root.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="content">The file content.</param>
    /// <param name="formFields">Extra form fields to send with the file, such as a comment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response body.</returns>
    public virtual async Task<JsonNode?> UploadAsync(
        string path,
        string fileName,
        byte[] content,
        IReadOnlyDictionary<string, string>? formFields,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await this.SendWithRetryAsync(
            () =>
            {
                var form = new MultipartFormDataContent();
                var file = new ByteArrayContent(content);
                file.Headers.ContentType = new MediaTypeHeaderValue(MediaTypes.FromFileName(fileName));
                form.Add(file, "file", fileName);

                foreach ((string name, string value) in formFields ?? new Dictionary<string, string>())
                {
                    form.Add(new StringContent(value, Encoding.UTF8), name);
                }

                var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };

                // Atlassian rejects multipart uploads that lack this header, as a defense against
                // cross-site request forgery.
                request.Headers.Add("X-Atlassian-Token", "no-check");
                return request;
            },
            cancellationToken);

        return await ReadJsonAsync(response, cancellationToken);
    }

    /// <summary>
    /// Downloads binary content, following redirects to the media service.
    /// </summary>
    /// <param name="path">The path relative to the site root.</param>
    /// <param name="maxBytes">The largest download allowed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The content and its media type.</returns>
    public virtual async Task<(byte[] Content, string? MediaType)> DownloadAsync(string path, long maxBytes, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await this.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken);

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw new AtlassianApiException($"The content is {response.Content.Headers.ContentLength} bytes, which is more than the {maxBytes}-byte limit.");
        }

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.LongLength > maxBytes)
        {
            throw new AtlassianApiException($"The content is {bytes.LongLength} bytes, which is more than the {maxBytes}-byte limit.");
        }

        return (bytes, response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Builds the message for a failed response from the error formats that Jira and Confluence use.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="reasonPhrase">The reason phrase.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The message.</returns>
    internal static string FormatError(HttpStatusCode statusCode, string? reasonPhrase, string body)
    {
        var details = new List<string>();

        try
        {
            if (JsonNode.Parse(body) is JsonObject error)
            {
                // Jira: { "errorMessages": [...], "errors": { "field": "message" } }
                if (error["errorMessages"] is JsonArray messages)
                {
                    details.AddRange(messages.Select(message => message?.ToString()).OfType<string>());
                }

                if (error["errors"] is JsonObject fieldErrors)
                {
                    details.AddRange(fieldErrors.Select(pair => $"{pair.Key}: {pair.Value}"));
                }

                // Confluence v2: { "errors": [ { "title": "...", "detail": "..." } ] }
                if (error["errors"] is JsonArray errors)
                {
                    details.AddRange(errors
                        .OfType<JsonObject>()
                        .Select(item => string.Join(": ", new[] { item["title"]?.ToString(), item["detail"]?.ToString() }.Where(part => !string.IsNullOrEmpty(part)))));
                }

                // Confluence v1 and some Jira endpoints: { "message": "..." }
                if (error["message"] is JsonValue message)
                {
                    details.Add(message.ToString());
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON (for example an HTML error page); fall back to the raw text below.
        }

        if (details.Count == 0 && !string.IsNullOrWhiteSpace(body))
        {
            details.Add(body.Length > 500 ? body[..500] + "..." : body);
        }

        string summary = $"Atlassian returned {(int)statusCode} {reasonPhrase ?? statusCode.ToString()}";
        return details.Count == 0 ? summary + "." : $"{summary}: {string.Join("; ", details.Where(detail => !string.IsNullOrWhiteSpace(detail)))}";
    }

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            // A few endpoints answer with plain text; return it as a JSON string.
            return JsonValue.Create(text);
        }
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        TimeSpan? delay = response.Headers.RetryAfter?.Delta;
        if (delay is null && response.Headers.RetryAfter?.Date is DateTimeOffset date)
        {
            delay = date - DateTimeOffset.UtcNow;
        }

        delay ??= TimeSpan.FromSeconds(Math.Pow(2, attempt));
        return delay.Value < TimeSpan.Zero ? TimeSpan.Zero : (delay.Value > MaxRetryDelay ? MaxRetryDelay : delay.Value);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            using HttpRequestMessage request = createRequest();
            HttpResponseMessage response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            bool throttled = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
            if (throttled && attempt < MaxRetries)
            {
                TimeSpan delay = GetRetryDelay(response, attempt);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            using (response)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new AtlassianApiException(response.StatusCode, FormatError(response.StatusCode, response.ReasonPhrase, body));
            }
        }
    }
}
