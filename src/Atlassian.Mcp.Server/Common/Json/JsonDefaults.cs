// <copyright file="JsonDefaults.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlassian.Mcp.Server.Common.Json;

/// <summary>
/// The JSON settings for request bodies and tool results.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Gets the options: camel-case names, and <see langword="null"/> properties left out, so a
    /// request only sends what the caller set.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Gets the options for tool results: the same as <see cref="Options"/>, without indentation,
    /// and without escaping characters such as quotes, ampersands, and non-ASCII letters, which
    /// only matter when JSON is embedded in HTML. Both keep results small and readable.
    /// </summary>
    public static JsonSerializerOptions Results { get; } = new(Options)
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
