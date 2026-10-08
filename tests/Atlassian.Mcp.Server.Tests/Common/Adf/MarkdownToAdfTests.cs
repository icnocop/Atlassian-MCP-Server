// <copyright file="MarkdownToAdfTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;

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
}
