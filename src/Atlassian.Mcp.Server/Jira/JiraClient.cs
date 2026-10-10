// <copyright file="JiraClient.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;
using ModelContextProtocol;

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
    /// <param name="references">
    /// The mentions and embedded attachments, from <see cref="ResolveReferencesAsync"/>; or
    /// <see langword="null"/> when the text has neither.
    /// </param>
    /// <returns>The document, or <see langword="null"/> when <paramref name="markdown"/> is <see langword="null"/>.</returns>
    public static JsonObject? ToAdf(string? markdown, AdfReferences? references = null)
        => markdown is null ? null : MarkdownToAdf.Convert(markdown, references);

    /// <summary>
    /// Converts optional Markdown text to an ADF document, first resolving its mentions and
    /// embedded attachments.
    /// </summary>
    /// <param name="markdown">The Markdown text, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null"/> when <paramref name="markdown"/> is <see langword="null"/>.</returns>
    /// <exception cref="McpException">A mention or an embedded attachment cannot be resolved.</exception>
    public async Task<JsonObject?> ToAdfAsync(string? markdown, CancellationToken cancellationToken)
        => ToAdf(markdown, await this.ResolveReferencesAsync([markdown], cancellationToken));

    /// <summary>
    /// Resolves what Markdown texts refer to: the names of mentions written without an account ID,
    /// by searching for Jira users; and the attachments embedded with <c>![name](attachment:ID)</c>,
    /// by looking up each attachment and the file that the media service holds for it.
    /// </summary>
    /// <param name="markdownTexts">The Markdown texts; <see langword="null"/> entries are skipped.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The references.</returns>
    /// <exception cref="McpException">A mention or an embedded attachment cannot be resolved.</exception>
    public async Task<AdfReferences> ResolveReferencesAsync(IEnumerable<string?> markdownTexts, CancellationToken cancellationToken)
    {
        List<string?> texts = markdownTexts.ToList();
        IReadOnlyDictionary<string, string> mentions = await MentionResolver.ResolveAsync(texts, this.FindMentionCandidatesAsync, cancellationToken);

        var attachments = new Dictionary<string, EmbeddedAttachment>(StringComparer.Ordinal);
        IEnumerable<string> attachmentIds = texts
            .Where(text => !string.IsNullOrEmpty(text))
            .SelectMany(MarkdownToAdf.FindAttachmentIds)
            .Distinct(StringComparer.Ordinal);

        foreach (string attachmentId in attachmentIds)
        {
            attachments[attachmentId] = await this.FindEmbeddedAttachmentAsync(attachmentId, cancellationToken);
        }

        return new AdfReferences(mentions, attachments);
    }

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

    /// <summary>
    /// Looks up an attachment to embed: its file name and media type, and the ID of its file in the
    /// media service, which ADF media nodes need.
    /// <para>
    /// The REST API does not return the media ID. It appears only in the URL of the media service
    /// that the attachment's content redirects to, <c>https://api.media.atlassian.com/file/{mediaId}/binary</c>,
    /// so the redirect is followed for its headers only. This is not a documented contract, so a
    /// URL of another shape fails with an error rather than embedding the wrong file.
    /// </para>
    /// </summary>
    /// <param name="attachmentId">The attachment ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="McpException">The attachment was not found, or its media ID could not be determined.</exception>
    private async Task<EmbeddedAttachment> FindEmbeddedAttachmentAsync(string attachmentId, CancellationToken cancellationToken)
    {
        JsonNode? metadata;
        Uri? target;
        try
        {
            metadata = await this.GetAsync($"attachment/{Segment(attachmentId)}", cancellationToken);
            target = await this.Http.GetRedirectTargetAsync($"{PlatformPath}attachment/content/{Segment(attachmentId)}", cancellationToken);
        }
        catch (AtlassianApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new McpException(
                $"Attachment {attachmentId} in ![name](attachment:{attachmentId}) was not found, or you cannot see it. Attach the file to the issue with atlassian_jira_add_attachment first, and use the ID it returns.");
        }

        string[] segments = target?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        int file = Array.IndexOf(segments, "file");
        if (file < 0 || file + 1 >= segments.Length || !Guid.TryParse(segments[file + 1], out _))
        {
            throw new McpException(
                $"Jira did not say where the media service holds attachment {attachmentId}, so it cannot be embedded. Link to it instead, as [name]({this.Http.SiteUrl}{PlatformPath}attachment/content/{attachmentId}).");
        }

        return new EmbeddedAttachment(
            segments[file + 1],
            (string?)metadata?["filename"] ?? string.Empty,
            (string?)metadata?["mimeType"]);
    }

    /// <summary>
    /// Searches for the active people a mention name might refer to. Apps and customer accounts
    /// cannot be mentioned, so they are left out.
    /// </summary>
    /// <param name="name">The display name or email address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The candidates.</returns>
    private async Task<IReadOnlyList<UserCandidate>> FindMentionCandidatesAsync(string name, CancellationToken cancellationToken)
    {
        string path = new QueryString("user/search").Add("query", name).Add("maxResults", 50).ToString();
        JsonNode? users = await this.GetAsync(path, cancellationToken);

        return (users as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(user => (string?)user["accountType"] is null or "atlassian" && (bool?)user["active"] != false)
            .Select(user => new UserCandidate((string?)user["accountId"] ?? string.Empty, (string?)user["displayName"] ?? string.Empty))
            .Where(user => user.AccountId.Length > 0)
            .ToList();
    }
}
