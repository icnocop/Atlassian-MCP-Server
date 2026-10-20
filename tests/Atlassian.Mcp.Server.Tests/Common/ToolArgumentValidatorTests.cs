// <copyright file="ToolArgumentValidatorTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using Atlassian.Mcp.Server.Common;

namespace Atlassian.Mcp.Server.Tests.Common;

/// <summary>
/// Tests for <see cref="ToolArgumentValidator"/>.
/// </summary>
[TestClass]
public sealed class ToolArgumentValidatorTests
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """{"type":"object","properties":{"pageId":{"type":"string"},"body":{"type":"string"},"publish":{"type":"boolean"}},"required":["pageId","body"]}""").RootElement;

    /// <summary>
    /// Verifies that arguments matching the parameters are accepted.
    /// </summary>
    [TestMethod]
    public void Validate_WithRequiredAndOptionalArguments_ReturnsNull()
    {
        // Act
        string? error = ToolArgumentValidator.Validate("tool", Schema, ["pageId", "body", "publish"]);

        // Assert
        Assert.IsNull(error);
    }

    /// <summary>
    /// Verifies that a wrongly named argument is reported both as unknown and, through the parameter
    /// it was meant for, as missing, with the tool's parameters listed.
    /// </summary>
    [TestMethod]
    public void Validate_WithWrongArgumentName_ReportsMissingAndUnknownAndListsParameters()
    {
        // Act
        string? error = ToolArgumentValidator.Validate("tool", Schema, ["pageId", "content"]);

        // Assert
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "missing the required parameter body", StringComparison.Ordinal);
        StringAssert.Contains(error, "no parameter named content", StringComparison.Ordinal);
        StringAssert.Contains(error, "pageId (required), body (required), publish", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a call with no arguments reports every required parameter.
    /// </summary>
    [TestMethod]
    public void Validate_WithNoArguments_ReportsEveryRequiredParameter()
    {
        // Act
        string? error = ToolArgumentValidator.Validate("tool", Schema, null);

        // Assert
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "missing the required parameters pageId, body", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a tool without parameters accepts a call without arguments.
    /// </summary>
    [TestMethod]
    public void Validate_WithEmptySchemaAndNoArguments_ReturnsNull()
    {
        // Arrange
        JsonElement schema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement;

        // Act
        string? error = ToolArgumentValidator.Validate("tool", schema, []);

        // Assert
        Assert.IsNull(error);
    }
}
