// <copyright file="ToolsetAttribute.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Configuration;

/// <summary>
/// Assigns the tools of a class, or a single tool method, to a toolset. A method attribute
/// overrides the class attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolsetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolsetAttribute"/> class.
    /// </summary>
    /// <param name="name">The toolset name, one of the constants in <see cref="Toolsets"/>.</param>
    public ToolsetAttribute(string name)
    {
        this.Name = name;
    }

    /// <summary>Gets the toolset name.</summary>
    public string Name { get; }
}
