// <copyright file="MarkdownToAdf.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Atlassian.Mcp.Server.Common.Json;

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// Converts Markdown text to Atlassian Document Format (ADF), which Jira Cloud requires for
/// descriptions, comments, worklog comments, and multi-line text fields, and which Confluence uses
/// for page bodies.
/// <para>
/// Supports a pragmatic subset of Markdown: headings, paragraphs, fenced code blocks, tables,
/// block quotes, thematic breaks, bulleted and numbered lists (nested by indentation), and inline
/// code, bold, italic, strikethrough, and links. Inline marks nest, so bold around inline code
/// yields one text node carrying both marks.
/// </para>
/// </summary>
public static partial class MarkdownToAdf
{
    private static readonly Mark StrongMark = new("strong", new { type = "strong" });
    private static readonly Mark EmMark = new("em", new { type = "em" });
    private static readonly Mark StrikeMark = new("strike", new { type = "strike" });
    private static readonly Mark CodeMark = new("code", new { type = "code" });

    /// <summary>
    /// Converts Markdown text into an ADF document.
    /// </summary>
    /// <param name="markdown">The Markdown text. <see langword="null"/> is treated as empty.</param>
    /// <returns>The ADF document.</returns>
    public static JsonObject Convert(string? markdown)
        => (JsonObject)JsonSerializer.SerializeToNode(
            new { type = "doc", version = 1, content = ParseDocument(markdown ?? string.Empty) },
            JsonDefaults.Options)!;

    /// <summary>
    /// Converts the Markdown string values of rich-text fields in a field map into ADF documents,
    /// leaving every other entry exactly as the caller wrote it.
    /// <para>
    /// Jira Cloud rejects a plain string for a multi-line text field (such as "Steps to Reproduce")
    /// with "The field value is not valid Atlassian Document Format (ADF) content", so those values
    /// are converted here. A value that is already a JSON object is left alone, so a caller that
    /// builds the ADF itself still works. <paramref name="isRichTextField"/> is consulted only for
    /// string values, so a map of plain option or array fields never triggers a metadata lookup.
    /// </para>
    /// </summary>
    /// <param name="fields">The field map, keyed by field ID. It is changed in place.</param>
    /// <param name="isRichTextField">Returns <see langword="true"/> for the ID of a field that holds ADF.</param>
    public static void ConvertRichTextFields(JsonObject fields, Func<string, bool> isRichTextField)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(isRichTextField);

        foreach (string id in fields.Select(pair => pair.Key).ToList())
        {
            if (fields[id] is JsonValue value
                && value.TryGetValue(out string? text)
                && isRichTextField(id))
            {
                fields[id] = Convert(text);
            }
        }
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(\s*)[-*]\s+(.*)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^(\s*)\d+\.\s+(.*)$")]
    private static partial Regex OrderedPattern();

    /// <summary>
    /// A code fence: three or more backticks or tildes. On an opening fence, the first token of the
    /// info string is the language; anything after it is ignored.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"^\s*(`{3,}|~{3,})\s*(\S*).*$")]
    private static partial Regex FencePattern();

    /// <summary>A thematic break: three or more of the same -, *, or _ character, optionally spaced.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"^\s*([-*_])\s*(\1\s*){2,}$")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"^\s*>\s?(.*)$")]
    private static partial Regex QuotePattern();

    /// <summary>A table row: pipe-delimited cells with both a leading and a trailing pipe.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"^\s*\|(.*)\|\s*$")]
    private static partial Regex TableRowPattern();

    /// <summary>A cell separator: a pipe that is not escaped as \|.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex CellSeparatorPattern();

