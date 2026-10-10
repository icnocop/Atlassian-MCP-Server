// <copyright file="RenderedAttachments.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Atlassian.Mcp.Server.Common.Adf;

namespace Atlassian.Mcp.Server.Jira;

/// <summary>
/// Finds the attachment that each ADF media node in a Jira response shows, from the HTML that Jira
/// renders for the same rich text (<c>renderedBody</c> for comments, <c>renderedFields</c> for
/// issues). An ADF media node names the file in the media service, while the REST API names
/// attachments by attachment ID, and the rendered HTML is the one place that links the two.
/// <para>
/// Jira renders an embedded file in one of three ways, and each is handled:
/// </para>
/// <list type="bullet">
/// <item>As a link, <c>&lt;a href=".../attachment/content/ID" data-media-services-id="..."&gt;</c>,
/// which names both IDs, so the match is certain.</item>
/// <item>As an image, <c>&lt;img src=".../attachment/content/ID" alt="file name"&gt;</c>, without the
/// media ID. It is matched to the media node with the same alternative text, or failing that, by
/// position when the unmatched media nodes and images are equal in number.</item>
/// <item>As an error, when Jira cannot render the file. Nothing links it to an attachment, so the
/// media node stays unmatched, and is written as a placeholder.</item>
/// </list>
/// </summary>
public static partial class RenderedAttachments
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="node"/> holds an ADF media node at any depth.
    /// </summary>
    /// <param name="node">The response.</param>
    /// <returns><see langword="true"/> when the rendered HTML is needed to write it as Markdown.</returns>
    public static bool HasMedia(JsonNode? node) => node switch
    {
        JsonObject item => TypeOf(item) == "media" || item.Any(pair => HasMedia(pair.Value)),
        JsonArray items => items.Any(HasMedia),
        _ => false,
    };

    /// <summary>
    /// Finds the attachment that each media node in <paramref name="response"/> shows.
    /// </summary>
    /// <param name="response">A Jira response that includes the rendered HTML of its rich text.</param>
    /// <returns>The attachment of each media ID that could be matched.</returns>
    public static IReadOnlyDictionary<string, AttachmentReference> Find(JsonNode? response)
    {
        var pairs = new List<(JsonObject Document, string Html)>();
        CollectPairs(response, pairs);

        // A link names both IDs, wherever it appears, so those are matched first and for the whole response.
        var attachments = new Dictionary<string, AttachmentReference>(StringComparer.Ordinal);
        foreach (RenderedFile file in pairs.SelectMany(pair => ParseHtml(pair.Html)).Where(file => file.MediaId is not null))
        {
            attachments.TryAdd(file.MediaId!, new AttachmentReference(file.AttachmentId, file.Name));
        }

        foreach ((JsonObject document, string html) in pairs)
        {
            MatchImages(document, html, attachments);
        }

        return attachments;
    }

    /// <summary>
    /// Removes the rendered HTML that was only requested to match media nodes to attachments.
    /// </summary>
    /// <param name="node">The response; it is changed in place.</param>
    /// <param name="property">The property to remove: <c>renderedBody</c> or <c>renderedFields</c>.</param>
    public static void RemoveRendered(JsonNode? node, string property)
    {
        switch (node)
        {
            case JsonObject item:
                item.Remove(property);
                foreach ((_, JsonNode? value) in item)
                {
                    RemoveRendered(value, property);
                }

                break;

            case JsonArray items:
                foreach (JsonNode? child in items)
                {
                    RemoveRendered(child, property);
                }

                break;
        }
    }

    /// <summary>
    /// The opening tag of a link or image to an attachment's content. Thumbnails, emoticons, and the
    /// small icons inside attachment links point elsewhere, so they do not match.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"<(?:a|img)\b[^>]*?\b(?:href|src)=""[^""]*?/attachment/content/(?<id>\d+)""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex FileTagPattern();

    [GeneratedRegex(@"(?<name>[\w-]+)=""(?<value>[^""]*)""")]
    private static partial Regex AttributePattern();

    /// <summary>
    /// Pairs each ADF document in a response with its rendered HTML: a comment's <c>body</c> with
    /// its <c>renderedBody</c>, and an issue's <c>fields</c>, including its comments, with the same
    /// entries of <c>renderedFields</c>.
    /// </summary>
    private static void CollectPairs(JsonNode? node, List<(JsonObject Document, string Html)> pairs)
    {
        switch (node)
        {
            case JsonObject item:
                if (item["body"] is JsonObject body && AdfToMarkdown.IsDocument(body) && TextOf(item["renderedBody"]) is { } renderedBody)
                {
                    pairs.Add((body, renderedBody));
                }

                if (item["fields"] is JsonObject fields && item["renderedFields"] is JsonObject renderedFields)
                {
                    foreach ((string name, JsonNode? value) in fields)
                    {
                        if (value is JsonObject document && AdfToMarkdown.IsDocument(document) && TextOf(renderedFields[name]) is { } html)
                        {
                            pairs.Add((document, html));
                        }
                    }

                    if (fields["comment"]?["comments"] is JsonArray comments
                        && renderedFields["comment"]?["comments"] is JsonArray renderedComments
                        && comments.Count == renderedComments.Count)
                    {
                        for (int i = 0; i < comments.Count; i++)
                        {
                            if (comments[i]?["body"] is JsonObject document && AdfToMarkdown.IsDocument(document) && TextOf(renderedComments[i]?["body"]) is { } html)
                            {
                                pairs.Add((document, html));
                            }
                        }
                    }
                }

                foreach ((string name, JsonNode? value) in item)
                {
                    if (name is not ("fields" or "renderedFields"))
                    {
                        CollectPairs(value, pairs);
                    }
                }

                break;

            case JsonArray items:
                foreach (JsonNode? child in items)
                {
                    CollectPairs(child, pairs);
                }

                break;
        }
    }

    /// <summary>
    /// Matches the media nodes of one document that no link matched to the images in its HTML: by
    /// alternative text first, and then by position, but only when what is left pairs up exactly.
    /// </summary>
    private static void MatchImages(JsonObject document, string html, Dictionary<string, AttachmentReference> attachments)
    {
        List<(string Id, string? Alt)> media = MediaNodes(document).Where(node => !attachments.ContainsKey(node.Id)).ToList();
        List<RenderedFile> images = ParseHtml(html).Where(file => file.MediaId is null).ToList();

        foreach ((string id, string? alt) in media.ToList())
        {
            if (string.IsNullOrEmpty(alt) || media.Count(other => other.Alt == alt) != 1)
            {
                continue;
            }

            List<RenderedFile> named = images.Where(image => image.Name == alt).ToList();
            if (named.Count == 1)
            {
                attachments[id] = new AttachmentReference(named[0].AttachmentId, named[0].Name);
                media.Remove((id, alt));
                images.Remove(named[0]);
            }
        }

        if (media.Count > 0 && media.Count == images.Count)
        {
            for (int i = 0; i < media.Count; i++)
            {
                attachments[media[i].Id] = new AttachmentReference(images[i].AttachmentId, images[i].Name);
            }
        }
    }

    private static IEnumerable<RenderedFile> ParseHtml(string html)
    {
        foreach (Match tag in FileTagPattern().Matches(html))
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attribute in AttributePattern().Matches(tag.Value))
            {
                attributes.TryAdd(attribute.Groups["name"].Value, WebUtility.HtmlDecode(attribute.Groups["value"].Value));
            }

            string? mediaId = attributes.GetValueOrDefault("data-media-services-id");
            string name = attributes.GetValueOrDefault("data-attachment-name") ?? attributes.GetValueOrDefault("alt") ?? string.Empty;
            yield return new RenderedFile(tag.Groups["id"].Value, string.IsNullOrEmpty(mediaId) ? null : mediaId, name);
        }
    }

    private static IEnumerable<(string Id, string? Alt)> MediaNodes(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject item:
                if (TypeOf(item) == "media" && TextOf(item["attrs"]?["id"]) is { Length: > 0 } id)
                {
                    yield return (id, TextOf(item["attrs"]?["alt"]));
                }

                foreach ((string Id, string? Alt) media in MediaNodes(item["content"]))
                {
                    yield return media;
                }

                break;

            case JsonArray items:
                foreach (JsonNode? child in items)
                {
                    foreach ((string Id, string? Alt) media in MediaNodes(child))
                    {
                        yield return media;
                    }
                }

                break;
        }
    }

    private static string TypeOf(JsonObject node) => TextOf(node["type"]) ?? string.Empty;

    private static string? TextOf(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>A link or image to an attachment's content, in rendered HTML.</summary>
    /// <param name="AttachmentId">The attachment ID.</param>
    /// <param name="MediaId">The media ID, which only links name.</param>
    /// <param name="Name">The file name, or the image's alternative text.</param>
    private sealed record RenderedFile(string AttachmentId, string? MediaId, string Name);
}
