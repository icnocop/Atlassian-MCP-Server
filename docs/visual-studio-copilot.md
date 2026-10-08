# Use with GitHub Copilot in Visual Studio

Requires Visual Studio 2022 version 17.14 or later, or Visual Studio 2026.

## Add the server

Create or edit `%USERPROFILE%\.mcp.json` to make the server available in every solution, or a `.mcp.json` file in a solution's folder to make it available only there:

```json
{
  "servers": {
    "atlassian": {
      "type": "stdio",
      "command": "dnx",
      "args": ["AtlassianMcpServer", "--yes"],
      "env": {
        "ATLASSIAN_SITE_URL": "https://example.atlassian.net",
        "ATLASSIAN_EMAIL": "you@example.com",
        "ATLASSIAN_API_TOKEN": "your-api-token",
        "ATLASSIAN_TOOLSETS": "jira-issues,jira-search,jira-comments,jira-links,jira-attachments,jira-projects,jira-users,jira-fields,confluence"
      }
    }
  }
}
```

Keep a file that contains your API token out of source control.

## Stay under the tool limit

Copilot sends at most about 128 tools to the model, and all of this server's toolsets together have more than that. The `ATLASSIAN_TOOLSETS` value above enables the most useful toolsets (84 tools). Add `jira-agile` for boards and sprints (109 tools), or `jira-worklogs` and `jira-filters`, as long as the total stays under the limit, including the tools of your other servers.

## Check that it works

Open the GitHub Copilot Chat window in Agent mode, select the tools button, and confirm that the `atlassian` tools are listed. Then ask, for example, "Check my Atlassian connection."

## Updates

`dnx` runs the latest version from NuGet each time Visual Studio starts the server. To stay on one version, use `AtlassianMcpServer@<version>`.
