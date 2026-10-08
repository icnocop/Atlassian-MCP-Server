// <copyright file="QueryStringTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Common.Http;

namespace Atlassian.Mcp.Server.Tests.Common.Http;

/// <summary>
/// Tests for <see cref="QueryString"/>.
/// </summary>
[TestClass]
public sealed class QueryStringTests
{
    /// <summary>
    /// Verifies that values are escaped and empty values are skipped.
    /// </summary>
    [TestMethod]
    public void ToString_WithMixedValues_EscapesAndSkipsEmpty()
    {
        // Arrange
        QueryString query = new QueryString("search")
            .Add("jql", "a = b")
            .Add("skipped", (string?)null)
            .Add("empty", string.Empty)
            .Add("n", 5)
            .Add("none", (int?)null)
            .Add("flag", true);

        // Act
        string result = query.ToString();

        // Assert
        Assert.AreEqual("search?jql=a%20%3D%20b&n=5&flag=true", result);
    }

    /// <summary>
    /// Verifies that a multi-valued parameter is repeated.
    /// </summary>
    [TestMethod]
    public void AddEach_WithValues_RepeatsTheParameter()
    {
        // Act
        string result = new QueryString("spaces").AddEach("keys", ["A", "B"]).ToString();

        // Assert
        Assert.AreEqual("spaces?keys=A&keys=B", result);
    }

    /// <summary>
    /// Verifies that parameters are appended to a path that already has a query string.
    /// </summary>
    [TestMethod]
    public void ToString_WithExistingQuery_AppendsWithAmpersand()
    {
        // Act
        string result = new QueryString("issue/X?fields=a").Add("expand", "b").ToString();

        // Assert
        Assert.AreEqual("issue/X?fields=a&expand=b", result);
    }

    /// <summary>
    /// Verifies that a path without parameters is returned unchanged.
    /// </summary>
    [TestMethod]
    public void ToString_WithNoParameters_ReturnsThePath()
    {
        // Act
        string result = new QueryString("myself").Add("x", (bool?)null).ToString();

        // Assert
        Assert.AreEqual("myself", result);
    }
}
