// <copyright file="ToolArgumentValidator.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;

namespace Atlassian.Mcp.Server.Common;

/// <summary>
/// Checks the argument names of a tool call against the tool's input schema. Without this, a
/// missing required argument fails with a generic "An error occurred invoking" message, and an
/// argument with a wrong name, such as content instead of body, is ignored silently.
/// </summary>
public static class ToolArgumentValidator
{
    /// <summary>
    /// Validates the argument names of a tool call.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="inputSchema">The JSON schema of the tool's arguments.</param>
    /// <param name="arguments">The arguments of the call, or <see langword="null"/> when there are none.</param>
    /// <returns>A message that names the problem and lists the tool's parameters, or <see langword="null"/> when the arguments are valid.</returns>
    public static string? Validate(string toolName, JsonElement inputSchema, IReadOnlyCollection<string>? arguments)
    {
        if (inputSchema.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parameters = new List<string>();
        if (inputSchema.TryGetProperty("properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object)
        {
            parameters.AddRange(properties.EnumerateObject().Select(property => property.Name));
        }

        var required = new List<string>();
        if (inputSchema.TryGetProperty("required", out JsonElement requiredNames) && requiredNames.ValueKind == JsonValueKind.Array)
        {
            required.AddRange(requiredNames.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!));
        }

        IReadOnlyCollection<string> given = arguments ?? [];
        List<string> unknown = given.Where(name => !parameters.Contains(name, StringComparer.Ordinal)).ToList();
        List<string> missing = required.Where(name => !given.Contains(name, StringComparer.Ordinal)).ToList();

        if (unknown.Count == 0 && missing.Count == 0)
        {
            return null;
        }

        var problems = new List<string>();
        if (missing.Count > 0)
        {
            problems.Add($"missing the required parameter{(missing.Count == 1 ? string.Empty : "s")} {string.Join(", ", missing)}");
        }

        if (unknown.Count > 0)
        {
            problems.Add($"the tool has no parameter{(unknown.Count == 1 ? string.Empty : "s")} named {string.Join(", ", unknown)}");
        }

        IEnumerable<string> described = parameters.Select(name => required.Contains(name, StringComparer.Ordinal) ? $"{name} (required)" : name);
        return $"The call to {toolName} was not run: {string.Join("; ", problems)}. Its parameters are: {string.Join(", ", described)}.";
    }
}
