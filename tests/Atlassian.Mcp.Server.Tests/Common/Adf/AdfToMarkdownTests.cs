// <copyright file="AdfToMarkdownTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Adf;

namespace Atlassian.Mcp.Server.Tests.Common.Adf;

/// <summary>
/// Tests for <see cref="AdfToMarkdown"/>.
/// </summary>
[TestClass]
public sealed class AdfToMarkdownTests
{
    /// <summary>
    /// Verifies that paragraphs, inline marks, and links are converted.
    /// </summary>
    [TestMethod]
    public void Convert_WithParagraphsAndMarks_WritesMarkdownSpans()
    {
        // Arrange
        JsonNode document = Doc("""
            {"type":"paragraph","content":[
              {"type":"text","text":"Plain "},
              {"type":"text","text":"bold","marks":[{"type":"strong"}]},
              {"type":"text","text":" "},
              {"type":"text","text":"it","marks":[{"type":"em"}]},
              {"type":"text","text":" "},
              {"type":"text","text":"old","marks":[{"type":"strike"}]},
              {"type":"text","text":" "},
              {"type":"text","text":"x()","marks":[{"type":"code"}]},
              {"type":"text","text":" "},
              {"type":"text","text":"site","marks":[{"type":"link","attrs":{"href":"https://example.com"}}]}
            ]},
            {"type":"paragraph","content":[{"type":"text","text":"Second"}]}
            """);

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("Plain **bold** *it* ~~old~~ `x()` [site](https://example.com)\n\nSecond", markdown);
    }

    /// <summary>
    /// Verifies that headings are written with their level.
    /// </summary>
    [TestMethod]
    public void Convert_WithHeading_WritesHashesForTheLevel()
    {
        // Arrange
        JsonNode document = Doc("""{"type":"heading","attrs":{"level":3},"content":[{"type":"text","text":"Title"}]}""");

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("### Title", markdown);
    }

    /// <summary>
    /// Verifies that nested bulleted and numbered lists are indented.
    /// </summary>
    [TestMethod]
    public void Convert_WithNestedLists_IndentsTheChildList()
    {
        // Arrange
        JsonNode document = Doc("""
            {"type":"bulletList","content":[
              {"type":"listItem","content":[
                {"type":"paragraph","content":[{"type":"text","text":"a"}]},
                {"type":"orderedList","attrs":{"order":1},"content":[
                  {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"one"}]}]},
                  {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"two"}]}]}
                ]}
              ]},
              {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"b"}]}]}
            ]}
            """);

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("- a\n  1. one\n  2. two\n- b", markdown);
    }

    /// <summary>
    /// Verifies that a code block keeps its language and line breaks.
    /// </summary>
    [TestMethod]
    public void Convert_WithCodeBlock_WritesFenceWithLanguage()
    {
        // Arrange
        JsonNode document = Doc("""{"type":"codeBlock","attrs":{"language":"csharp"},"content":[{"type":"text","text":"var x = 1;\nvar y = 2;"}]}""");

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("```csharp\nvar x = 1;\nvar y = 2;\n```", markdown);
    }

    /// <summary>
    /// Verifies that a table is written as a GitHub-style table.
    /// </summary>
    [TestMethod]
    public void Convert_WithTable_WritesGitHubTable()
    {
        // Arrange
        JsonNode document = MarkdownToAdf.Convert("| A | B |\n| --- | --- |\n| 1 | 2 |");

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("| A | B |\n| --- | --- |\n| 1 | 2 |", markdown);
    }

