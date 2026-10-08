# Atlassian MCP Server

[![Build](https://github.com/icnocop/Atlassian-MCP-Server/actions/workflows/build.yml/badge.svg)](https://github.com/icnocop/Atlassian-MCP-Server/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Atlassian.Mcp.Server.svg)](https://www.nuget.org/packages/Atlassian.Mcp.Server)

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that lets AI assistants such as Claude Code and GitHub Copilot work with **Jira Cloud** and **Confluence Cloud**. It runs on your machine, talks to your site with your own API token, and needs no Rovo credits.

> This is an unofficial, community project. It is not affiliated with or endorsed by Atlassian.

## Features

**Jira**

- **Issues**: get, create, update, delete, assign, transition, labels, watchers, votes, and change history.
- **Custom fields**: set any field, including custom fields, and discover a project's required fields and allowed values before creating an issue.
- **Markdown everywhere**: descriptions, comments, worklog comments, and multi-line custom fields are written in Markdown and converted to Atlassian Document Format; rich text is read back as Markdown.
- **Search**: JQL search with paging, approximate totals, JQL validation, and value suggestions.
- **Comments, links, and attachments**: read and write comments, link issues by link type name or phrase (such as "is blocked by"), and upload, download, or delete attachments.
- **Projects and people**: projects, components, versions, users, groups, and permissions.
- **Time tracking**: worklogs and estimates.
- **Filters**: saved filters and favorites.
- **Agile**: boards, backlogs, sprints, epics, ranking, and sprint report, burndown, and velocity data.

**Confluence**

- **Private drafts**: new pages and changes to existing pages are saved as drafts by default, so you can preview them in Confluence before anything is published.
- **Safe editing**: edit one section of a page and keep everything else, including macros and smart links; edits that would lose content Markdown cannot represent are refused.
- **Pages**: read pages as Markdown or ADF, create, update, publish, move, and delete them, and browse children and version history.
- **Search**: CQL search across spaces.
- **Spaces, comments, attachments, and labels**.

Tools are grouped into [toolsets](#configuration) so you can enable only what you need. See the [full list of tools](docs/tools.md).

## System requirements

- Windows, macOS, or Linux.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later, which provides the `dnx` and `dotnet tool` commands used to run the server.
- An MCP client, such as Claude Code, GitHub Copilot in Visual Studio Code or Visual Studio, or the GitHub Copilot CLI.

## Atlassian requirements

- A Jira Cloud site, a Confluence Cloud site, or both, such as `https://example.atlassian.net`.
- An Atlassian account on that site.
- An API token for that account. See [Manage API tokens for your Atlassian account](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/).

The server acts as you: it can see and change only what your account can, and every change appears in Jira and Confluence history under your name.

## Getting started

1. **Create an API token** at https://id.atlassian.com/manage-profile/security/api-tokens.
2. **Add the server to your AI assistant.** Every client runs the same command, `dnx Atlassian.Mcp.Server --yes`, which downloads the server from NuGet and runs the latest version each time it starts. Follow the guide for your assistant:
   - [Claude Code](docs/claude-code.md)
   - [GitHub Copilot in Visual Studio Code](docs/vscode-copilot.md)
   - [GitHub Copilot in Visual Studio](docs/visual-studio-copilot.md)
   - [GitHub Copilot CLI](docs/copilot-cli.md)
   If your workspace has a `global.json` that pins an older .NET SDK, `dnx` cannot run there, because clients start the server in the workspace folder. Install the server as a global tool instead, from a folder without that `global.json`: `dotnet tool install --global Atlassian.Mcp.Server`. Then use `atlassian-mcp-server` as the command, and update it with `dotnet tool update --global Atlassian.Mcp.Server`.
3. **Try it.** Ask your assistant, for example:
   - "Show me my open Jira issues in project PROJ."
   - "Create a bug in PROJ for the login timeout, with steps to reproduce."
   - "Draft a Confluence page in my personal space summarizing PROJ-123." Then open the link it returns to preview the draft.

## Configuration

The server is configured with environment variables, which each guide shows how to set.

| Variable | Required | Description |
|---|---|---|
| `ATLASSIAN_SITE_URL` | Yes | Your site URL, such as `https://example.atlassian.net`. |
| `ATLASSIAN_EMAIL` | Yes | The email address of your Atlassian account. |
| `ATLASSIAN_API_TOKEN` | Yes | Your API token. |
| `ATLASSIAN_TOOLSETS` | No | Comma-separated toolsets to enable. Defaults to `all`. |
| `ATLASSIAN_ENABLED_TOOLS` | No | Comma-separated tool names to enable, to narrow the toolsets further. |
| `ATLASSIAN_READ_ONLY` | No | `true` to enable only tools that do not change anything. |

| Toolset | Tools for |
|---|---|
| `jira-issues` | Issues, transitions, labels, watchers, votes, and history |
| `jira-search` | JQL search, validation, and suggestions |
| `jira-comments` | Issue comments |
| `jira-links` | Issue links and link types |
| `jira-attachments` | Issue attachments |
| `jira-projects` | Projects, components, versions, and roles |
| `jira-users` | Users, groups, and permissions |
| `jira-worklogs` | Worklogs and time tracking |
| `jira-filters` | Saved filters |
| `jira-agile` | Boards, sprints, epics, ranking, and reports |
| `jira-fields` | Fields, issue types, priorities, statuses, resolutions, and connection checks |
| `jira-admin` | Creating, updating, and deleting projects, and group membership |
| `confluence` | Pages, drafts, search, spaces, comments, attachments, and labels |
| `confluence-admin` | Deleting pages. Not included in `all`; enable it explicitly. |

Some clients limit how many tools they send to the model; GitHub Copilot allows about 128. The guides show a toolset selection that stays under that limit.

## More information

- [Tools](docs/tools.md)
- [Contributing](CONTRIBUTING.md)
- [Changes](CHANGELOG.md)

## License

[MIT](LICENSE)
