// <copyright file="FilterTools.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.ComponentModel;
using System.Text.Json.Nodes;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Jira.Filters;

/// <summary>
/// Tools for saved Jira filters.
/// </summary>
[McpServerToolType]
[Toolset(Toolsets.JiraFilters)]
public sealed class FilterTools
{
    private readonly JiraClient jira;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterTools"/> class.
    /// </summary>
    /// <param name="jira">The Jira client.</param>
    public FilterTools(JiraClient jira)
    {
        this.jira = jira;
    }

    /// <summary>
    /// Searches the filters that the user can see.
    /// </summary>
    /// <param name="filterName">The text that filter names must contain.</param>
    /// <param name="ownerAccountId">The account ID of the owner.</param>
    /// <param name="expand">A comma-separated list of properties to expand.</param>
    /// <param name="startAt">The index of the first result.</param>
    /// <param name="maxResults">The largest number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of filters.</returns>
    [McpServerTool(Name = "atlassian_jira_search_filters", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Searches the saved Jira filters that the user owns or that are shared with them, one page at a time.")]
    public async Task<string> Search(
        [Description("Optional text that filter names must contain.")] string? filterName = null,
        [Description("Optional account ID of the filter owner.")] string? ownerAccountId = null,
        [Description("Optional comma-separated properties to expand: description, owner, jql, viewUrl, searchUrl, favourite, sharePermissions.")] string? expand = null,
        [Description("The index of the first result, starting at 0.")] int? startAt = null,
        [Description("The largest number of results to return.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString("filter/search")
            .Add("filterName", filterName)
            .Add("accountId", ownerAccountId?.Trim())
            .Add("expand", expand)
            .Add("startAt", startAt)
            .Add("maxResults", maxResults)
            .ToString();

        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the specified filter.
    /// </summary>
    /// <param name="filterId">The filter ID.</param>
    /// <param name="expand">A comma-separated list of properties to expand.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The filter.</returns>
    [McpServerTool(Name = "atlassian_jira_get_filter", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the specified saved Jira filter, including its JQL.")]
    public async Task<string> Get(
        [Description("The filter ID.")] string filterId,
        [Description("Optional comma-separated properties to expand, such as sharedUsers or subscriptions.")] string? expand = null,
        CancellationToken cancellationToken = default)
    {
        string path = new QueryString($"filter/{JiraClient.Segment(filterId)}").Add("expand", expand).ToString();
        return ToolResult.Json(await this.jira.GetAsync(path, cancellationToken));
    }

    /// <summary>
    /// Gets the filters that the user marked as favorites.
    /// </summary>
    /// <param name="expand">A comma-separated list of properties to expand.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The favorite filters.</returns>
    [McpServerTool(Name = "atlassian_jira_get_favorite_filters", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the saved Jira filters that the user marked as favorites.")]
    public async Task<string> GetFavorites(
        [Description("Optional comma-separated properties to expand, such as sharedUsers or subscriptions.")] string? expand = null,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.GetAsync(new QueryString("filter/favourite").Add("expand", expand).ToString(), cancellationToken));

    /// <summary>
    /// Creates a filter.
    /// </summary>
    /// <param name="name">The filter name.</param>
    /// <param name="jql">The JQL query.</param>
    /// <param name="description">The description.</param>
    /// <param name="favorite">A value indicating whether to mark the filter as a favorite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new filter.</returns>
    [McpServerTool(Name = "atlassian_jira_create_filter", OpenWorld = true)]
    [Description("Saves a JQL query as a Jira filter.")]
    public async Task<string> Create(
        [Description("The filter name, unique among the user's filters.")] string name,
        [Description("The JQL query.")] string jql,
        [Description("Optional filter description.")] string? description = null,
        [Description("Optional flag that marks the filter as a favorite.")] bool? favorite = null,
        CancellationToken cancellationToken = default)
    {
        var body = new { name, jql, description, favourite = favorite };
        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Post, "filter", body, cancellationToken));
    }

    /// <summary>
    /// Updates the specified filter.
    /// </summary>
    /// <param name="filterId">The filter ID.</param>
    /// <param name="name">The new name.</param>
    /// <param name="jql">The new JQL query.</param>
    /// <param name="description">The new description.</param>
    /// <param name="favorite">A value indicating whether the filter is a favorite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated filter.</returns>
    [McpServerTool(Name = "atlassian_jira_update_filter", Idempotent = true, OpenWorld = true)]
    [Description("Updates the specified saved Jira filter. Only the values that are passed are changed.")]
    public async Task<string> Update(
        [Description("The filter ID.")] string filterId,
        [Description("Optional new filter name.")] string? name = null,
        [Description("Optional new JQL query.")] string? jql = null,
        [Description("Optional new description.")] string? description = null,
        [Description("Optional flag that marks the filter as a favorite or not.")] bool? favorite = null,
        CancellationToken cancellationToken = default)
    {
        string path = $"filter/{JiraClient.Segment(filterId)}";

        // The API requires the name in every update, so an unchanged name is read first.
        if (string.IsNullOrWhiteSpace(name))
        {
            JsonNode? current = await this.jira.GetAsync(path, cancellationToken);
            name = current?["name"]?.GetValue<string>();
        }

        var body = new JsonObject
        {
            ["name"] = name,
            ["jql"] = jql,
            ["description"] = description,
            ["favourite"] = favorite,
        };

        foreach (string key in body.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
        {
            body.Remove(key);
        }

        return ToolResult.Json(await this.jira.SendAsync(HttpMethod.Put, path, body, cancellationToken));
    }

    /// <summary>
    /// Deletes the specified filter.
    /// </summary>
    /// <param name="filterId">The filter ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation.</returns>
    [McpServerTool(Name = "atlassian_jira_delete_filter", Destructive = true, Idempotent = true, OpenWorld = true)]
    [Description("Deletes the specified saved Jira filter.")]
    public async Task<string> Delete(
        [Description("The filter ID.")] string filterId,
        CancellationToken cancellationToken = default)
    {
        await this.jira.SendAsync(HttpMethod.Delete, $"filter/{JiraClient.Segment(filterId)}", body: null, cancellationToken);
        return ToolResult.Success($"Deleted filter {filterId}.");
    }

    /// <summary>
    /// Marks the specified filter as a favorite.
    /// </summary>
    /// <param name="filterId">The filter ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The filter.</returns>
    [McpServerTool(Name = "atlassian_jira_add_filter_to_favorites", Idempotent = true, OpenWorld = true)]
    [Description("Marks the specified saved Jira filter as a favorite of the user.")]
    public async Task<string> AddToFavorites(
        [Description("The filter ID.")] string filterId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.SendAsync(HttpMethod.Put, $"filter/{JiraClient.Segment(filterId)}/favourite", body: null, cancellationToken));

    /// <summary>
    /// Removes the specified filter from the user's favorites.
    /// </summary>
    /// <param name="filterId">The filter ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The filter.</returns>
    [McpServerTool(Name = "atlassian_jira_remove_filter_from_favorites", Idempotent = true, OpenWorld = true)]
    [Description("Removes the specified saved Jira filter from the user's favorites.")]
    public async Task<string> RemoveFromFavorites(
        [Description("The filter ID.")] string filterId,
        CancellationToken cancellationToken = default)
        => ToolResult.Json(await this.jira.SendAsync(HttpMethod.Delete, $"filter/{JiraClient.Segment(filterId)}/favourite", body: null, cancellationToken));
}
