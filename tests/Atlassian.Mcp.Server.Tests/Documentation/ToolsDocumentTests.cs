// <copyright file="ToolsDocumentTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Atlassian.Mcp.Server.Configuration;

namespace Atlassian.Mcp.Server.Tests.Documentation;

/// <summary>
/// Tests that keep the generated tool reference in step with the code.
/// </summary>
[TestClass]
public sealed class ToolsDocumentTests
{
    /// <summary>
    /// Verifies that docs/tools.md lists every tool. Run build/Update-ToolsDoc.ps1 to fix a failure.
    /// </summary>
    [TestMethod]
    public void ToolsDocument_ListsEveryTool()
    {
        // Arrange
        string document = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "tools.md"));

        // Act
        List<string> missing = ToolRegistration.Discover()
            .Select(tool => tool.Name)
            .Where(name => !document.Contains($"`{name}`", StringComparison.Ordinal))
            .ToList();

        // Assert
        Assert.AreEqual(0, missing.Count, $"docs/tools.md is missing {string.Join(", ", missing)}. Run build/Update-ToolsDoc.ps1.");
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Atlassian.Mcp.Server.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