    /// <summary>
    /// Inline tokens, in precedence order: code span, bold, strikethrough, italic (* or _), and link.
    /// The two italic branches share the group name "em".
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"`(?<code>[^`]+)`|\*\*(?<strong>.+?)\*\*|~~(?<strike>.+?)~~|\*(?<em>[^*]+)\*|(?<!\w)_(?<em>.+?)_(?!\w)|\[(?<linkText>[^\]]+)\]\((?<href>[^)]+)\)")]
    private static partial Regex InlinePattern();

    private static Mark LinkMark(string href) => new("link", new { type = "link", attrs = new { href } });

    private static List<object> ParseDocument(string markdown)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        List<object> blocks = ParseBlocks(lines, inQuote: false);

        if (blocks.Count == 0)
        {
            blocks.Add(new { type = "paragraph", content = new List<object>() });
        }

        return blocks;
    }

    /// <summary>
    /// Parses <paramref name="lines"/> into ADF block nodes.
    /// </summary>
    /// <param name="lines">The Markdown lines, already split on single line feeds.</param>
    /// <param name="inQuote">
    /// <see langword="true"/> while parsing the contents of a block quote. An ADF block quote
    /// accepts only paragraphs, lists, and code blocks, so headings become bold paragraphs, and
    /// rules, tables, and nested quotes stay paragraph text rather than producing nodes that Jira
    /// would reject.
    /// </param>
    /// <returns>The block nodes.</returns>
    private static List<object> ParseBlocks(string[] lines, bool inQuote)
    {
        var blocks = new List<object>();
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            Match fence = FencePattern().Match(line);
            if (fence.Success)
            {
                blocks.Add(ReadCodeBlock(lines, ref i, fence.Groups[1].Value, fence.Groups[2].Value));
                continue;
            }

            if (!inQuote && RulePattern().IsMatch(line))
            {
                blocks.Add(new { type = "rule" });
                i++;
                continue;
            }

            Match heading = HeadingPattern().Match(line);
            if (heading.Success)
            {
                string text = heading.Groups[2].Value.Trim();
                blocks.Add(inQuote
                    ? (object)new { type = "paragraph", content = ParseInline(text, [StrongMark]) }
                    : new
                    {
                        type = "heading",
                        attrs = new { level = heading.Groups[1].Value.Length },
                        content = ParseInline(text),
                    });
                i++;
                continue;
            }

            if (!inQuote && QuotePattern().IsMatch(line))
            {
                var quoted = new List<string>();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && QuotePattern().IsMatch(lines[i]))
                {
                    quoted.Add(QuotePattern().Match(lines[i]).Groups[1].Value);
                    i++;
                }

                blocks.Add(new { type = "blockquote", content = ParseBlocks(quoted.ToArray(), inQuote: true) });
                continue;
            }

            if (!inQuote && IsTableStart(lines, i))
            {
                blocks.Add(ReadTable(lines, ref i));
                continue;
            }

            if (BulletPattern().IsMatch(line) || OrderedPattern().IsMatch(line))
            {
                var items = new List<ListLine>();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    Match bullet = BulletPattern().Match(lines[i]);
                    Match ordered = OrderedPattern().Match(lines[i]);
                    if (bullet.Success)
                    {
                        items.Add(new ListLine(bullet.Groups[1].Value.Length, false, bullet.Groups[2].Value));
                    }
                    else if (ordered.Success)
                    {
                        items.Add(new ListLine(ordered.Groups[1].Value.Length, true, ordered.Groups[2].Value));
                    }
                    else
                    {
                        break;
                    }

                    i++;
                }

                int position = 0;
                blocks.Add(BuildList(items, ref position, items[0].Indent));
                continue;
            }

            // A paragraph: consecutive plain lines join into one block.
            var paragraph = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !StartsBlock(lines, i, inQuote))
            {
                paragraph.Add(lines[i]);
                i++;
            }

            // Every branch above either consumed the line or fell through to here, so the first
            // line of a paragraph can never start a block; this guarantees progress regardless.
            if (paragraph.Count == 0)
            {
                paragraph.Add(lines[i]);
                i++;
            }

            blocks.Add(new { type = "paragraph", content = BuildParagraphContent(paragraph) });
        }

        return blocks;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the line at <paramref name="index"/> starts a block that
    /// the paragraph accumulator must not swallow. It mirrors the branches of
    /// <see cref="ParseBlocks(string[], bool)"/>, including the ones suppressed inside a block
    /// quote, so that the two stay in step.
    /// </summary>
    private static bool StartsBlock(string[] lines, int index, bool inQuote)
    {
        string line = lines[index];

        if (FencePattern().IsMatch(line) || HeadingPattern().IsMatch(line) || BulletPattern().IsMatch(line) || OrderedPattern().IsMatch(line))
        {
            return true;
        }

        return !inQuote && (RulePattern().IsMatch(line) || QuotePattern().IsMatch(line) || IsTableStart(lines, index));
    }

    /// <summary>
    /// Reads a fenced code block, advancing <paramref name="i"/> past the closing fence. The
    /// contents are taken verbatim, with no trimming or inline parsing, and keep their line breaks.
    /// An unterminated fence runs to the end of the input.
    /// </summary>
    private static object ReadCodeBlock(string[] lines, ref int i, string delimiter, string language)
    {
        var code = new List<string>();
        i++;

        while (i < lines.Length && !IsClosingFence(lines[i], delimiter))
        {
            code.Add(lines[i]);
            i++;
        }

        if (i < lines.Length)
        {
            i++;
        }

        string text = string.Join("\n", code);
        List<object> content = string.IsNullOrEmpty(text) ? [] : [new { type = "text", text }];

        // ADF forbids marks inside a code block, so the single text node carries none.
        return string.IsNullOrEmpty(language)
            ? (object)new { type = "codeBlock", content }
            : new { type = "codeBlock", attrs = new { language }, content };
    }

    /// <summary>
    /// Returns <see langword="true"/> when the line closes <paramref name="opening"/>: the same
    /// fence character, repeated at least as many times, and nothing else on the line.
    /// </summary>
    private static bool IsClosingFence(string line, string opening)
    {
        string trimmed = line.Trim();
        return trimmed.Length >= opening.Length && trimmed.All(c => c == opening[0]);
    }

    /// <summary>
    /// Returns <see langword="true"/> when a GitHub-style table starts at <paramref name="index"/>:
    /// a pipe-delimited header row followed by a delimiter row such as <c>| --- | :-: |</c>.
    /// </summary>
    private static bool IsTableStart(string[] lines, int index)
        => TableRowPattern().IsMatch(lines[index])
           && index + 1 < lines.Length
           && lines[index + 1].Contains('|', StringComparison.Ordinal)
           && IsTableDelimiter(lines[index + 1]);

    private static bool IsTableDelimiter(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Contains('-', StringComparison.Ordinal)
               && trimmed.All(c => c is '|' or '-' or ':' or ' ' or '\t');
    }

    /// <summary>
    /// Reads a table (the header row, the delimiter row, then data rows until the first line that
    /// is not a table row), advancing <paramref name="i"/> past it.
    /// </summary>
    private static object ReadTable(string[] lines, ref int i)
    {
        var rows = new List<object> { BuildTableRow(SplitTableCells(lines[i]), header: true) };
        i += 2;

        while (i < lines.Length && TableRowPattern().IsMatch(lines[i]))
        {
            rows.Add(BuildTableRow(SplitTableCells(lines[i]), header: false));
            i++;
        }

        return new
        {
            type = "table",
            attrs = new { isNumberColumnEnabled = false, layout = "default" },
            content = rows,
        };
    }

    private static object BuildTableRow(List<string> cells, bool header)
        => new
        {
            type = "tableRow",
            content = cells.Select(cell => (object)new
            {
                type = header ? "tableHeader" : "tableCell",
                attrs = new { },
                content = new List<object> { new { type = "paragraph", content = ParseInline(cell) } },
            }).ToList(),
        };

    /// <summary>Splits a table row into cell texts, honoring pipes escaped as \|.</summary>
    private static List<string> SplitTableCells(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        return CellSeparatorPattern().Split(trimmed)
            .Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())
            .ToList();
    }

    /// <summary>
    /// Builds the inline content of a paragraph. Soft-wrapped lines join with a space, as in
    /// Markdown; a line ending in two spaces or a backslash produces an explicit hard break.
    /// </summary>
    private static List<object> BuildParagraphContent(List<string> lines)
    {
        var content = new List<object>();
        var pending = new List<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            string raw = lines[i];
            bool hardBreak = i < lines.Count - 1
                             && (raw.EndsWith("  ", StringComparison.Ordinal) || raw.TrimEnd().EndsWith('\\'));

            string text = raw.Trim();
            if (hardBreak && text.EndsWith('\\'))
            {
                text = text[..^1].TrimEnd();
            }

            pending.Add(text);

            if (hardBreak)
            {
                content.AddRange(ParseInline(string.Join(" ", pending)));
                content.Add(new { type = "hardBreak" });
                pending.Clear();
            }
        }

        if (pending.Count > 0)
        {
            content.AddRange(ParseInline(string.Join(" ", pending)));
        }

        return content;
    }

    private static object BuildList(List<ListLine> items, ref int position, int indent)
    {
        bool ordered = items[position].Ordered;
        var listItems = new List<object>();

        while (position < items.Count && items[position].Indent == indent)
        {
            ListLine current = items[position];
            position++;

            var itemContent = new List<object>
            {
                new { type = "paragraph", content = ParseInline(current.Text) },
            };

            // A deeper indent after this item is a nested list that belongs to it.
            if (position < items.Count && items[position].Indent > indent)
            {
                itemContent.Add(BuildList(items, ref position, items[position].Indent));
            }

            listItems.Add(new { type = "listItem", content = itemContent });
        }

        return ordered
            ? (object)new { type = "orderedList", attrs = new { order = 1 }, content = listItems }
            : new { type = "bulletList", content = listItems };
    }

    private static List<object> ParseInline(string text) => ParseInline(text, []);

    /// <summary>
    /// Parses inline Markdown into ADF text nodes, carrying <paramref name="marks"/> down so that
    /// nested spans (such as bold around inline code) accumulate both marks on the leaf text node.
    /// Code spans are terminal: their contents are literal and are not parsed further.
    /// </summary>
    private static List<object> ParseInline(string text, List<Mark> marks)
    {
        var nodes = new List<object>();
        if (string.IsNullOrEmpty(text))
        {
            return nodes;
        }

        int position = 0;
        foreach (Match match in InlinePattern().Matches(text))
        {
            if (match.Index > position)
            {
                AddText(nodes, text[position..match.Index], marks);
            }

            if (match.Groups["code"].Success)
            {
                AddText(nodes, match.Groups["code"].Value, WithMark(marks, CodeMark));
            }
            else if (match.Groups["strong"].Success)
            {
                nodes.AddRange(ParseInline(match.Groups["strong"].Value, WithMark(marks, StrongMark)));
            }
            else if (match.Groups["strike"].Success)
            {
                nodes.AddRange(ParseInline(match.Groups["strike"].Value, WithMark(marks, StrikeMark)));
            }
            else if (match.Groups["em"].Success)
            {
                nodes.AddRange(ParseInline(match.Groups["em"].Value, WithMark(marks, EmMark)));
            }
            else if (match.Groups["linkText"].Success)
            {
                nodes.AddRange(ParseInline(match.Groups["linkText"].Value, WithMark(marks, LinkMark(match.Groups["href"].Value))));
            }

            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            AddText(nodes, text[position..], marks);
        }

        return nodes;
    }

    /// <summary>
    /// Returns a new mark list with <paramref name="add"/> appended, unless a mark of the same type
    /// is already present.
    /// </summary>
    private static List<Mark> WithMark(List<Mark> marks, Mark add)
        => marks.Any(existing => existing.Type == add.Type) ? marks : [.. marks, add];

    private static void AddText(List<object> nodes, string text, List<Mark> marks)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        nodes.Add(marks.Count == 0
            ? (object)new { type = "text", text }
            : new { type = "text", text, marks = marks.Select(mark => mark.Adf).ToArray() });
    }

    /// <summary>An ADF mark and its type name, used to avoid duplicates when marks nest.</summary>
    /// <param name="Type">The mark type.</param>
    /// <param name="Adf">The ADF mark object.</param>
    private sealed record Mark(string Type, object Adf);

    /// <summary>One line of a Markdown list.</summary>
    /// <param name="Indent">The number of leading spaces.</param>
    /// <param name="Ordered">A value indicating whether the line is a numbered item.</param>
    /// <param name="Text">The item text.</param>
    private sealed record ListLine(int Indent, bool Ordered, string Text);
}
