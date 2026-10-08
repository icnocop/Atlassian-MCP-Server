// <copyright file="ToolRegistrationTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Reflection;
using Atlassian.Mcp.Server.Configuration;

namespace Atlassian.Mcp.Server.Tests.Configuration;

/// <summary>
/// Tests for <see cref="ToolRegistration"/>.
/// </summary>
[TestClass]
public sealed class ToolRegistrationTests
{
    /// <summary>
    /// Verifies that every tool is found, with a unique, prefixed name and a known toolset.
    /// </summary>
    [TestMethod]
    public void Discover_FindsEveryToolWithUniquePrefixedNameAndKnownToolset()
    {
        // Act
        IReadOnlyList<ToolDescriptor> tools = ToolRegistration.Discover();

        // Assert
        Assert.IsTrue(tools.Count > 130, $"Found {tools.Count} tools.");
        Assert.AreEqual(tools.Count, tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());
        foreach (ToolDescriptor tool in tools)
        {
            Assert.IsTrue(
                tool.Name.StartsWith("atlassian_jira_", StringComparison.Ordinal) || tool.Name.StartsWith("atlassian_confluence_", StringComparison.Ordinal),
                tool.Name);
            Assert.IsTrue(Toolsets.Known.Contains(tool.Toolset), $"{tool.Name}: {tool.Toolset}");
        }
    }

    /// <summary>
    /// Verifies that every tool and every one of its parameters has a description for the model.
    /// </summary>
    [TestMethod]
    public void Discover_EveryToolAndParameter_HasDescription()
    {
        // Act
        IReadOnlyList<ToolDescriptor> tools = ToolRegistration.Discover();

        // Assert
        foreach (ToolDescriptor tool in tools)
        {
            Assert.IsNotNull(tool.Method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>(), tool.Name);
            foreach (ParameterInfo parameter in tool.Method.GetParameters().Where(parameter => parameter.ParameterType != typeof(CancellationToken)))
            {
                Assert.IsNotNull(parameter.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>(), $"{tool.Name}.{parameter.Name}");
            }
        }
    }

    /// <summary>
    /// Verifies that only tools in the enabled toolsets are selected.
    /// </summary>
    [TestMethod]
    public void Select_WithToolsets_KeepsOnlyThoseToolsets()
    {
        // Act
        IReadOnlyList<ToolDescriptor> selected = ToolRegistration.Select(ToolRegistration.Discover(), Options([Toolsets.JiraIssues]));

        // Assert
        Assert.IsTrue(selected.Count > 0);
        Assert.IsTrue(selected.All(tool => tool.Toolset == Toolsets.JiraIssues));
    }

    /// <summary>
    /// Verifies that an allow list of tool names is honored.
    /// </summary>
    [TestMethod]
    public void Select_WithEnabledTools_KeepsOnlyThoseTools()
    {
        // Act
        IReadOnlyList<ToolDescriptor> selected = ToolRegistration.Select(
            ToolRegistration.Discover(),
            Options(Toolsets.Known, enabledTools: ["atlassian_jira_get_issue"]));

        // Assert
        Assert.AreEqual(1, selected.Count);
        Assert.AreEqual("atlassian_jira_get_issue", selected[0].Name);
    }

    /// <summary>
    /// Verifies that read-only mode removes every tool that changes data.
    /// </summary>
    [TestMethod]
    public void Select_WithReadOnly_KeepsOnlyReadOnlyTools()
    {
        // Act
        IReadOnlyList<ToolDescriptor> selected = ToolRegistration.Select(ToolRegistration.Discover(), Options(Toolsets.Known, readOnly: true));

        // Assert
        Assert.IsTrue(selected.Count > 0);
        Assert.IsTrue(selected.All(tool => tool.ReadOnly));
        Assert.IsFalse(selected.Any(tool => tool.Name == "atlassian_jira_create_issue"));
    }

    /// <summary>
    /// Verifies that deleting Confluence pages is only available when its opt-in toolset is enabled.
    /// </summary>
    [TestMethod]
    public void Select_ConfluenceDeletePage_OnlyWithConfluenceAdmin()
    {
        // Arrange
        IReadOnlyList<ToolDescriptor> tools = ToolRegistration.Discover();
        HashSet<string> defaults = Toolsets.Parse(null, []);

        // Act
        IReadOnlyList<ToolDescriptor> byDefault = ToolRegistration.Select(tools, Options(defaults));
        IReadOnlyList<ToolDescriptor> withAdmin = ToolRegistration.Select(tools, Options(Toolsets.Known));

        // Assert
        Assert.IsFalse(byDefault.Any(tool => tool.Name == "atlassian_confluence_delete_page"));
        Assert.IsTrue(withAdmin.Any(tool => tool.Name == "atlassian_confluence_delete_page"));
    }

    private static AtlassianOptions Options(IEnumerable<string> toolsets, IEnumerable<string>? enabledTools = null, bool readOnly = false)
        => new(
            new Uri("https://example.atlassian.net/"),
            "user@example.com",
            "token",
            toolsets.ToHashSet(StringComparer.OrdinalIgnoreCase),
            (enabledTools ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase),
            readOnly);
}
