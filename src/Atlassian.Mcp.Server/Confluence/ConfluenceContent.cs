// <copyright file="ConfluenceContent.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Atlassian.Mcp.Server.Common.Adf;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Confluence;

/// <summary>
/// Converts between the body formats that the tools accept and the representations that the
/// Confluence REST API uses, and reads the paging cursors it returns.
/// </summary>
public static class ConfluenceContent
{
    /// <summary>The Confluence representation of an ADF body.</summary>
    public const string AdfRepresentation = "atlas_doc_format";

    /// <summary>The Confluence representation of a storage-format (XHTML) body.</summary>
    public const string StorageRepresentation = "storage";

    /// <summary>
    /// Normalizes the value of a body format parameter.
    /// </summary>
    /// <param name="bodyFormat">markdown, adf, or storage.</param>
    /// <returns>The normalized format: markdown, adf, or storage.</returns>
    /// <exception cref="McpException">The format is not known.</exception>
    public static string NormalizeFormat(string? bodyFormat)
    {
        string format = (bodyFormat ?? "markdown").Trim().ToUpperInvariant();
        return format switch
        {
            "" or "MARKDOWN" or "MD" => "markdown",
            "ADF" or "ATLAS_DOC_FORMAT" => "adf",
            "STORAGE" or "XHTML" => "storage",
            _ => throw new McpException("The bodyFormat parameter must be markdown, adf, or storage."),
        };
    }

    /// <summary>
    /// Builds the request body object for page or comment content.
    /// </summary>
    /// <param name="body">The content, in <paramref name="bodyFormat"/>.</param>
    /// <param name="bodyFormat">markdown, adf, or storage.</param>
    /// <returns>The body object, with its representation and value.</returns>
    /// <exception cref="McpException">The ADF is not a valid JSON document.</exception>
    public static JsonObject ToRequestBody(string body, string bodyFormat)
    {
        switch (NormalizeFormat(bodyFormat))
        {
            case "storage":
                return new JsonObject { ["representation"] = StorageRepresentation, ["value"] = body };

            case "adf":
                if (!AdfToMarkdown.IsDocument(Parse(body)))
                {
                    throw new McpException("With bodyFormat adf, the body must be an ADF document: a JSON object with \"type\": \"doc\".");
                }

                return new JsonObject { ["representation"] = AdfRepresentation, ["value"] = body };

            default:
                return new JsonObject { ["representation"] = AdfRepresentation, ["value"] = MarkdownToAdf.Convert(body).ToJsonString() };
        }
    }

    /// <summary>
    /// Builds the request body object for an ADF document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The body object.</returns>
    public static JsonObject ToRequestBody(JsonObject document)
        => new() { ["representation"] = AdfRepresentation, ["value"] = (document ?? throw new ArgumentNullException(nameof(document))).ToJsonString() };

    /// <summary>
    /// Reads the ADF document of a page or comment returned with <c>body-format=atlas_doc_format</c>.
    /// </summary>
    /// <param name="content">The page or comment.</param>
    /// <returns>The document, or an empty document when the body is missing.</returns>
    public static JsonObject ReadAdf(JsonNode? content)
    {
        string? value = content?["body"]?[AdfRepresentation]?["value"]?.GetValue<string>();
        return Parse(value) as JsonObject ?? new JsonObject { ["type"] = "doc", ["version"] = 1, ["content"] = new JsonArray() };
    }

    /// <summary>
    /// Reads the cursor of the next page from the <c>_links.next</c> link of a list response.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The cursor, or <see langword="null"/> on the last page.</returns>
    public static string? NextCursor(JsonNode? response)
    {
        string? next = response?["_links"]?["next"]?.GetValue<string>();
        if (string.IsNullOrEmpty(next))
        {
            return null;
        }

        int query = next.IndexOf('?', StringComparison.Ordinal);
        return query < 0 ? null : HttpUtility.ParseQueryString(next[(query + 1)..])["cursor"];
    }

    private static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
