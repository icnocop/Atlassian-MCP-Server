# Use with the GitHub Copilot CLI

## Add the server

Add the server to `~/.copilot/mcp-config.json` (on Windows, `%USERPROFILE%\.copilot\mcp-config.json`), or run `/mcp add` in the Copilot CLI and enter the same values:

```json
{
  "mcpServers": {
    "atlassian": {
      "type": "local",
      "command": "dnx",
      "args": ["AtlassianMcpServer", "--yes"],
      "env": {
        "ATLASSIAN_SITE_URL": "https://example.atlassian.net",
        "ATLASSIAN_EMAIL": "you@example.com",
        "ATLASSIAN_API_TOKEN": "your-api-token",
        "ATLASSIAN_TOOLSETS": "jira-issues,jira-search,jira-comments,jira-links,jira-attachments,jira-projects,jira-users,jira-fields,confluence"
      },
      "tools": ["*"]
    }
  }
}
```

The `ATLASSIAN_TOOLSETS` value keeps the number of tools under the limit of about 128 that Copilot sends to the model. Add `jira-agile` for boards and sprints (109 tools in total).

## Check that it works

Run `/mcp` in the Copilot CLI and confirm that `atlassian` is connected. Then ask, for example, "Check my Atlassian connection."

## Updates

`dnx` runs the latest version from NuGet each time the Copilot CLI starts the server. To stay on one version, use `AtlassianMcpServer@<version>`.
