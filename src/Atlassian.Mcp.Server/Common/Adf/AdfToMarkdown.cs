// <copyright file="AdfToMarkdown.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// Converts Atlassian Document Format (ADF) to Markdown, so that an AI model can read rich text
/// without the overhead of the ADF tree.
/// <para>
/// Content that Markdown cannot represent (macros, media, smart links shown as cards, and other
/// extensions) is written as an HTML comment placeholder such as
/// <c>&lt;!-- adf:extension key=jira --&gt;</c>, so the model knows it is there. Those
/// placeholders are not converted back: writing the Markdown back to Atlassian would lose that
/// content, which is why <see cref="AdfInspector"/> exists.
/// </para>
/// <para>
/// An embedded file is written as <c>![name](attachment:ID)</c>, which <see cref="MarkdownToAdf"/>
/// reads back, when the caller supplies the attachment that each media node shows (Jira's media
/// nodes name the file in the media service, not the attachment). Otherwise it is a placeholder.
/// </para>
/// </summary>
public static class AdfToMarkdown
{
    /// <summary>The attribute that <see cref="Annotate"/> adds to a media node for its attachment ID.</summary>
    private const string AttachmentIdAttribute = "mcp:attachmentId";

    /// <summary>The attribute that <see cref="Annotate"/> adds to a media node for its file name.</summary>
    private const string FileNameAttribute = "mcp:fileName";

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="node"/> is an ADF document.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> for an object whose type is <c>doc</c>.</returns>
    public static bool IsDocument(JsonNode? node)
        => node is JsonObject document && TypeOf(document) == "doc";

    /// <summary>
    /// Converts an ADF document, or any ADF node, to Markdown.
    /// </summary>
    /// <param name="node">The document or node.</param>
    /// <returns>The Markdown text, without trailing line breaks.</returns>
    public static string Convert(JsonNode? node)
    {
        var writer = new StringBuilder();
        WriteBlock(writer, node, prefix: string.Empty);
        return writer.ToString().TrimEnd('\n', ' ');
    }

    /// <summary>
    /// Converts an ADF document, or any ADF node, to Markdown, writing each media node whose media
    /// ID is in <paramref name="attachments"/> as <c>![name](attachment:ID)</c>.
    /// </summary>
    /// <param name="node">The document or node.</param>
    /// <param name="attachments">The attachment that each media ID shows, or <see langword="null"/>.</param>
    /// <returns>The Markdown text, without trailing line breaks.</returns>
    public static string Convert(JsonNode? node, IReadOnlyDictionary<string, AttachmentReference>? attachments)
        => Convert(Annotate(node, attachments));

    /// <summary>
    /// Returns a copy of <paramref name="node"/> with every ADF document inside it replaced by its
    /// Markdown text, as <see cref="ConvertDocuments(JsonNode?)"/> does, writing each media node
    /// whose media ID is in <paramref name="attachments"/> as <c>![name](attachment:ID)</c>.
    /// </summary>
    /// <param name="node">The response.</param>
    /// <param name="attachments">The attachment that each media ID shows, or <see langword="null"/>.</param>
    /// <returns>The converted copy.</returns>
    public static JsonNode? ConvertDocuments(JsonNode? node, IReadOnlyDictionary<string, AttachmentReference>? attachments)
        => ConvertDocuments(Annotate(node, attachments));

