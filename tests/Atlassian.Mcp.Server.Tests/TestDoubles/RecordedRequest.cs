// <copyright file="RecordedRequest.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;

namespace Atlassian.Mcp.Server.Tests.TestDoubles;

/// <summary>
/// A request recorded by <see cref="RecordingHttpClient"/>.
/// </summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path relative to the site root, including the query string.</param>
/// <param name="Body">The JSON body, or <see langword="null"/>.</param>
/// <param name="FileName">The uploaded file name, for uploads.</param>
/// <param name="Content">The uploaded content, for uploads.</param>
/// <param name="FormFields">The extra form fields, for uploads.</param>
internal sealed record RecordedRequest(
    HttpMethod Method,
    string Path,
    JsonNode? Body,
    string? FileName,
    byte[]? Content,
    IReadOnlyDictionary<string, string>? FormFields);
