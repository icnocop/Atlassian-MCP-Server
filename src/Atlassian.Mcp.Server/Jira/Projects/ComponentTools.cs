// <copyright file="ComponentTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Projects;

/// <summary>
/// Tools for the components of Jira projects.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraProjects)]
public sealed class ComponentTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public ComponentTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Gets all components of the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The components.</returns>
    [McpServerTool(Name = "atlassian_jira_get_project_components", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets all components of the specified Jira project.")]
    public async Task<string> GetAll(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync($"project/{JiraClient.Segment(projectKey)}/components", cancellationToken));

    /// <summary>
    /// Creates a component in the specified project.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <param name="name">The component name.</param>
    /// <param name="description">The description.</param>
    /// <param name="leadAccountId">The account ID of the component lead.</param>
    /// <param name="assigneeType">The default assignee.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new component.</returns>
    [McpServerTool(Name = "atlassian_jira_create_component", OpenWorld = true)]
    [Description("Creates a component in the specified Jira project.")]
    public async Task<string> Create(
        [Description("The project key, such as PROJ.")] string projectKey,
        [Description("The component name.")] string name,
        [Description("Optional component description.")] string? description = null,
        [Description("Optional account ID of the component lead.")] string? leadAccountId = null,
        [Description("Optional default assignee: PROJECT_DEFAULT, COMPONENT_LEAD, PROJECT_LEAD, or UNASSIGNED.")] string? assigneeType = null,
        CancellationToken cancellationToken = default)
    {
        var body = new { project = projectKey.Trim(), name, description, leadAccountId, assigneeType };
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, "component", body, cancellationToken));
    }

    /// <summary>
    /// Deletes the specified component.
    /// </summary>
    /// <param name="componentId">The component ID.</param>
    /// <param name="moveIssuesTo">The ID of the component to move its issues to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_component", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified Jira component, optionally moving its issues to another component.")]
    public async Task<string> Delete(
        [Description("The component ID, from atlassian_jira_get_project_components.")] string componentId,
        [Description("Optional ID of the component to move the issues to. When omitted, the component is removed from its issues.")] string? moveIssuesTo = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"component/{JiraClient.Segment(componentId)}").Add("moveIssuesTo", moveIssuesTo?.Trim()).ToString();
        await this.jira.SendAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        return ToolResult.Success($"Deleted component {componentId}.");
    }
}
