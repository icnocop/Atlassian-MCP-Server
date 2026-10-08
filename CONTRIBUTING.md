# Contributing

Thank you for helping improve the Atlassian MCP Server. Bug reports, ideas, and pull requests are welcome.

## Build and test

Requires the .NET 10 SDK.

```shell
dotnet build Atlassian.Mcp.Server.sln
dotnet test Atlassian.Mcp.Server.sln
```

Warnings are errors, and the build enforces the [StyleCop](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) rules: a file header, XML documentation on every member, `this.` qualification, and the usual member ordering. `stylecop.json` and `.editorconfig` hold the settings.

## Run the server from source

Set the three required environment variables (see the [README](README.md#configuration)), then:

```shell
dotnet run --project src/Atlassian.Mcp.Server
```

The server speaks MCP over standard input and output, so it waits silently for a client. To try it in an MCP client, install your build as a global tool and point the client at the `atlassian-mcp-server` command:

```shell
dotnet pack src/Atlassian.Mcp.Server -c Release
dotnet tool install --global Atlassian.Mcp.Server --add-source ./artifacts --prerelease
```

Use `dotnet tool update` with the same arguments after rebuilding. The [MCP Inspector](https://github.com/modelcontextprotocol/inspector) (`npx @modelcontextprotocol/inspector atlassian-mcp-server`) is useful for calling tools by hand.

## Add or change a tool

- Put the tool in the feature folder it belongs to, such as `src/Atlassian.Mcp.Server/Jira/Comments/`, in a class marked with `[McpServerToolType]` and a `[Toolset(...)]` attribute.
- Name the tool `atlassian_jira_<action>` or `atlassian_confluence_<action>`, and name the method after the action only (`Get`, `GetAll`, `Create`, and so on).
- Describe the tool and every parameter with `[Description]`, written for the AI model: what it does, the accepted formats, and the defaults.
- Mark tools that change nothing with `ReadOnly = true`, and tools that delete with `Destructive = true`. Read-only mode relies on this.
- Accept Markdown for rich text, and return rich text as Markdown.
- Add unit tests in the matching folder under `tests/Atlassian.Mcp.Server.Tests/`. `RecordingHttpClient` records requests and returns queued responses, so tests need no Atlassian site.
- Run `build/Update-ToolsDoc.ps1` to regenerate [docs/tools.md](docs/tools.md); a test fails when a tool is missing from it.

## Pull requests

- Branch from `main` and open a pull request against `main`.
- The **Build** workflow builds and tests every pull request. Pull requests from forks wait for a maintainer to approve the workflow run.
- Add a line to the `Unreleased` section of [CHANGELOG.md](CHANGELOG.md) for user-visible changes.

## Releases

Every push to `main` builds the package and attaches it to a draft GitHub release for the version in `Directory.Build.props` (`VersionPrefix`). Publishing that draft release pushes the package to NuGet. Increase `VersionPrefix` after each release.
