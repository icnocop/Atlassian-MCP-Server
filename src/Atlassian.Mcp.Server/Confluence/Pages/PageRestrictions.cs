// <copyright file="PageRestrictions.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Confluence.Pages;

/// <summary>
/// Sets who can view and edit a Confluence page.
/// </summary>
internal static class PageRestrictions
{
    /// <summary>No restrictions: anyone who can see the space can view and edit the page.</summary>
    internal const string Open = "open";

    /// <summary>Anyone who can see the space can view the page; only the current user can edit it.</summary>
    internal const string EditRestricted = "edit-restricted";

    /// <summary>Only the current user can view and edit the page.</summary>
    internal const string Private = "private";

    /// <summary>The description of the restrictions parameter, shared by the page tools.</summary>
    internal const string ParameterDescription =
        "Who can view and edit the page: open (anyone who can see the space can view and edit it), "
        + "edit-restricted (anyone who can see the space can view it; only you can edit it), "
        + "or private (only you can view and edit it).";

    /// <summary>
    /// Normalizes a restrictions value.
    /// </summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns>The normalized value, or <see langword="null"/> when none is given.</returns>
    /// <exception cref="McpException">The value is not open, edit-restricted, or private.</exception>
    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            Open => Open,
            EditRestricted => EditRestricted,
            Private => Private,
            _ => throw new McpException($"The restrictions parameter must be {Open}, {EditRestricted}, or {Private}, not '{value.Trim()}'."),
        };
    }

    /// <summary>
    /// Replaces the restrictions of the specified page.
    /// </summary>
    /// <param name="confluence">The Confluence client.</param>
    /// <param name="pageId">The page ID.</param>
    /// <param name="restrictions">The normalized restrictions value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal static async Task ApplyAsync(ConfluenceClient confluence, string pageId, string restrictions, CancellationToken cancellationToken)
    {
        string path = $"content/{Uri.EscapeDataString(pageId.Trim())}/restriction";

        if (restrictions == Open)
        {
            await confluence.SendV1Async(HttpMethod.Delete, path, body: null, cancellationToken);
            return;
        }

        JsonNode? me = await confluence.GetV1Async("user/current", cancellationToken);
        string accountId = me?["accountId"]?.GetValue<string>()
            ?? throw new McpException("Could not find the account ID of the current user to restrict the page to.");

        // A PUT changes only the operations it lists and keeps the others, and an operation with no
        // users and no groups has its restriction removed, so both operations are always sent.
        var request = new JsonArray
        {
            Restriction("read", restrictions == Private ? accountId : null),
            Restriction("update", accountId),
        };

        await confluence.SendV1Async(HttpMethod.Put, path, request, cancellationToken);
    }

    private static JsonObject Restriction(string operation, string? accountId) => new()
    {
        ["operation"] = operation,
        ["restrictions"] = new JsonObject
        {
            ["user"] = accountId is null
                ? new JsonArray()
                : new JsonArray(new JsonObject { ["type"] = "known", ["accountId"] = accountId }),
            ["group"] = new JsonArray(),
        },
    };
}
