// <copyright file="AtlassianApiException.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Net;
using ModelContextProtocol;

namespace Atlassian.Mcp.Server.Common.Http;

/// <summary>
/// The exception thrown when an Atlassian REST API request fails. The MCP server returns its message
/// to the client as the tool result, so the message is written for the AI model to act on.
/// </summary>
public sealed class AtlassianApiException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AtlassianApiException"/> class.
    /// </summary>
    public AtlassianApiException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AtlassianApiException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public AtlassianApiException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AtlassianApiException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public AtlassianApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AtlassianApiException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="message">The message.</param>
    public AtlassianApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        this.StatusCode = statusCode;
    }

    /// <summary>Gets the HTTP status code of the response, when there was one.</summary>
    public HttpStatusCode? StatusCode { get; }
}
