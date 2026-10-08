# Use with Claude Code

## Add the server

Run this once, replacing the three values. `--scope user` makes the server available in every project.

```shell
claude mcp add atlassian --scope user \
  -e ATLASSIAN_SITE_URL=https://example.atlassian.net \
  -e ATLASSIAN_EMAIL=you@example.com \
  -e ATLASSIAN_API_TOKEN=your-api-token \
  -- dnx Atlassian.Mcp.Server --yes
```

On Windows PowerShell, put the command on one line or end each line with a backtick (`` ` ``) instead of a backslash.

To share the server with a team through a project's `.mcp.json` file instead, use `--scope project`, and keep the API token out of the file by referencing an environment variable that each person sets:

```json
{
  "mcpServers": {
    "atlassian": {
      "command": "dnx",
      "args": ["Atlassian.Mcp.Server", "--yes"],
      "env": {
        "ATLASSIAN_SITE_URL": "https://example.atlassian.net",
        "ATLASSIAN_EMAIL": "${ATLASSIAN_EMAIL}",
        "ATLASSIAN_API_TOKEN": "${ATLASSIAN_API_TOKEN}"
      }
    }
  }
}
```

## Check that it works

Run `claude mcp list`, or type `/mcp` in Claude Code, and confirm that `atlassian` is connected. Then ask, for example, "Check my Atlassian connection."

## Tool names and permissions

Claude Code shows the tools as `mcp__atlassian__<tool>`, such as `mcp__atlassian__atlassian_jira_get_issue`. To stop Claude Code from asking before each read, allow the read-only tools in your `settings.json`:

```json
{
  "permissions": {
    "allow": [
      "mcp__atlassian__atlassian_jira_get_*",
      "mcp__atlassian__atlassian_jira_search_*",
      "mcp__atlassian__atlassian_jira_list_*",
      "mcp__atlassian__atlassian_confluence_get_*",
      "mcp__atlassian__atlassian_confluence_search"
    ]
  }
}
```

## Updates

`dnx` runs the latest version from NuGet each time Claude Code starts the server. To stay on one version, use `Atlassian.Mcp.Server@<version>` in place of `Atlassian.Mcp.Server`.
