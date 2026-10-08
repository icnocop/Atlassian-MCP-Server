// <copyright file="ServerLog.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Atlassian.Mcp.Server.Common;

/// <summary>
/// The log messages of the server host.
/// </summary>
internal static partial class ServerLog
{
    /// <summary>
    /// Logs that the server is starting.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="server">The server name.</param>
    /// <param name="version">The server version.</param>
    /// <param name="site">The Atlassian site URL.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "{Server} {Version} is starting for {Site}.")]
    public static partial void Starting(ILogger logger, string server, string version, Uri site);

    /// <summary>
    /// Logs an exception that no code handled.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception.</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Critical, Message = "An unhandled exception occurred.")]
    public static partial void UnhandledException(ILogger logger, Exception? exception);

    /// <summary>
    /// Logs that the server stopped because of an exception.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception.</param>
    [LoggerMessage(EventId = 3, Level = LogLevel.Critical, Message = "The server stopped because of an unexpected error.")]
    public static partial void Stopped(ILogger logger, Exception exception);
}