    /// <summary>
    /// Verifies that block quotes and panels are written as quoted lines.
    /// </summary>
    [TestMethod]
    public void Convert_WithBlockquoteAndPanel_WritesQuotedLines()
    {
        // Arrange
        JsonNode document = Doc("""
            {"type":"blockquote","content":[{"type":"paragraph","content":[{"type":"text","text":"quoted"}]}]},
            {"type":"panel","attrs":{"panelType":"warning"},"content":[{"type":"paragraph","content":[{"type":"text","text":"careful"}]}]}
            """);

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        StringAssert.StartsWith(markdown, "> quoted", StringComparison.Ordinal);
        StringAssert.Contains(markdown, "> **Warning:**", StringComparison.Ordinal);
        StringAssert.Contains(markdown, "> careful", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that mentions, emoji, dates, status lozenges, and inline cards are written as text.
    /// </summary>
    [TestMethod]
    public void Convert_WithInlineNodes_WritesReadableText()
    {
        // Arrange
        JsonNode document = Doc("""
            {"type":"paragraph","content":[
              {"type":"mention","attrs":{"id":"abc","text":"@Jane"}},
              {"type":"text","text":" "},
              {"type":"emoji","attrs":{"shortName":":smile:"}},
              {"type":"text","text":" "},
              {"type":"date","attrs":{"timestamp":"1700000000000"}},
              {"type":"text","text":" "},
              {"type":"status","attrs":{"text":"DONE","color":"green"}},
              {"type":"text","text":" "},
              {"type":"inlineCard","attrs":{"url":"https://example.com/x"}}
            ]}
            """);

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("@[Jane](accountid:abc) :smile: 2023-11-14 [DONE] [https://example.com/x](https://example.com/x)", markdown);
    }

    /// <summary>
    /// Verifies that a mention without an account ID, which cannot be written back, is written as plain text.
    /// </summary>
    [TestMethod]
    public void Convert_WithMentionWithoutAccountId_WritesPlainText()
    {
        // Arrange
        JsonNode document = Doc("""{"type":"paragraph","content":[{"type":"mention","attrs":{"text":"@Jane"}}]}""");

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("@Jane", markdown);
    }

    /// <summary>
    /// Verifies that a mention without text is written with its account ID as the name, and that a
    /// closing bracket in the name, which would end it early, is dropped.
    /// </summary>
    [TestMethod]
    public void Convert_WithMentionWithoutTextOrWithBracket_WritesReadableMention()
    {
        // Arrange
        JsonNode document = Doc("""
            {"type":"paragraph","content":[
              {"type":"mention","attrs":{"id":"abc"}},
              {"type":"text","text":" "},
              {"type":"mention","attrs":{"id":"def","text":"@Jane [Ops]"}}
            ]}
            """);

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        Assert.AreEqual("@[abc](accountid:abc) @[Jane [Ops](accountid:def)", markdown);
    }

    /// <summary>
    /// Verifies that a mention converted to Markdown and back is the same mention.
    /// </summary>
    [TestMethod]
    public void Convert_WithMentionRoundTrip_KeepsTheAccountId()
    {
        // Arrange
        JsonNode document = Doc("""{"type":"paragraph","content":[{"type":"text","text":"Hi "},{"type":"mention","attrs":{"id":"5b10ac8d82e05b22cc7d4ef5","text":"@Jane Doe"}}]}""");

        // Act
        JsonObject roundTrip = MarkdownToAdf.Convert(AdfToMarkdown.Convert(document));

        // Assert
        Assert.AreEqual(document.ToJsonString(), roundTrip.ToJsonString());
    }

    /// <summary>
    /// Verifies that a macro is written as a placeholder that names it and its parameters.
    /// </summary>
    [TestMethod]
    public void Convert_WithExtension_WritesPlaceholderComment()
    {
        // Arrange
        JsonNode document = Doc("""
            {"type":"extension","attrs":{"extensionKey":"jira","extensionType":"com.atlassian.confluence.macro.core",
              "parameters":{"macroParams":{"key":{"value":"PROJ-1"}}}}}
            """);

        // Act
        string markdown = AdfToMarkdown.Convert(document);

        // Assert
        StringAssert.StartsWith(markdown, "<!-- adf:extension extensionKey=jira", StringComparison.Ordinal);
        StringAssert.Contains(markdown, "key=PROJ-1", StringComparison.Ordinal);
        StringAssert.EndsWith(markdown, "-->", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that only ADF documents inside a response are replaced.
    /// </summary>
    [TestMethod]
    public void ConvertDocuments_WithNestedDocuments_ReplacesOnlyTheDocuments()
    {
        // Arrange
        JsonNode response = JsonNode.Parse("""
            {"key":"PROJ-1","fields":{
              "summary":"Plain",
              "description":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"Hello"}]}]},
              "priority":{"type":"high","name":"High"},
              "comment":{"comments":[{"body":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"Hi"}]}]}}]}
            }}
            """)!;

        // Act
        JsonNode converted = AdfToMarkdown.ConvertDocuments(response)!;

        // Assert
        Assert.AreEqual("Hello", converted["fields"]!["description"]!.GetValue<string>());
        Assert.AreEqual("Hi", converted["fields"]!["comment"]!["comments"]![0]!["body"]!.GetValue<string>());
        Assert.AreEqual("Plain", converted["fields"]!["summary"]!.GetValue<string>());
        Assert.AreEqual("High", converted["fields"]!["priority"]!["name"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies that Markdown converted to ADF, back to Markdown, and to ADF again gives the same ADF.
    /// </summary>
    [TestMethod]
    public void Convert_WithMarkdownRoundTrip_ProducesIdenticalAdf()
    {
        // Arrange
        const string markdown = """
            # Title

            Some **bold**, *italic*, `code`, and a [link](https://example.com).

            ## Lists

            - a
              - b
            - c

            1. one
            2. two

            ```cs
            var x = 1;
            ```

            | A | B |
            | --- | --- |
            | 1 | 2 |
            """;

        JsonObject first = MarkdownToAdf.Convert(markdown);

        // Act
        JsonObject second = MarkdownToAdf.Convert(AdfToMarkdown.Convert(first));

        // Assert
        Assert.AreEqual(first.ToJsonString(), second.ToJsonString());
    }

    /// <summary>
    /// Verifies that media nodes with a known attachment are written as ![name](attachment:ID):
    /// an image by its alternative text, and the files of a group, by file name, in one paragraph;
    /// and that a media node without a known attachment stays a placeholder.
    /// </summary>
    [TestMethod]
    public void Convert_WithKnownAttachments_WritesAttachmentReferences()
    {
        // Arrange
        JsonNode document = Doc("""
            {"type":"mediaSingle","attrs":{"layout":"align-start"},"content":[
              {"type":"media","attrs":{"type":"file","id":"m-image","alt":"screenshot","collection":""}}]},
            {"type":"mediaGroup","content":[
              {"type":"media","attrs":{"type":"file","id":"m-logs","collection":""}},
              {"type":"media","attrs":{"type":"file","id":"m-trace","collection":""}}]},
            {"type":"mediaSingle","attrs":{"layout":"center"},"content":[
              {"type":"media","attrs":{"type":"file","id":"m-unknown","collection":""}}]}
            """);
        var attachments = new Dictionary<string, AttachmentReference>
        {
            ["m-image"] = new("101", "screenshot.png"),
            ["m-logs"] = new("103", "logs.zip"),
            ["m-trace"] = new("104", "trace [old].zip"),
        };

        // Act
        string markdown = AdfToMarkdown.Convert(document, attachments);

        // Assert
        Assert.AreEqual(
            "![screenshot](attachment:101)\n\n![logs.zip](attachment:103)\n![trace [old.zip](attachment:104)\n\n<!-- adf:media id=m-unknown type=file -->",
            markdown);
    }

    /// <summary>
    /// Verifies that embedded attachments written as Markdown convert back into the same media blocks.
    /// </summary>
    [TestMethod]
    public void Convert_WithAttachmentRoundTrip_ProducesIdenticalAdf()
    {
        // Arrange
        var embedded = new AdfReferences(
            new Dictionary<string, string>(),
            new Dictionary<string, EmbeddedAttachment>
            {
                ["101"] = new("m-image", "screenshot.png", "image/png"),
                ["103"] = new("m-logs", "logs.zip", "application/zip"),
                ["104"] = new("m-trace", "trace.zip", "application/zip"),
            });
        var read = new Dictionary<string, AttachmentReference>
        {
            ["m-image"] = new("101", "screenshot.png"),
            ["m-logs"] = new("103", "logs.zip"),
            ["m-trace"] = new("104", "trace.zip"),
        };
        JsonObject first = MarkdownToAdf.Convert("Before\n\n![screenshot](attachment:101)\n\n![logs.zip](attachment:103)\n![trace.zip](attachment:104)\n\nAfter", embedded);

        // Act
        JsonObject second = MarkdownToAdf.Convert(AdfToMarkdown.Convert(first, read), embedded);

        // Assert
        Assert.AreEqual(first.ToJsonString(), second.ToJsonString());
    }

    /// <summary>
    /// Verifies that converting the documents of a response with known attachments leaves the
    /// response itself without the annotations used to write them.
    /// </summary>
    [TestMethod]
    public void ConvertDocuments_WithKnownAttachments_DoesNotChangeTheResponse()
    {
        // Arrange
        JsonNode response = JsonNode.Parse("""
            {"body":{"type":"doc","version":1,"content":[{"type":"mediaGroup","content":[{"type":"media","attrs":{"type":"file","id":"m-logs","collection":""}}]}]}}
            """)!;
        string before = response.ToJsonString();

        // Act
        JsonNode converted = AdfToMarkdown.ConvertDocuments(response, new Dictionary<string, AttachmentReference> { ["m-logs"] = new("103", "logs.zip") })!;

        // Assert
        Assert.AreEqual("![logs.zip](attachment:103)", converted["body"]!.GetValue<string>());
        Assert.AreEqual(before, response.ToJsonString());
    }

    private static JsonNode Doc(string blocks)
        => JsonNode.Parse($$"""{"type":"doc","version":1,"content":[{{blocks}}]}""")!;
}
