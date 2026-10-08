// <copyright file="ToolResult.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common.Json;

namespace Atlassian.Mcp.Server.Common;

/// <summary>
/// Formats tool results as compact JSON text.
/// </summary>
public static class ToolResult
{
    /// <summary>
    /// Formats an API response, pruned of nulls and navigation links.
    /// </summary>
    /// <param name="node">The response.</param>
    /// <returns>The JSON text, or <c>{}</c> when nothing is left.</returns>
    public static string Json(JsonNode? node)
        => JsonPruner.Prune(node)?.ToJsonString(JsonDefaults.Results) ?? "{}";

    /// <summary>
    /// Formats an object built by the tool.
    /// </summary>
    /// <param name="value">The object.</param>
    /// <returns>The JSON text.</returns>
    public static string Json(object value)
        => Json(JsonSerializer.SerializeToNode(value, JsonDefaults.Options));

    /// <summary>
    /// Formats a confirmation for a request that returns no body.
    /// </summary>
    /// <param name="message">The confirmation, written as a complete sentence.</param>
    /// <returns>The JSON text.</returns>
    public static string Success(string message)
        => new JsonObject { ["success"] = true, ["message"] = message }.ToJsonString(JsonDefaults.Results);
}
