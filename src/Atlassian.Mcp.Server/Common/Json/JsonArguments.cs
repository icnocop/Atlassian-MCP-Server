// <copyright file="JsonArguments.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Common.Json;

/// <summary>
/// Parses tool arguments that arrive as strings: JSON objects and comma-separated lists.
/// </summary>
public static class JsonArguments
{
    /// <summary>
    /// Parses a JSON object argument.
    /// </summary>
    /// <param name="value">The JSON text, or <see langword="null"/> or empty for none.</param>
    /// <param name="parameterName">The parameter name, for the error message.</param>
    /// <returns>The object, or <see langword="null"/> when <paramref name="value"/> is empty.</returns>
    /// <exception cref="McpException">The value is not a JSON object.</exception>
    public static JsonObject? ParseObject(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value) as JsonObject
                ?? throw new McpException($"The {parameterName} parameter must be a JSON object, such as {{\"customfield_10010\": \"value\"}}.");
        }
        catch (JsonException exception)
        {
            throw new McpException($"The {parameterName} parameter is not valid JSON: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Parses a JSON value argument of any kind.
    /// </summary>
    /// <param name="value">The JSON text.</param>
    /// <param name="parameterName">The parameter name, for the error message.</param>
    /// <returns>The parsed node.</returns>
    /// <exception cref="McpException">The value is not valid JSON.</exception>
    public static JsonNode? ParseNode(string value, string parameterName)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException exception)
        {
            throw new McpException($"The {parameterName} parameter is not valid JSON: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Splits a comma-separated list, trimming entries and dropping blank ones.
    /// </summary>
    /// <param name="value">The list, or <see langword="null"/>.</param>
    /// <returns>The entries.</returns>
    public static List<string> SplitList(string? value)
        => (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
