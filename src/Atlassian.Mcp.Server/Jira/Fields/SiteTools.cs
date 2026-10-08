// <copyright file="SiteTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Fields;

/// <summary>
/// Tools for the Jira site and for this server's own connection to it.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraFields)]
public sealed class SiteTools
{
    private readonly JiraClient jira;
    private readonly AtlassianOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    /// <param name="options">The server options.</param>
    public SiteTools(JiraClient jira, AtlassianOptions options)
    {
        this.jira = jira;
        this.options = options;
    }

    /// <summary>
    /// Gets information about the Jira site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The site information.</returns>
    [McpServerTool(Name = "atlassian_jira_get_server_info", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets information about the Jira site: base URL, version, deployment type, and server time.")]
    public async Task<string> GetServerInfo(CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync("serverInfo", cancellationToken));

    /// <summary>
    /// Checks that the server can reach the site and sign in.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The site URL and the signed-in account.</returns>
    [McpServerTool(Name = "atlassian_jira_check_connection", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Checks that this server can reach the Jira site and sign in with the configured API token. Returns the site URL and the signed-in account.")]
    public async Task<string> CheckConnection(CancellationToken cancellationToken = default)
    {
        JsonNode? user = await this.jira.GetAsync("myself", cancellationToken);
        JsonNode? server = await this.jira.GetAsync("serverInfo", cancellationToken);

        return ToolResult.Json(new
        {
            ok = true,
            siteUrl = this.options.SiteUrl.ToString(),
            accountId = user?["accountId"]?.GetValue<string>(),
            displayName = user?["displayName"]?.GetValue<string>(),
            serverTitle = server?["serverTitle"]?.GetValue<string>(),
            deploymentType = server?["deploymentType"]?.GetValue<string>(),
        });
    }

    /// <summary>
    /// Gets the configuration of this server, without the API token.
    /// </summary>
    /// <returns>The configuration.</returns>
    [McpServerTool(Name = "atlassian_jira_get_configuration", ReadOnly = true, Idempotent = true)]
    [Description("Gets the configuration of this MCP server: site URL, account email, enabled toolsets and tools, and whether it is read-only. The API token is never returned.")]
    public string GetConfiguration()
        => ToolResult.Json(new
        {
            siteUrl = this.options.SiteUrl.ToString(),
            email = this.options.Email,
            apiTokenConfigured = !string.IsNullOrEmpty(this.options.ApiToken),
            toolsets = this.options.Toolsets.Order(StringComparer.Ordinal).ToArray(),
            enabledTools = this.options.EnabledTools.Order(StringComparer.Ordinal).ToArray(),
            readOnly = this.options.ReadOnly,
            serverVersion = ServerInfo.Version,
        });
}