    /// <summary>
    /// Returns a copy of <paramref name="node"/> with every ADF document inside it, at any depth,
    /// replaced by its Markdown text. Used for API responses that embed rich text in several places,
    /// such as an issue's description, environment, comments, and multi-line custom fields.
    /// </summary>
    /// <param name="node">The response.</param>
    /// <returns>The converted copy.</returns>
    public static JsonNode? ConvertDocuments(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject document when IsDocument(document):
                return JsonValue.Create(Convert(document));

            case JsonObject source:
                var target = new JsonObject();
                foreach ((string name, JsonNode? value) in source)
                {
                    target[name] = ConvertDocuments(value);
                }

                return target;

            case JsonArray array:
                return new JsonArray(array.Select(ConvertDocuments).ToArray());

            default:
                return node?.DeepClone();
        }
    }

    private static void WriteBlocks(StringBuilder writer, JsonNode? content, string prefix)
    {
        foreach (JsonNode? child in content as JsonArray ?? [])
        {
            WriteBlock(writer, child, prefix);
        }
    }

    private static void WriteBlock(StringBuilder writer, JsonNode? node, string prefix)
    {
        if (node is not JsonObject block)
        {
            return;
        }

        switch (TypeOf(block))
        {
            case "doc":
            case "layoutSection":
            case "layoutColumn":
            case "bodiedSyncBlock":
            case "syncBlock":
                WriteBlocks(writer, block["content"], prefix);
                break;

            case "paragraph":
                WriteLines(writer, prefix, Inline(block["content"]));
                break;

            case "heading":
                int level = Math.Clamp(Attribute<int?>(block, "level") ?? 1, 1, 6);
                WriteLines(writer, prefix, new string('#', level) + " " + Inline(block["content"]));
                break;

            case "codeBlock":
                string language = Attribute<string>(block, "language") ?? string.Empty;
                string code = string.Concat((block["content"] as JsonArray ?? []).Select(child => child?["text"]?.GetValue<string>()));
                WriteLines(writer, prefix, $"```{language}\n{code}\n```");
                break;

            case "blockquote":
                WriteBlocks(writer, block["content"], prefix + "> ");
                break;

            case "panel":
                string panelType = Attribute<string>(block, "panelType") ?? "info";
                WriteLines(writer, prefix + "> ", $"**{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(panelType)}:**");
                WriteBlocks(writer, block["content"], prefix + "> ");
                break;

            case "rule":
                WriteLines(writer, prefix, "---");
                break;

            case "bulletList":
            case "orderedList":
            case "taskList":
            case "decisionList":
                WriteList(writer, block, prefix, indent: string.Empty);
                writer.Append('\n');
                break;

            case "table":
                WriteTable(writer, block, prefix);
                break;

            case "expand":
            case "nestedExpand":
                string title = Attribute<string>(block, "title") ?? string.Empty;
                WriteLines(writer, prefix, $"**{(title.Length == 0 ? "Details" : title)}**");
                WriteBlocks(writer, block["content"], prefix);
                break;

            case "mediaSingle":
            case "mediaGroup":
                // Files that are written back share one paragraph, which MarkdownToAdf reads back
                // into one mediaGroup; an image or video is a paragraph of its own.
                var run = new List<string>();
                bool single = TypeOf(block) == "mediaSingle";
                foreach (JsonObject media in (block["content"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (EmbeddedAttachment(media, single) is { } markdown)
                    {
                        run.Add(markdown);
                        continue;
                    }

                    if (run.Count > 0)
                    {
                        WriteLines(writer, prefix, string.Join("\n", run));
                        run.Clear();
                    }

                    WriteLines(writer, prefix, Placeholder(media));
                }

                if (run.Count > 0)
                {
                    WriteLines(writer, prefix, string.Join("\n", run));
                }

                break;

            default:
                // Block extensions (macros), cards, and anything this converter does not know.
                WriteLines(writer, prefix, Placeholder(block));
                break;
        }
    }

    private static void WriteLines(StringBuilder writer, string prefix, string text)
    {
        foreach (string line in text.Split('\n'))
        {
            writer.Append(prefix).Append(line).Append('\n');
        }

        writer.Append(prefix.TrimEnd()).Append('\n');
    }

    private static void WriteList(StringBuilder writer, JsonObject list, string prefix, string indent)
    {
        string listType = TypeOf(list);
        int number = Attribute<int?>(list, "order") ?? 1;

        foreach (JsonObject item in (list["content"] as JsonArray ?? []).OfType<JsonObject>())
        {
            string marker = listType switch
            {
                "orderedList" => $"{number++}. ",
                "taskList" => string.Equals(Attribute<string>(item, "state"), "DONE", StringComparison.Ordinal) ? "- [x] " : "- [ ] ",
                _ => "- ",
            };

            // Task and decision items hold inline content directly; list items hold blocks.
            if (TypeOf(item) is "taskItem" or "decisionItem")
            {
                writer.Append(prefix).Append(indent).Append(marker).Append(Inline(item["content"])).Append('\n');
                continue;
            }

            bool first = true;
            foreach (JsonObject child in (item["content"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (TypeOf(child) is "bulletList" or "orderedList" or "taskList")
                {
                    WriteList(writer, child, prefix, indent + "  ");
                    continue;
                }

                string text = TypeOf(child) == "paragraph" ? Inline(child["content"]) : Convert(child);
                string lead = first ? marker : new string(' ', marker.Length);
                foreach (string line in text.Split('\n'))
                {
                    writer.Append(prefix).Append(indent).Append(lead).Append(line).Append('\n');
                    lead = new string(' ', marker.Length);
                }

                first = false;
            }
        }
    }

    private static void WriteTable(StringBuilder writer, JsonObject table, string prefix)
    {
        List<List<string>> rows = (table["content"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(row => (row["content"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(cell => Convert(new JsonObject { ["type"] = "doc", ["content"] = cell["content"]?.DeepClone() })
                    .Replace("\n\n", "<br>", StringComparison.Ordinal)
                    .Replace("\n", "<br>", StringComparison.Ordinal)
                    .Replace("|", "\\|", StringComparison.Ordinal))
                .ToList())
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        int columns = rows.Max(row => row.Count);
        var text = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            List<string> cells = [.. rows[i], .. Enumerable.Repeat(string.Empty, columns - rows[i].Count)];
            text.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
            if (i == 0)
            {
                text.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", columns))).Append('\n');
            }
        }

        WriteLines(writer, prefix, text.ToString().TrimEnd('\n'));
    }

    private static string Inline(JsonNode? content)
    {
        var text = new StringBuilder();

        foreach (JsonObject node in (content as JsonArray ?? []).OfType<JsonObject>())
        {
            switch (TypeOf(node))
            {
                case "text":
                    text.Append(ApplyMarks(node["text"]?.GetValue<string>() ?? string.Empty, node["marks"] as JsonArray));
                    break;
                case "hardBreak":
                    text.Append("  \n");
                    break;
                case "mention":
                    text.Append(Mention(node));
                    break;
                case "emoji":
                    text.Append(Attribute<string>(node, "text") ?? Attribute<string>(node, "shortName") ?? string.Empty);
                    break;
                case "date":
                    text.Append(FormatDate(Attribute<string>(node, "timestamp")));
                    break;
                case "status":
                    text.Append('[').Append(Attribute<string>(node, "text")).Append(']');
                    break;
                case "inlineCard":
                    string? url = Attribute<string>(node, "url");
                    text.Append(url is null ? Placeholder(node) : $"[{url}]({url})");
                    break;
                default:
                    text.Append(Placeholder(node));
                    break;
            }
        }

        return text.ToString();
    }

    private static string ApplyMarks(string text, JsonArray? marks)
    {
        if (text.Length == 0 || marks is null)
        {
            return text;
        }

        string result = text;
        string? href = null;

        foreach (JsonObject mark in marks.OfType<JsonObject>())
        {
            switch (TypeOf(mark))
            {
                case "code":
                    result = $"`{result}`";
                    break;
                case "strong":
                    result = $"**{result}**";
                    break;
                case "em":
                    result = $"*{result}*";
                    break;
                case "strike":
                    result = $"~~{result}~~";
                    break;
                case "link":
                    href = Attribute<string>(mark, "href");
                    break;
            }
        }

        return href is null ? result : $"[{result}]({href})";
    }

    private static string FormatDate(string? timestamp)
        => long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out long milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : timestamp ?? string.Empty;

    /// <summary>
    /// Writes a node that Markdown cannot represent as an HTML comment that names its type and its
    /// identifying attributes.
    /// </summary>
    private static string Placeholder(JsonObject? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var parts = new List<string> { "adf:" + TypeOf(node) };
        foreach (string name in new[] { "extensionKey", "extensionType", "url", "id", "collection", "type", "text" })
        {
            if (node["attrs"]?[name] is JsonValue value && value.TryGetValue(out string? text) && text.Length > 0)
            {
                parts.Add($"{name}={text.Replace("--", "- -", StringComparison.Ordinal)}");
            }
        }

        if (node["attrs"]?["parameters"]?["macroParams"] is JsonObject macroParams)
        {
            foreach ((string name, JsonNode? value) in macroParams)
            {
                if (value?["value"] is JsonValue parameter && parameter.TryGetValue(out string? text))
                {
                    parts.Add($"{name}={text.Replace("--", "- -", StringComparison.Ordinal)}");
                }
            }
        }

        return $"<!-- {string.Join(' ', parts)} -->";
    }

    /// <summary>
    /// Writes a mention as <c>@[Display Name](accountid:ID)</c>, the syntax
    /// <see cref="MarkdownToAdf"/> reads back into the same mention. A mention without an account ID
    /// cannot be written back, so it becomes plain <c>@Display Name</c>.
    /// </summary>
    private static string Mention(JsonObject node)
    {
        string? id = Attribute<string>(node, "id");
        string name = (Attribute<string>(node, "text") ?? id ?? string.Empty).TrimStart('@');
        if (string.IsNullOrEmpty(id))
        {
            return "@" + name;
        }

        // A closing bracket would end the name early when the Markdown is read back, so it is dropped.
        name = name.Replace("]", string.Empty, StringComparison.Ordinal).Trim();
        return $"@[{(name.Length == 0 ? id : name)}](accountid:{id})";
    }

    /// <summary>
    /// Writes a media node that <see cref="Annotate"/> matched to an attachment as
    /// <c>![name](attachment:ID)</c>; or returns <see langword="null"/> for one it did not.
    /// The name is the image's alternative text when it has one, and the file name otherwise.
    /// </summary>
    private static string? EmbeddedAttachment(JsonObject media, bool single)
    {
        string? attachmentId = Attribute<string>(media, AttachmentIdAttribute);
        if (string.IsNullOrEmpty(attachmentId))
        {
            return null;
        }

        string? alt = Attribute<string>(media, "alt");
        string fileName = Attribute<string>(media, FileNameAttribute) ?? string.Empty;
        string name = single && !string.IsNullOrWhiteSpace(alt) ? alt : (fileName.Length > 0 ? fileName : alt ?? string.Empty);

        // A closing bracket would end the name early when the Markdown is read back, so it is dropped.
        return $"![{name.Replace("]", string.Empty, StringComparison.Ordinal).Trim()}](attachment:{attachmentId})";
    }

    /// <summary>
    /// Returns a copy of <paramref name="node"/> in which every media node whose ID is in
    /// <paramref name="attachments"/> carries its attachment ID and file name as extra attributes,
    /// for <see cref="EmbeddedAttachment"/>. The copy is only converted, never sent to Atlassian.
    /// </summary>
    private static JsonNode? Annotate(JsonNode? node, IReadOnlyDictionary<string, AttachmentReference>? attachments)
    {
        if (node is null || attachments is null || attachments.Count == 0)
        {
            return node;
        }

        JsonNode copy = node.DeepClone();
        AnnotateInPlace(copy, attachments);
        return copy;
    }

    private static void AnnotateInPlace(JsonNode? node, IReadOnlyDictionary<string, AttachmentReference> attachments)
    {
        switch (node)
        {
            case JsonObject item:
                if (TypeOf(item) == "media"
                    && item["attrs"] is JsonObject attrs
                    && Attribute<string>(item, "id") is { } mediaId
                    && attachments.TryGetValue(mediaId, out AttachmentReference? attachment))
                {
                    attrs[AttachmentIdAttribute] = attachment.AttachmentId;
                    attrs[FileNameAttribute] = attachment.FileName;
                }

                foreach ((_, JsonNode? value) in item)
                {
                    AnnotateInPlace(value, attachments);
                }

                break;

            case JsonArray items:
                foreach (JsonNode? child in items)
                {
                    AnnotateInPlace(child, attachments);
                }

                break;
        }
    }

    private static string TypeOf(JsonObject node) => node["type"] is JsonValue type && type.TryGetValue(out string? text) ? text : string.Empty;

    private static T? Attribute<T>(JsonObject node, string name)
    {
        if (node["attrs"]?[name] is not JsonValue value)
        {
            return default;
        }

        if (value.TryGetValue(out T? result))
        {
            return result;
        }

        // Numbers sometimes arrive as strings, and the reverse.
        try
        {
            return (T?)System.Convert.ChangeType(value.ToString(), Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return default;
        }
    }
}
