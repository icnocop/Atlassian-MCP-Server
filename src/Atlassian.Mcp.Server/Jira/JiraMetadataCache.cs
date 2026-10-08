// <copyright file="JiraMetadataCache.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;

namespace Atlassian.Mcp.Server.Jira;

/// <summary>
/// Site-wide Jira definitions that do not change during a session: which fields hold rich text,
/// and the issue link types. Each is fetched at most once per process, because a tool call that
/// needs them would otherwise pay a round trip every time.
/// </summary>
public sealed class JiraMetadataCache : IDisposable
{
    /// <summary>The custom field type of a multi-line text field, which Jira Cloud stores as ADF.</summary>
    private const string TextAreaCustomType = "com.atlassian.jira.plugin.system.customfieldtypes:textarea";

    /// <summary>
    /// The system fields that hold rich text. The summary is deliberately left out: its schema type
    /// is also "string", but Jira stores it as plain single-line text, so sending ADF for it fails.
    /// </summary>
    private static readonly HashSet<string> RichTextSystemFields = new(StringComparer.OrdinalIgnoreCase) { "description", "environment" };

    private readonly SemaphoreSlim gate = new(1, 1);
    private HashSet<string>? richTextFieldIds;
    private List<JsonObject>? linkTypes;

    /// <summary>
    /// Gets the IDs of the fields whose values must be ADF documents rather than strings.
    /// </summary>
    /// <param name="client">The Jira client used on the first call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The field IDs, compared case-insensitively.</returns>
    public async Task<IReadOnlySet<string>> GetRichTextFieldIdsAsync(JiraClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        await this.gate.WaitAsync(cancellationToken);
        try
        {
            if (this.richTextFieldIds is null)
            {
                JsonNode? fields = await client.GetAsync("field", cancellationToken);
                this.richTextFieldIds = SelectRichTextFieldIds(fields);
            }

            return this.richTextFieldIds;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>
    /// Finds a link type by its name or by either of its directional descriptions, so that
    /// "is blocked by" finds the "Blocks" type. Matching ignores case.
    /// </summary>
    /// <param name="client">The Jira client used on the first call.</param>
    /// <param name="name">The name or description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The link type, or <see langword="null"/> when the site has none that matches.</returns>
    public async Task<JsonObject?> FindLinkTypeAsync(JiraClient client, string name, CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonObject> types = await this.GetLinkTypesAsync(client, cancellationToken);
        string wanted = name.Trim();

        return types.FirstOrDefault(type =>
            Matches(type["name"], wanted) || Matches(type["inward"], wanted) || Matches(type["outward"], wanted));
    }

    /// <summary>
    /// Gets every issue link type that the site defines.
    /// </summary>
    /// <param name="client">The Jira client used on the first call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The link types.</returns>
    public async Task<IReadOnlyList<JsonObject>> GetLinkTypesAsync(JiraClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        await this.gate.WaitAsync(cancellationToken);
        try
        {
            if (this.linkTypes is null)
            {
                JsonNode? response = await client.GetAsync("issueLinkType", cancellationToken);
                this.linkTypes = (response?["issueLinkTypes"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            }

            return this.linkTypes;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>
    /// Discards everything cached, so the next call fetches it again; for example after an
    /// administrator adds a field or link type while the server is running.
    /// </summary>
    public void Invalidate()
    {
        this.gate.Wait();
        try
        {
            this.richTextFieldIds = null;
            this.linkTypes = null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => this.gate.Dispose();

    /// <summary>
    /// Selects the IDs of the rich-text fields from the response of the fields endpoint.
    /// </summary>
    /// <param name="fields">The array of field definitions.</param>
    /// <returns>The field IDs.</returns>
    internal static HashSet<string> SelectRichTextFieldIds(JsonNode? fields)
        => (fields as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(field =>
                string.Equals(field["schema"]?["custom"]?.GetValue<string>(), TextAreaCustomType, StringComparison.Ordinal)
                || (field["schema"]?["system"]?.GetValue<string>() is string system && RichTextSystemFields.Contains(system)))
            .Select(field => field["id"]?.GetValue<string>() ?? field["key"]?.GetValue<string>())
            .OfType<string>()
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool Matches(JsonNode? candidate, string name)
        => candidate is JsonValue value
           && value.TryGetValue(out string? text)
           && string.Equals(text, name, StringComparison.OrdinalIgnoreCase);
}
