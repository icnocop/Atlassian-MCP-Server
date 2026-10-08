// <copyright file="FieldTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Fields;

/// <summary>
/// Tools for the fields that Jira issues can have.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraFields)]
public sealed class FieldTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public FieldTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets every system and custom field of the site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The fields.</returns>
    [McpServerTool(Name = "atlassian_jira_get_fields", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets every system and custom field of the Jira site: ID (such as customfield_10010), name, whether it is custom, and its schema type.")]
    public async Task<string> GetAll(CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync("field", cancellationToken));

    /// <summary>
    /// Gets the custom fields of the site.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The custom fields.</returns>
    [McpServerTool(Name = "atlassian_jira_get_custom_fields", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the custom fields of the Jira site: ID (such as customfield_10010), name, and schema type. Use the ID in additionalFields when creating or updating issues.")]
    public async Task<string> GetCustom(CancellationToken cancellationToken = default)
    {
        JsonNode? fields = await this.jira.GetAsync("field", cancellationToken);
        return ToolResult.Json(SelectCustom(fields));
    }

    /// <summary>
    /// Selects the custom fields from the response of the fields endpoint.
    /// </summary>
    /// <param name="fields">The array of field definitions.</param>
    /// <returns>The custom fields.</returns>
    internal static JsonArray SelectCustom(JsonNode? fields)
        => new((fields as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(field => field["custom"] is JsonValue custom && custom.TryGetValue(out bool isCustom) && isCustom)
            .Select(field => (JsonNode?)field.DeepClone())
            .ToArray());
}
