# Use with GitHub Copilot in Visual Studio Code

## Add the server

Open the Command Palette, run **MCP: Open User Configuration**, and add the server to the `mcp.json` file that opens. VS Code prompts for the API token the first time the server starts and stores it securely.

```json
{
  "inputs": [
    {
      "id": "atlassian-api-token",
      "type": "promptString",
      "description": "Atlassian API token",
      "password": true
    }
  ],
  "servers": {
    "atlassian": {
      "type": "stdio",
      "command": "dnx",
      "args": ["AtlassianMcpServer", "--yes"],
      "env": {
        "ATLASSIAN_SITE_URL": "https://example.atlassian.net",
        "ATLASSIAN_EMAIL": "you@example.com",
        "ATLASSIAN_API_TOKEN": "${input:atlassian-api-token}",
        "ATLASSIAN_TOOLSETS": "jira-issues,jira-search,jira-comments,jira-links,jira-attachments,jira-projects,jira-users,jira-fields,confluence"
      }
    }
  }
}
```

To share the server with everyone who works in a repository, put the same content in `.vscode/mcp.json` in the repository instead.

## Stay under the tool limit

Copilot sends at most about 128 tools to the model, and all of this server's toolsets together have more than that. The `ATLASSIAN_TOOLSETS` value above enables the most useful toolsets (84 tools). Add `jira-agile` for boards and sprints (109 tools), or `jira-worklogs` and `jira-filters`, as long as the total stays under the limit, including the tools of your other servers. You can also turn tools off in the **Configure Tools** picker in the Chat view.

## Check that it works

Open the Chat view in Agent mode, select **Configure Tools**, and confirm that the `atlassian` tools are listed. Then ask, for example, "Check my Atlassian connection."

## Updates

`dnx` runs the latest version from NuGet each time VS Code starts the server. To stay on one version, use `AtlassianMcpServer@<version>`.
