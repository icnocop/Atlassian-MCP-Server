// <copyright file="ServerInfo.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Reflection;

namespace Atlassian.Mcp.Server.Common;

/// <summary>
/// The name and version that the server reports to clients and to Atlassian.
/// </summary>
public static class ServerInfo
{
    /// <summary>The server name.</summary>
    public const string Name = "atlassian-mcp-server";

    /// <summary>Gets the server version, without build metadata.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        string? version = typeof(ServerInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrEmpty(version))
        {
            return "0.0.0";
        }

        // The SDK appends "+<commit>"; the user agent and the server info only need the version.
        int metadata = version.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? version : version[..metadata];
    }
}
