// <copyright file="ToolDescriptor.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Reflection;

namespace Atlassian.Mcp.Server.Configuration;

/// <summary>
/// Describes one tool method found in the assembly.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="Toolset">The toolset that the tool belongs to.</param>
/// <param name="ReadOnly">A value indicating whether the tool only reads data.</param>
/// <param name="Method">The method that implements the tool.</param>
public sealed record ToolDescriptor(string Name, string Toolset, bool ReadOnly, MethodInfo Method);
