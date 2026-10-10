// <copyright file="MarkdownToAdfTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Tests.Common.Adf;

/// <summary>
/// Tests for <see cref="MarkdownToAdf"/>.
/// </summary>
[TestClass]
public sealed class MarkdownToAdfTests
{
    /// <summary>
    /// Verifies that converting Markdown with fenced code block preserves newlines and language.
    /// </summary>
    [TestMethod]
    public void Convert_WithFencedCodeBlock_PreservesNewlinesAndLanguage()
    {
        // Regression: fences used to fall through to the paragraph accumulator, which trimmed and
        // space-joined the lines, then the inline code-span regex matched across the whole block.
        const string markdown = """
            Changing the getters:

            ```csharp
            public DateTime NextRunTime
            {
                get { return DateTimeOffset.FromUnixTimeSeconds(startTime).LocalDateTime; }
            }
            ```

            The tests were not updated.
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(3, blocks.GetArrayLength());
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual("codeBlock", AdfNodes.TypeOf(blocks[1]));
        Assert.AreEqual("csharp", blocks[1].GetProperty("attrs").GetProperty("language").GetString());
        Assert.AreEqual(
            "public DateTime NextRunTime\n{\n    get { return DateTimeOffset.FromUnixTimeSeconds(startTime).LocalDateTime; }\n}",
            AdfNodes.TextOf(blocks[1]));
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[2]));
        Assert.AreEqual("The tests were not updated.", AdfNodes.TextOf(blocks[2]));
    }

    /// <summary>
    /// Verifies that converting Markdown with fenced code block containing blank line keeps the blank line.
    /// </summary>
    [TestMethod]
    public void Convert_WithFencedCodeBlockContainingBlankLine_KeepsTheBlankLine()
    {
        const string markdown = """
            ```csharp
            var x = 1;

            var y = 2;
            ```
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(1, blocks.GetArrayLength());
        Assert.AreEqual("var x = 1;\n\nvar y = 2;", AdfNodes.TextOf(blocks[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with fenced code block without language omits the language attribute.
    /// </summary>
    [TestMethod]
    public void Convert_WithFencedCodeBlockWithoutLanguage_OmitsTheLanguageAttribute()
    {
        const string markdown = """
            ```
            plain text
            ```
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual("codeBlock", AdfNodes.TypeOf(blocks[0]));
        Assert.IsFalse(blocks[0].TryGetProperty("attrs", out _));
        Assert.AreEqual("plain text", AdfNodes.TextOf(blocks[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with tilde fence produces code block.
    /// </summary>
    [TestMethod]
    public void Convert_WithTildeFence_ProducesCodeBlock()
    {
        const string markdown = """
            ~~~
            plain text
            ~~~
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(1, blocks.GetArrayLength());
        Assert.AreEqual("codeBlock", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual("plain text", AdfNodes.TextOf(blocks[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with unterminated fence treats the remainder as code.
    /// </summary>
    [TestMethod]
    public void Convert_WithUnterminatedFence_TreatsTheRemainderAsCode()
    {
        const string markdown = """
            ```csharp
            var x = 1;
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(1, blocks.GetArrayLength());
        Assert.AreEqual("codeBlock", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual("var x = 1;", AdfNodes.TextOf(blocks[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with fence on the line after text keeps the blocks separate.
    /// </summary>
    [TestMethod]
    public void Convert_WithFenceOnTheLineAfterText_KeepsTheBlocksSeparate()
    {
        // No blank line between the paragraph and the fence.
        const string markdown = """
            Update the assertion:
            ```csharp
            Assert.AreEqual(expected, actual);
            ```
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(2, blocks.GetArrayLength());
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual("Update the assertion:", AdfNodes.TextOf(blocks[0]));
        Assert.AreEqual("codeBlock", AdfNodes.TypeOf(blocks[1]));
    }

    /// <summary>
    /// Verifies that converting Markdown with backticks inside fenced code does not produce code marks.
    /// </summary>
    [TestMethod]
    public void Convert_WithBackticksInsideFencedCode_DoesNotProduceCodeMarks()
    {
        const string markdown = """
            ```csharp
            var s = "`x`";
            ```
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        var text = blocks[0].GetProperty("content")[0];
        Assert.AreEqual("var s = \"`x`\";", text.GetProperty("text").GetString());
        Assert.IsFalse(text.TryGetProperty("marks", out _), "ADF forbids marks inside a code block.");
    }

    /// <summary>
    /// Verifies that converting Markdown with inline code still produces a code mark.
    /// </summary>
    [TestMethod]
    public void Convert_WithInlineCode_StillProducesACodeMark()
    {
        JsonElement blocks = AdfNodes.Blocks("Use `Assert.AreEqual` here.");

        var content = blocks[0].GetProperty("content");
        Assert.AreEqual(3, content.GetArrayLength());
        Assert.AreEqual("Use ", content[0].GetProperty("text").GetString());
        Assert.AreEqual("Assert.AreEqual", content[1].GetProperty("text").GetString());
        CollectionAssert.AreEqual(new[] { "code" }, AdfNodes.MarksOf(content[1]));
        Assert.AreEqual(" here.", content[2].GetProperty("text").GetString());
    }

    /// <summary>
    /// Verifies that converting Markdown with table produces table with header row.
    /// </summary>
    [TestMethod]
    public void Convert_WithTable_ProducesTableWithHeaderRow()
    {
        const string markdown = """
            | Job | True next run |
            |-----|---------------|
            | A   | 9:00 PM       |
            | B   | 11:00 PM      |
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(1, blocks.GetArrayLength());
        Assert.AreEqual("table", AdfNodes.TypeOf(blocks[0]));
        Assert.IsFalse(blocks[0].GetProperty("attrs").GetProperty("isNumberColumnEnabled").GetBoolean());
        Assert.AreEqual("default", blocks[0].GetProperty("attrs").GetProperty("layout").GetString());

        var rows = blocks[0].GetProperty("content");
        Assert.AreEqual(3, rows.GetArrayLength());

        var header = rows[0].GetProperty("content");
        Assert.AreEqual("tableHeader", AdfNodes.TypeOf(header[0]));
        Assert.AreEqual("Job", AdfNodes.TextOf(header[0].GetProperty("content")[0]));
        Assert.AreEqual("True next run", AdfNodes.TextOf(header[1].GetProperty("content")[0]));

        var firstRow = rows[1].GetProperty("content");
        Assert.AreEqual("tableCell", AdfNodes.TypeOf(firstRow[0]));
        Assert.AreEqual("A", AdfNodes.TextOf(firstRow[0].GetProperty("content")[0]));
        Assert.AreEqual("9:00 PM", AdfNodes.TextOf(firstRow[1].GetProperty("content")[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with escaped pipe in cell keeps the pipe in the cell text.
    /// </summary>
    [TestMethod]
    public void Convert_WithEscapedPipeInCell_KeepsThePipeInTheCellText()
    {
        const string markdown = """
            | Expression | Result |
            |------------|--------|
            | a \| b     | true   |
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        var cells = blocks[0].GetProperty("content")[1].GetProperty("content");
        Assert.AreEqual(2, cells.GetArrayLength());
        Assert.AreEqual("a | b", AdfNodes.TextOf(cells[0].GetProperty("content")[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with inline markup in cell parses the cell content.
    /// </summary>
    [TestMethod]
    public void Convert_WithInlineMarkupInCell_ParsesTheCellContent()
    {
        const string markdown = """
            | Field | Value |
            |-------|-------|
            | `id`  | **1** |
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        var cells = blocks[0].GetProperty("content")[1].GetProperty("content");
        CollectionAssert.AreEqual(new[] { "code" }, AdfNodes.MarksOf(cells[0].GetProperty("content")[0].GetProperty("content")[0]));
        CollectionAssert.AreEqual(new[] { "strong" }, AdfNodes.MarksOf(cells[1].GetProperty("content")[0].GetProperty("content")[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with pipe line but no delimiter row produces a paragraph.
    /// </summary>
    [TestMethod]
    public void Convert_WithPipeLineButNoDelimiterRow_ProducesAParagraph()
    {
        JsonElement blocks = AdfNodes.Blocks("| this is not | a table |");

        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual("| this is not | a table |", AdfNodes.TextOf(blocks[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with blockquote produces blockquote with paragraph.
    /// </summary>
    [TestMethod]
    public void Convert_WithBlockquote_ProducesBlockquoteWithParagraph()
    {
        const string markdown = """
            > Displaying dates in the user's time zone
            > is a UI-only concern.

            Outside the quote.
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(2, blocks.GetArrayLength());
        Assert.AreEqual("blockquote", AdfNodes.TypeOf(blocks[0]));

        var quoted = blocks[0].GetProperty("content");
        Assert.AreEqual(1, quoted.GetArrayLength());
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(quoted[0]));
        Assert.AreEqual("Displaying dates in the user's time zone is a UI-only concern.", AdfNodes.TextOf(quoted[0]));
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[1]));
    }

    /// <summary>
    /// Verifies that converting Markdown with fence inside blockquote keeps the code block inside the quote.
    /// </summary>
    [TestMethod]
    public void Convert_WithFenceInsideBlockquote_KeepsTheCodeBlockInsideTheQuote()
    {
        const string markdown = """
            > Note:
            > ```csharp
            > var x = 1;
            > ```
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual("blockquote", AdfNodes.TypeOf(blocks[0]));

        var quoted = blocks[0].GetProperty("content");
        Assert.AreEqual(2, quoted.GetArrayLength());
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(quoted[0]));
        Assert.AreEqual("codeBlock", AdfNodes.TypeOf(quoted[1]));
        Assert.AreEqual("var x = 1;", AdfNodes.TextOf(quoted[1]));
    }

    /// <summary>
    /// Verifies that converting Markdown with heading inside blockquote produces a strong paragraph.
    /// </summary>
    [TestMethod]
    public void Convert_WithHeadingInsideBlockquote_ProducesAStrongParagraph()
    {
        // ADF's blockquote accepts no heading node, so the heading degrades to bold text.
        const string markdown = """
            > ## Heads up
            > Body text.
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        var quoted = blocks[0].GetProperty("content");
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(quoted[0]));
        CollectionAssert.AreEqual(new[] { "strong" }, AdfNodes.MarksOf(quoted[0].GetProperty("content")[0]));
        Assert.AreEqual("Heads up", AdfNodes.TextOf(quoted[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with thematic break produces rule.
    /// </summary>
    [TestMethod]
    public void Convert_WithThematicBreak_ProducesRule()
    {
        const string markdown = """
            Above.

            ---

            Below.
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual(3, blocks.GetArrayLength());
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual("rule", AdfNodes.TypeOf(blocks[1]));
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[2]));
    }

    /// <summary>
    /// Verifies that converting Markdown with spaced thematic break produces rule not a list.
    /// </summary>
    [TestMethod]
    public void Convert_WithSpacedThematicBreak_ProducesRuleNotAList()
    {
        JsonElement blocks = AdfNodes.Blocks("- - -");

        Assert.AreEqual("rule", AdfNodes.TypeOf(blocks[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with strikethrough produces strike mark.
    /// </summary>
    [TestMethod]
    public void Convert_WithStrikethrough_ProducesStrikeMark()
    {
        JsonElement blocks = AdfNodes.Blocks("This is ~~obsolete~~ now.");

        var content = blocks[0].GetProperty("content");
        Assert.AreEqual("obsolete", content[1].GetProperty("text").GetString());
        CollectionAssert.AreEqual(new[] { "strike" }, AdfNodes.MarksOf(content[1]));
    }

    /// <summary>
    /// Verifies that converting Markdown with trailing double space produces hard break.
    /// </summary>
    [TestMethod]
    public void Convert_WithTrailingDoubleSpace_ProducesHardBreak()
    {
        JsonElement blocks = AdfNodes.Blocks("I recommend returning UTC dates.  \nDisplaying them locally is a UI concern.");

        var content = blocks[0].GetProperty("content");
        Assert.AreEqual(3, content.GetArrayLength());
        Assert.AreEqual("I recommend returning UTC dates.", content[0].GetProperty("text").GetString());
        Assert.AreEqual("hardBreak", AdfNodes.TypeOf(content[1]));
        Assert.AreEqual("Displaying them locally is a UI concern.", content[2].GetProperty("text").GetString());
    }

    /// <summary>
    /// Verifies that converting Markdown with trailing backslash produces hard break without the backslash.
    /// </summary>
    [TestMethod]
    public void Convert_WithTrailingBackslash_ProducesHardBreakWithoutTheBackslash()
    {
        JsonElement blocks = AdfNodes.Blocks("First line\\\nSecond line");

        var content = blocks[0].GetProperty("content");
        Assert.AreEqual(3, content.GetArrayLength());
        Assert.AreEqual("First line", content[0].GetProperty("text").GetString());
        Assert.AreEqual("hardBreak", AdfNodes.TypeOf(content[1]));
        Assert.AreEqual("Second line", content[2].GetProperty("text").GetString());
    }

    /// <summary>
    /// Verifies that converting Markdown with soft wrapped paragraph joins lines with a space.
    /// </summary>
    [TestMethod]
    public void Convert_WithSoftWrappedParagraph_JoinsLinesWithASpace()
    {
        JsonElement blocks = AdfNodes.Blocks("First line\nsecond line");

        var content = blocks[0].GetProperty("content");
        Assert.AreEqual(1, content.GetArrayLength());
        Assert.AreEqual("First line second line", content[0].GetProperty("text").GetString());
    }

    /// <summary>
    /// Verifies that converting Markdown with nested list nests the child list.
    /// </summary>
    [TestMethod]
    public void Convert_WithNestedList_NestsTheChildList()
    {
        const string markdown = """
            - outer
              - inner
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual("bulletList", AdfNodes.TypeOf(blocks[0]));

        var outerItem = blocks[0].GetProperty("content")[0].GetProperty("content");
        Assert.AreEqual(2, outerItem.GetArrayLength());
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(outerItem[0]));
        Assert.AreEqual("bulletList", AdfNodes.TypeOf(outerItem[1]));
        Assert.AreEqual("inner", AdfNodes.TextOf(outerItem[1].GetProperty("content")[0].GetProperty("content")[0]));
    }

    /// <summary>
    /// Verifies that converting Markdown with heading and link produces heading with link mark.
    /// </summary>
    [TestMethod]
    public void Convert_WithHeadingAndLink_ProducesHeadingWithLinkMark()
    {
        const string markdown = """
            ## Symptom

            See [build 23174](http://ag-tfs/build?id=23174) for the failures.
            """;

        JsonElement blocks = AdfNodes.Blocks(markdown);

        Assert.AreEqual("heading", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual(2, blocks[0].GetProperty("attrs").GetProperty("level").GetInt32());

        var link = blocks[1].GetProperty("content")[1];
        CollectionAssert.AreEqual(new[] { "link" }, AdfNodes.MarksOf(link));
        Assert.AreEqual(
            "http://ag-tfs/build?id=23174",
            link.GetProperty("marks")[0].GetProperty("attrs").GetProperty("href").GetString());
    }

    /// <summary>
    /// Verifies that converting Markdown with empty text produces an empty paragraph.
    /// </summary>
    [TestMethod]
    public void Convert_WithEmptyText_ProducesAnEmptyParagraph()
    {
        JsonElement blocks = AdfNodes.Blocks(string.Empty);

        Assert.AreEqual(1, blocks.GetArrayLength());
        Assert.AreEqual("paragraph", AdfNodes.TypeOf(blocks[0]));
        Assert.AreEqual(0, blocks[0].GetProperty("content").GetArrayLength());
    }

    /// <summary>
    /// Verifies that converting Markdown with CRLF line endings parses blocks the same as LF.
    /// </summary>
    [TestMethod]
    public void Convert_WithCrLfLineEndings_ParsesBlocksTheSameAsLf()
    {
        string crlf = MarkdownToAdf.Convert("# Title\r\n\r\n```sh\r\nls -l\r\n```\r\n").ToJsonString();
        string lf = MarkdownToAdf.Convert("# Title\n\n```sh\nls -l\n```\n").ToJsonString();

        Assert.AreEqual(lf, crlf);
    }

    /// <summary>
    /// Verifies that a Markdown string for a rich-text field is converted to an ADF document.
    /// </summary>
    [TestMethod]
    public void ConvertRichTextFields_WithMarkdownStringForRichTextField_ConvertsItToAdf()
    {
        // Arrange
        var fields = new JsonObject { ["customfield_10050"] = "1. Open the app\n2. Click **Save**" };

        // Act
        MarkdownToAdf.ConvertRichTextFields(fields, id => id == "customfield_10050");

        // Assert
        JsonObject document = (JsonObject)fields["customfield_10050"]!;
        Assert.AreEqual("doc", document["type"]!.GetValue<string>());
        Assert.AreEqual("orderedList", document["content"]![0]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a string for a plain field is left unchanged.
    /// </summary>
    [TestMethod]
    public void ConvertRichTextFields_WithStringForPlainField_LeavesItUnchanged()
    {
        // Arrange
        var fields = new JsonObject { ["summary"] = "A **summary**" };

        // Act
        MarkdownToAdf.ConvertRichTextFields(fields, id => id == "description");

        // Assert
        Assert.AreEqual("A **summary**", fields["summary"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an ADF object supplied by the caller is left unchanged.
    /// </summary>
    [TestMethod]
    public void ConvertRichTextFields_WithCallerSuppliedAdfObject_LeavesItUnchanged()
    {
        // Arrange
        var adf = new JsonObject { ["type"] = "doc", ["version"] = 1, ["content"] = new JsonArray() };
        var fields = new JsonObject { ["customfield_10050"] = adf };

        // Act
        MarkdownToAdf.ConvertRichTextFields(fields, id => id == "customfield_10050");

        // Assert
        Assert.AreSame(adf, fields["customfield_10050"]);
    }

    /// <summary>
    /// Verifies that the predicate is not consulted when no value is a string, so that a map of
    /// option or array fields never triggers a metadata lookup.
    /// </summary>
    [TestMethod]
    public void ConvertRichTextFields_WithNoStringValues_NeverConsultsThePredicate()
    {
        // Arrange
        var fields = new JsonObject
        {
            ["priority"] = new JsonObject { ["name"] = "High" },
            ["fixVersions"] = new JsonArray(new JsonObject { ["name"] = "1.0" }),
        };
        bool consulted = false;

        // Act
        MarkdownToAdf.ConvertRichTextFields(fields, id =>
        {
            consulted = true;
            return true;
        });

        // Assert
        Assert.IsFalse(consulted);
    }

    /// <summary>
    /// Verifies that converting one field keeps every other entry.
    /// </summary>
    [TestMethod]
    public void ConvertRichTextFields_WithOneFieldToConvert_KeepsTheOtherEntries()
    {
        // Arrange
        var fields = new JsonObject
        {
            ["environment"] = "Windows 11",
            ["labels"] = new JsonArray("a", "b"),
        };

        // Act
        MarkdownToAdf.ConvertRichTextFields(fields, id => id == "environment");

        // Assert
        Assert.AreEqual(2, fields.Count);
        Assert.AreEqual("doc", fields["environment"]!["type"]!.GetValue<string>());
        Assert.AreEqual(2, fields["labels"]!.AsArray().Count);
    }

    /// <summary>
    /// Verifies that a mention with an explicit account ID becomes a mention node between the text around it.
    /// </summary>
    [TestMethod]
    public void Convert_WithMentionWithAccountId_WritesMentionNode()
    {
        // Act
        JsonElement paragraph = AdfNodes.Blocks("Hi @[Jane Doe](accountid:5b10ac8d), please review.")[0];

        // Assert
        JsonElement[] content = paragraph.GetProperty("content").EnumerateArray().ToArray();
        Assert.HasCount(3, content);
        Assert.AreEqual("Hi ", content[0].GetProperty("text").GetString());
        Assert.AreEqual("mention", AdfNodes.TypeOf(content[1]));
        Assert.AreEqual("5b10ac8d", content[1].GetProperty("attrs").GetProperty("id").GetString());
        Assert.AreEqual("@Jane Doe", content[1].GetProperty("attrs").GetProperty("text").GetString());
        Assert.AreEqual(", please review.", content[2].GetProperty("text").GetString());
    }

    /// <summary>
    /// Verifies that a mention written without an account ID takes it from the resolved names.
    /// </summary>
    [TestMethod]
    public void Convert_WithMentionAndResolvedName_WritesMentionNode()
    {
        // Arrange
        var accountIds = new Dictionary<string, string> { ["Jane Doe"] = "abc" };

        // Act
        JsonObject document = MarkdownToAdf.Convert("@[Jane Doe] and @[Jane Doe]", AdfReferences.ForMentions(accountIds));

        // Assert
        JsonArray content = document["content"]![0]!["content"]!.AsArray();
        Assert.AreEqual("abc", content[0]!["attrs"]!["id"]!.GetValue<string>());
        Assert.AreEqual("abc", content[2]!["attrs"]!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that a mention whose name was not resolved is refused rather than posted without an account ID.
    /// </summary>
    [TestMethod]
    public void Convert_WithUnresolvedMention_ThrowsInvalidOperationException()
    {
        // Act and assert
        Assert.ThrowsExactly<InvalidOperationException>(() => MarkdownToAdf.Convert("Hi @[Jane Doe]"));
    }

    /// <summary>
    /// Verifies that a mention inside bold text, a list item, and a table cell is a mention node
    /// without the surrounding marks.
    /// </summary>
    [TestMethod]
    public void Convert_WithMentionInsideBoldListAndTable_WritesMentionNodes()
    {
        // Arrange
        const string markdown = """
            **Owner: @[Jane](accountid:a)**

            - @[Jane](accountid:a)

            | Who |
            | --- |
            | @[Jane](accountid:a) |
            """;

        // Act
        string json = MarkdownToAdf.Convert(markdown).ToJsonString();

        // Assert
        Assert.AreEqual(3, json.Split("\"type\":\"mention\"").Length - 1);
        StringAssert.Contains(json, """{"type":"mention","attrs":{"id":"a","text":"@Jane"}}""", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that mention syntax inside a code span or a code block stays literal text.
    /// </summary>
    [TestMethod]
    public void Convert_WithMentionInsideCode_KeepsItLiteral()
    {
        // Arrange
        const string markdown = """
            Use `@[Jane]` to mention.

            ```
            @[Jane]
            ```
            """;

        // Act
        string json = MarkdownToAdf.Convert(markdown).ToJsonString();

        // Assert
        Assert.IsFalse(json.Contains("\"mention\"", StringComparison.Ordinal));
        Assert.HasCount(0, MarkdownToAdf.FindMentionNames(markdown));
    }

    /// <summary>
    /// Verifies that a bare @name, an @ after a word character, and a plain link are not mentions.
    /// </summary>
    [TestMethod]
    public void Convert_WithTextThatIsNotAMention_LeavesItAlone()
    {
        // Act
        JsonElement paragraph = AdfNodes.Blocks("@Jane mail user@[host] or [site](https://example.com)")[0];

        // Assert
        Assert.IsFalse(paragraph.GetProperty("content").EnumerateArray().Any(node => AdfNodes.TypeOf(node) == "mention"));
        Assert.AreEqual("@Jane mail user@[host] or site", AdfNodes.TextOf(paragraph));
    }

    /// <summary>
    /// Verifies that only the names of mentions without an account ID are returned, once each, in order.
    /// </summary>
    [TestMethod]
    public void FindMentionNames_WithSeveralMentions_ReturnsDistinctNamesToResolve()
    {
        // Act
        IReadOnlyList<string> names = MarkdownToAdf.FindMentionNames("@[Bob] @[Jane Doe](accountid:x) @[ Alice ] @[Bob]");

        // Assert
        CollectionAssert.AreEqual(new[] { "Bob", "Alice" }, names.ToArray());
    }

    /// <summary>
    /// Verifies that the account IDs of mentions in rich-text fields are taken from the resolved names.
    /// </summary>
    [TestMethod]
    public void ConvertRichTextFields_WithMention_UsesTheResolvedAccountId()
    {
        // Arrange
        var fields = new JsonObject { ["customfield_10050"] = "Ask @[Jane]" };

        // Act
        MarkdownToAdf.ConvertRichTextFields(fields, _ => true, AdfReferences.ForMentions(new Dictionary<string, string> { ["Jane"] = "abc" }));

        // Assert
        Assert.AreEqual("abc", fields["customfield_10050"]!["content"]![0]!["content"]![1]!["attrs"]!["id"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that images and videos become a mediaSingle each, that consecutive other files share
    /// one mediaGroup, and that the text around them stays in paragraphs, as Jira's editor writes them.
    /// </summary>
    [TestMethod]
    public void Convert_WithEmbeddedAttachments_WritesMediaBlocksLikeJira()
    {
        // Arrange
        const string markdown = """
            Logs and a screenshot:

            ![screenshot](attachment:101)
            ![](attachment:102)
            ![logs.zip](attachment:103) ![trace.zip](attachment:104)
            ![](attachment:105)

            Thanks.
            """;

        // Act
        JsonObject document = MarkdownToAdf.Convert(markdown, Attachments());

        // Assert
        JsonArray blocks = document["content"]!.AsArray();
        CollectionAssert.AreEqual(
            new[] { "paragraph", "mediaSingle", "mediaSingle", "mediaGroup", "mediaSingle", "paragraph" },
            blocks.Select(block => block!["type"]!.GetValue<string>()).ToArray());
        Assert.AreEqual(
            """{"type":"mediaSingle","attrs":{"layout":"align-start"},"content":[{"type":"media","attrs":{"type":"file","id":"00000000-0000-0000-0000-000000000101","collection":"","alt":"screenshot"}}]}""",
            blocks[1]!.ToJsonString());
        Assert.AreEqual("photo.jpg", blocks[2]!["content"]![0]!["attrs"]!["alt"]!.GetValue<string>());
        Assert.AreEqual(
            """{"type":"mediaGroup","content":[{"type":"media","attrs":{"type":"file","id":"00000000-0000-0000-0000-000000000103","collection":""}},{"type":"media","attrs":{"type":"file","id":"00000000-0000-0000-0000-000000000104","collection":""}}]}""",
            blocks[3]!.ToJsonString());
        Assert.AreEqual("demo.mp4", blocks[4]!["content"]![0]!["attrs"]!["alt"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that an attachment in the middle of a paragraph splits it, without leaving the line
    /// break that separated the text from the attachment.
    /// </summary>
    [TestMethod]
    public void Convert_WithAttachmentInsideParagraph_SplitsTheParagraph()
    {
        // Act
        JsonObject document = MarkdownToAdf.Convert("See the logs:  \n![logs.zip](attachment:103)  \nfor details.", Attachments());

        // Assert
        JsonArray blocks = document["content"]!.AsArray();
        Assert.HasCount(3, blocks);
        Assert.AreEqual("""{"type":"paragraph","content":[{"type":"text","text":"See the logs:"}]}""", blocks[0]!.ToJsonString());
        Assert.AreEqual("mediaGroup", blocks[1]!["type"]!.GetValue<string>());
        Assert.AreEqual("""{"type":"paragraph","content":[{"type":"text","text":"for details."}]}""", blocks[2]!.ToJsonString());
    }

    /// <summary>
    /// Verifies that an attachment where ADF cannot hold a media block is refused with an explanation.
    /// </summary>
    /// <param name="markdown">The Markdown.</param>
    /// <param name="place">The place the message names.</param>
    [TestMethod]
    [DataRow("# Title ![a](attachment:101)", "a heading")]
    [DataRow("- ![a](attachment:101)", "a list item")]
    [DataRow("| A |\n| --- |\n| ![a](attachment:101) |", "a table cell")]
    [DataRow("> ![a](attachment:101)", "a block quote")]
    public void Convert_WithAttachmentOutsideAParagraph_ThrowsMcpException(string markdown, string place)
    {
        // Act
        McpException exception = Assert.ThrowsExactly<McpException>(() => MarkdownToAdf.Convert(markdown, Attachments()));

        // Assert
        StringAssert.Contains(exception.Message, $"is inside {place}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that an attachment that was not resolved is refused rather than posted without its media.
    /// </summary>
    [TestMethod]
    public void Convert_WithUnresolvedAttachment_ThrowsInvalidOperationException()
    {
        // Act and assert
        Assert.ThrowsExactly<InvalidOperationException>(() => MarkdownToAdf.Convert("![a](attachment:999)", Attachments()));
    }

    /// <summary>
    /// Verifies that only embedded attachments outside code are returned, once each, in order, and
    /// that ordinary images and links are not attachments.
    /// </summary>
    [TestMethod]
    public void FindAttachmentIds_WithSeveralReferences_ReturnsDistinctIdsOutsideCode()
    {
        // Arrange
        const string markdown = """
            ![b](attachment:2) ![a](attachment:1)

            `![c](attachment:3)` and [link](https://example.com/x.png) and ![d](https://example.com/d.png)

            ```
            ![e](attachment:4)
            ```

            ![b](attachment:2)
            """;

        // Act
        IReadOnlyList<string> ids = MarkdownToAdf.FindAttachmentIds(markdown);

        // Assert
        CollectionAssert.AreEqual(new[] { "2", "1" }, ids.ToArray());
    }

    /// <summary>
    /// Returns resolved attachments for the tests: a PNG, a JPEG that Jira recorded as
    /// binary/octet-stream, two zip files, and a video.
    /// </summary>
    private static AdfReferences Attachments()
        => new(
            new Dictionary<string, string>(),
            new Dictionary<string, EmbeddedAttachment>
            {
                ["101"] = new("00000000-0000-0000-0000-000000000101", "screenshot.png", "image/png"),
                ["102"] = new("00000000-0000-0000-0000-000000000102", "photo.jpg", "binary/octet-stream"),
                ["103"] = new("00000000-0000-0000-0000-000000000103", "logs.zip", "application/zip"),
                ["104"] = new("00000000-0000-0000-0000-000000000104", "trace.zip", "application/zip"),
                ["105"] = new("00000000-0000-0000-0000-000000000105", "demo.mp4", "video/mp4"),
            });
}
