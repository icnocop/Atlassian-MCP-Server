// <copyright file="ToolRegistration.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Configuration;

/// <summary>
/// Registers the tools that the options enable.
/// </summary>
public static class ToolRegistration
{
    /// <summary>
    /// Finds every tool method in the assembly.
    /// </summary>
    /// <returns>The tools, ordered by name.</returns>
    public static IReadOnlyList<ToolDescriptor> Discover()
    {
        var tools = new List<ToolDescriptor>();

        foreach (Type type in typeof(ToolRegistration).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
            {
                continue;
            }

            string? typeToolset = type.GetCustomAttribute<ToolsetAttribute>()?.Name;

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                McpServerToolAttribute? tool = method.GetCustomAttribute<McpServerToolAttribute>();
                if (tool is null)
                {
                    continue;
                }

                string toolset = method.GetCustomAttribute<ToolsetAttribute>()?.Name
                    ?? typeToolset
                    ?? throw new InvalidOperationException($"The tool {type.Name}.{method.Name} has no toolset.");

                string name = tool.Name ?? throw new InvalidOperationException($"The tool {type.Name}.{method.Name} has no name.");

                tools.Add(new ToolDescriptor(name, toolset, tool.ReadOnly, method));
            }
        }

        return tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Selects the tools that the options enable.
    /// </summary>
    /// <param name="tools">Every tool.</param>
    /// <param name="options">The options.</param>
    /// <returns>The enabled tools.</returns>
    public static IReadOnlyList<ToolDescriptor> Select(IEnumerable<ToolDescriptor> tools, AtlassianOptions options)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(options);

        return tools
            .Where(tool => options.Toolsets.Contains(tool.Toolset))
            .Where(tool => options.EnabledTools.Count == 0 || options.EnabledTools.Contains(tool.Name))
            .Where(tool => !options.ReadOnly || tool.ReadOnly)
            .ToList();
    }

    /// <summary>
    /// Adds the tools that the options enable to the MCP server.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="options">The options.</param>
    /// <returns>The builder.</returns>
    public static IMcpServerBuilder WithAtlassianTools(this IMcpServerBuilder builder, AtlassianOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (ToolDescriptor tool in Select(Discover(), options))
        {
            MethodInfo method = tool.Method;
            Type type = method.DeclaringType!;

            builder.Services.AddSingleton(services => method.IsStatic
                ? McpServerTool.Create(method, target: null, new McpServerToolCreateOptions { Services = services })
                : McpServerTool.Create(
                    method,
                    context => ActivatorUtilities.CreateInstance(context.Services ?? services, type),
                    new McpServerToolCreateOptions { Services = services }));
        }

        return builder;
    }
}
