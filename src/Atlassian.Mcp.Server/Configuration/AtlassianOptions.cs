// <copyright file="AtlassianOptions.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Configuration;

/// <summary>
/// The server settings, read from environment variables when the process starts.
/// </summary>
public sealed class AtlassianOptions
{
    /// <summary>The variable that holds the Atlassian Cloud site URL.</summary>
    public const string SiteUrlVariable = "ATLASSIAN_SITE_URL";

    /// <summary>The variable that holds the email address of the Atlassian account.</summary>
    public const string EmailVariable = "ATLASSIAN_EMAIL";

    /// <summary>The variable that holds the API token of the Atlassian account.</summary>
    public const string ApiTokenVariable = "ATLASSIAN_API_TOKEN";

    /// <summary>The variable that holds the comma-separated list of toolsets to enable.</summary>
    public const string ToolsetsVariable = "ATLASSIAN_TOOLSETS";

    /// <summary>The variable that holds the comma-separated list of tool names to enable.</summary>
    public const string EnabledToolsVariable = "ATLASSIAN_ENABLED_TOOLS";

    /// <summary>The variable that hides every tool that changes data, when set to <c>true</c>.</summary>
    public const string ReadOnlyVariable = "ATLASSIAN_READ_ONLY";

    /// <summary>
    /// Initializes a new instance of the <see cref="AtlassianOptions"/> class.
    /// </summary>
    /// <param name="siteUrl">The site URL, such as <c>https://example.atlassian.net/</c>.</param>
    /// <param name="email">The email address of the Atlassian account.</param>
    /// <param name="apiToken">The API token of the Atlassian account.</param>
    /// <param name="toolsets">The toolsets to enable.</param>
    /// <param name="enabledTools">The tool names to enable, or an empty set to enable every tool in <paramref name="toolsets"/>.</param>
    /// <param name="readOnly">A value indicating whether tools that change data are hidden.</param>
    public AtlassianOptions(
        Uri siteUrl,
        string email,
        string apiToken,
        IReadOnlySet<string> toolsets,
        IReadOnlySet<string> enabledTools,
        bool readOnly)
    {
        this.SiteUrl = siteUrl;
        this.Email = email;
        this.ApiToken = apiToken;
        this.Toolsets = toolsets;
        this.EnabledTools = enabledTools;
        this.ReadOnly = readOnly;
    }

    /// <summary>Gets the site URL. It always ends with a slash.</summary>
    public Uri SiteUrl { get; }

    /// <summary>Gets the email address of the Atlassian account.</summary>
    public string Email { get; }

    /// <summary>Gets the API token of the Atlassian account.</summary>
    public string ApiToken { get; }

    /// <summary>Gets the toolsets to enable.</summary>
    public IReadOnlySet<string> Toolsets { get; }

    /// <summary>Gets the tool names to enable. An empty set enables every tool in <see cref="Toolsets"/>.</summary>
    public IReadOnlySet<string> EnabledTools { get; }

    /// <summary>Gets a value indicating whether tools that change data are hidden.</summary>
    public bool ReadOnly { get; }

    /// <summary>
    /// Reads the options from environment variables.
    /// </summary>
    /// <param name="getVariable">Returns the value of an environment variable, or <see langword="null"/> when it is not set.</param>
    /// <param name="errors">Receives a message for every missing or invalid variable.</param>
    /// <returns>The options, or <see langword="null"/> when <paramref name="errors"/> is not empty.</returns>
    public static AtlassianOptions? FromEnvironment(Func<string, string?> getVariable, out IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(getVariable);

        var messages = new List<string>();

        string? siteUrlText = Read(getVariable, SiteUrlVariable, messages);
        string? email = Read(getVariable, EmailVariable, messages);
        string? apiToken = Read(getVariable, ApiTokenVariable, messages);

        Uri? siteUrl = null;
        if (siteUrlText is not null)
        {
            siteUrl = ParseSiteUrl(siteUrlText, messages);
        }

        HashSet<string> toolsets = Configuration.Toolsets.Parse(getVariable(ToolsetsVariable), messages);
        HashSet<string> enabledTools = SplitList(getVariable(EnabledToolsVariable));
        bool readOnly = string.Equals(getVariable(ReadOnlyVariable)?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        errors = messages;
        if (messages.Count > 0)
        {
            return null;
        }

        return new AtlassianOptions(siteUrl!, email!, apiToken!, toolsets, enabledTools, readOnly);
    }

    /// <summary>
    /// Splits a comma-separated list into a case-insensitive set, ignoring blank entries.
    /// </summary>
    /// <param name="value">The list, or <see langword="null"/>.</param>
    /// <returns>The trimmed entries.</returns>
    internal static HashSet<string> SplitList(string? value)
        => (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string? Read(Func<string, string?> getVariable, string name, List<string> messages)
    {
        string? value = getVariable(name)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            messages.Add($"The {name} environment variable is not set.");
            return null;
        }

        return value;
    }

    private static Uri? ParseSiteUrl(string value, List<string> messages)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            messages.Add($"The {SiteUrlVariable} environment variable must be an https URL, such as https://example.atlassian.net.");
            return null;
        }

        // Only the site root is meaningful: Jira and Confluence paths are appended to it, so a URL
        // copied from the browser (for example ".../wiki/home" or ".../jira/your-work") is trimmed.
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
    }
}
