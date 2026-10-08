// <copyright file="VersionTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Projects;

/// <summary>
/// Tools for the versions (releases) of Jira projects.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraProjects)]
public sealed class VersionTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public VersionTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>Gets or sets the clock that supplies today's date for releases.</summary>
    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets all versions of the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The versions.</returns>
    [McpServerTool(Name = "atlassian_jira_get_project_versions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets all versions (releases) of the specified Jira project, with their IDs, dates, and released and archived flags.")]
    public async Task<string> GetAll(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync($"project/{JiraClient.Segment(projectKey)}/versions", cancellationToken));

    /// <summary>
    /// Creates a version in the specified project.
    /// </summary>
    /// <param name="projectKey">The project key or ID.</param>
    /// <param name="name">The version name.</param>
    /// <param name="description">The description.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="releaseDate">The release date.</param>
    /// <param name="released">A value indicating whether the version is released.</param>
    /// <param name="archived">A value indicating whether the version is archived.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new version.</returns>
    [McpServerTool(Name = "atlassian_jira_create_version", OpenWorld = true)]
    [Description("Creates a version (release) in the specified Jira project.")]
    public async Task<string> Create(
        [Description("The project key, such as PROJ, or the project ID.")] string projectKey,
        [Description("The version name, such as 2.1.0.")] string name,
        [Description("Optional version description.")] string? description = null,
        [Description("Optional start date, as YYYY-MM-DD.")] string? startDate = null,
        [Description("Optional release date, as YYYY-MM-DD.")] string? releaseDate = null,
        [Description("Optional flag that marks the version as released.")] bool? released = null,
        [Description("Optional flag that marks the version as archived.")] bool? archived = null,
        CancellationToken cancellationToken = default)
    {
        long projectId = await this.ResolveProjectIdAsync(projectKey, cancellationToken);
        var body = new { projectId, name, description, startDate, releaseDate, released, archived };
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, "version", body, cancellationToken));
    }

    /// <summary>
    /// Updates the specified version.
    /// </summary>
    /// <param name="versionId">The version ID.</param>
    /// <param name="name">The new name.</param>
    /// <param name="description">The new description.</param>
    /// <param name="startDate">The new start date.</param>
    /// <param name="releaseDate">The new release date.</param>
    /// <param name="released">A value indicating whether the version is released.</param>
    /// <param name="archived">A value indicating whether the version is archived.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated version.</returns>
    [McpServerTool(Name = "atlassian_jira_update_version", Idempotent = true, OpenWorld = true)]
    [Description("Updates the specified Jira version. Only the values that are passed are changed.")]
    public async Task<string> Update(
        [Description("The version ID, from atlassian_jira_get_project_versions.")] string versionId,
        [Description("Optional new version name.")] string? name = null,
        [Description("Optional new description.")] string? description = null,
        [Description("Optional new start date, as YYYY-MM-DD.")] string? startDate = null,
        [Description("Optional new release date, as YYYY-MM-DD.")] string? releaseDate = null,
        [Description("Optional flag that marks the version as released or unreleased.")] bool? released = null,
        [Description("Optional flag that marks the version as archived or not archived.")] bool? archived = null,
        CancellationToken cancellationToken = default)
    {
        var body = new { name, description, startDate, releaseDate, released, archived };
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Put, $"version/{JiraClient.Segment(versionId)}", body, cancellationToken));
    }

    /// <summary>
    /// Marks the specified version as released.
    /// </summary>
    /// <param name="versionId">The version ID.</param>
    /// <param name="releaseDate">The release date; defaults to today.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The released version.</returns>
    [McpServerTool(Name = "atlassian_jira_release_version", Idempotent = true, OpenWorld = true)]
    [Description("Marks the specified Jira version as released, on the given date or today.")]
    public async Task<string> Release(
        [Description("The version ID, from atlassian_jira_get_project_versions.")] string versionId,
        [Description("Optional release date, as YYYY-MM-DD. Defaults to today (UTC).")] string? releaseDate = null,
        CancellationToken cancellationToken = default)
    {
        string date = string.IsNullOrWhiteSpace(releaseDate)
            ? this.Clock.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : releaseDate.Trim();

        var body = new { released = true, releaseDate = date };
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Put, $"version/{JiraClient.Segment(versionId)}", body, cancellationToken));
    }

    /// <summary>
    /// Deletes the specified version.
    /// </summary>
    /// <param name="versionId">The version ID.</param>
    /// <param name="moveFixIssuesTo">The ID of the version to move fix-version references to.</param>
    /// <param name="moveAffectedIssuesTo">The ID of the version to move affects-version references to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_version", Destructive = true, OpenWorld = true)]
    [Description("Deletes the specified Jira version, optionally moving the issues that reference it to other versions.")]
    public async Task<string> Delete(
        [Description("The version ID, from atlassian_jira_get_project_versions.")] string versionId,
        [Description("Optional ID of the version to set as the fix version of issues that use this one.")] string? moveFixIssuesTo = null,
        [Description("Optional ID of the version to set as the affects version of issues that use this one.")] string? moveAffectedIssuesTo = null,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            moveFixIssuesTo = ProjectTools.ParseOptionalId(moveFixIssuesTo, nameof(moveFixIssuesTo)),
            moveAffectedIssuesTo = ProjectTools.ParseOptionalId(moveAffectedIssuesTo, nameof(moveAffectedIssuesTo)),
        };

        await this.jira.SendAsync(HttpMethod.Post, $"version/{JiraClient.Segment(versionId)}/removeAndSwap", body, cancellationToken);
        return ToolResult.Success($"Deleted version {versionId}.");
    }

    private async Task<long> ResolveProjectIdAsync(string projectKey, CancellationToken cancellationToken)
    {
        if (long.TryParse(projectKey.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id))
        {
            return id;
        }

        JsonNode? project = await this.jira.GetAsync($"project/{JiraClient.Segment(projectKey)}", cancellationToken);
        return ProjectTools.ParseOptionalId(project?["id"]?.GetValue<string>(), nameof(projectKey))
            ?? throw new ModelContextProtocol.McpException($"Could not find the ID of project {projectKey}.");
    }
}
