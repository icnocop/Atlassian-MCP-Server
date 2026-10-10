// <copyright file="ConfluenceClient.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using Atlassian.Mcp.Server.Common.Http;
using ModelContextProtocol;

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

    /// <summary>
    /// Resolves what Markdown texts refer to: the names of mentions written without an account ID,
    /// by searching for Confluence users. Embedding attachments with <c>![name](attachment:ID)</c>
    /// is supported for Jira only, so it is refused here.
    /// </summary>
    /// <param name="markdownTexts">The Markdown texts; <see langword="null"/> entries are skipped.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The references.</returns>
    /// <exception cref="McpException">A name matches no user, or more than one; or a text embeds an attachment.</exception>
    public async Task<AdfReferences> ResolveReferencesAsync(IEnumerable<string?> markdownTexts, CancellationToken cancellationToken)
    {
        List<string?> texts = markdownTexts.ToList();
        string? attachmentId = texts.Where(text => !string.IsNullOrEmpty(text)).SelectMany(MarkdownToAdf.FindAttachmentIds).FirstOrDefault();
        if (attachmentId is not null)
        {
            throw new McpException(
                $"Embedding an attachment with ![name](attachment:{attachmentId}) is supported in Jira only. In Confluence, link to the attachment instead.");
        }

        return AdfReferences.ForMentions(await MentionResolver.ResolveAsync(texts, this.FindMentionCandidatesAsync, cancellationToken));
    }

    /// <summary>
    /// Searches for the people a mention name might refer to, by full name. Apps cannot be
    /// mentioned, so they are left out.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The candidates.</returns>
    private async Task<IReadOnlyList<UserCandidate>> FindMentionCandidatesAsync(string name, CancellationToken cancellationToken)
    {
        string literal = name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        string path = new QueryString("search/user").Add("cql", $"user.fullname~\"{literal}\"").Add("limit", 50).ToString();
        JsonNode? response = await this.GetV1Async(path, cancellationToken);

        return (response?["results"] as JsonArray ?? [])
            .Select(result => result?["user"] as JsonObject)
            .OfType<JsonObject>()
            .Where(user => (string?)user["accountType"] is null or "atlassian")
            .Select(user => new UserCandidate((string?)user["accountId"] ?? string.Empty, (string?)user["displayName"] ?? (string?)user["publicName"] ?? string.Empty))
            .Where(user => user.AccountId.Length > 0)
            .ToList();
    }
}
