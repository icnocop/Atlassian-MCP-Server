// <copyright file="Toolsets.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Configuration;

/// <summary>
/// The names of the toolsets that group the tools, so that a client can enable only what it needs.
/// </summary>
public static class Toolsets
{
    /// <summary>The value that enables every toolset except the opt-in ones.</summary>
    public const string All = "all";

    /// <summary>Jira issues: get, create, update, delete, transition, assign, labels, watchers, and votes.</summary>
    public const string JiraIssues = "jira-issues";

    /// <summary>Jira JQL search, validation, and autocomplete.</summary>
    public const string JiraSearch = "jira-search";

    /// <summary>Jira issue comments.</summary>
    public const string JiraComments = "jira-comments";

    /// <summary>Jira issue links and link types.</summary>
    public const string JiraLinks = "jira-links";

    /// <summary>Jira issue attachments.</summary>
    public const string JiraAttachments = "jira-attachments";

    /// <summary>Jira projects, components, versions, and roles.</summary>
    public const string JiraProjects = "jira-projects";

    /// <summary>Jira users, groups, and permissions.</summary>
    public const string JiraUsers = "jira-users";

    /// <summary>Jira worklogs and time tracking.</summary>
    public const string JiraWorklogs = "jira-worklogs";

    /// <summary>Jira saved filters.</summary>
    public const string JiraFilters = "jira-filters";

    /// <summary>Jira boards, sprints, epics, backlog, ranking, and reports.</summary>
    public const string JiraAgile = "jira-agile";

    /// <summary>Jira fields, issue types, statuses, priorities, resolutions, and site information.</summary>
    public const string JiraFields = "jira-fields";

    /// <summary>Jira administration: creating, updating, and deleting projects and group membership.</summary>
    public const string JiraAdmin = "jira-admin";

    /// <summary>Confluence pages, spaces, search, comments, attachments, and labels.</summary>
    public const string Confluence = "confluence";

    /// <summary>Confluence page deletion. Opt-in: not included in <see cref="All"/>.</summary>
    public const string ConfluenceAdmin = "confluence-admin";

    /// <summary>Gets every known toolset name.</summary>
    public static IReadOnlyList<string> Known { get; } =
    [
        JiraIssues,
        JiraSearch,
        JiraComments,
        JiraLinks,
        JiraAttachments,
        JiraProjects,
        JiraUsers,
        JiraWorklogs,
        JiraFilters,
        JiraAgile,
        JiraFields,
        JiraAdmin,
        Confluence,
        ConfluenceAdmin,
    ];

    /// <summary>Gets the toolsets that <see cref="All"/> leaves out, because they hold tools that destroy content.</summary>
    public static IReadOnlySet<string> OptIn { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ConfluenceAdmin };

    /// <summary>
    /// Parses the value of the toolsets variable.
    /// </summary>
    /// <param name="value">A comma-separated list of toolset names, or <see langword="null"/> for <see cref="All"/>.</param>
    /// <param name="errors">Receives a message for every unknown toolset name.</param>
    /// <returns>The enabled toolsets.</returns>
    public static HashSet<string> Parse(string? value, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        HashSet<string> requested = AtlassianOptions.SplitList(value);
        if (requested.Count == 0)
        {
            requested.Add(All);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in requested)
        {
            if (string.Equals(name, All, StringComparison.OrdinalIgnoreCase))
            {
                result.UnionWith(Known.Where(known => !OptIn.Contains(known)));
            }
            else if (Known.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(name);
            }
            else
            {
                errors.Add($"The {AtlassianOptions.ToolsetsVariable} environment variable names an unknown toolset '{name}'. Known toolsets: {All}, {string.Join(", ", Known)}.");
            }
        }

        return result;
    }
}
