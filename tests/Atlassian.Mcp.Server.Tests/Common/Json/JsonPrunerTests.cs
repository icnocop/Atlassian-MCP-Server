// <copyright file="JsonPrunerTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Json;

namespace Atlassian.Mcp.Server.Tests.Common.Json;

/// <summary>
/// Tests for <see cref="JsonPruner"/>.
/// </summary>
[TestClass]
public sealed class JsonPrunerTests
{
    /// <summary>
    /// Verifies that nulls, empty containers, and navigation links are removed at every level,
    /// while false and zero are kept.
    /// </summary>
    [TestMethod]
    public void Prune_WithNoise_RemovesItRecursivelyAndKeepsFalseAndZero()
    {
        // Arrange
        JsonNode node = JsonNode.Parse("""
            {"a":null,"b":{},"c":[],"self":"https://x","avatarUrls":{"16x16":"y"},
             "d":{"_links":{"next":"z"},"expand":"names","e":false,"f":0,"g":{"h":null},"i":[null,{"self":"s"},1]}}
            """)!;

        // Act
        JsonNode? pruned = JsonPruner.Prune(node);

        // Assert
        Assert.AreEqual("""{"d":{"e":false,"f":0,"i":[1]}}""", pruned!.ToJsonString());
    }

    /// <summary>
    /// Verifies that nothing is left of a node that holds only noise.
    /// </summary>
    [TestMethod]
    public void Prune_WithOnlyNoise_ReturnsNull()
    {
        // Act
        JsonNode? pruned = JsonPruner.Prune(JsonNode.Parse("""{"self":"x","a":[]}"""));

        // Assert
        Assert.IsNull(pruned);
    }
}
