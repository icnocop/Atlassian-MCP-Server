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

Every build has a unique version, `MAJOR.MINOR.BUILD`. `MAJOR.MINOR` is `VersionMajorMinor` in `Directory.Build.props` (increase it by hand for a new feature release), and `BUILD` is the GitHub workflow run number. Local builds are `0.1.0-dev`; workflow builds are prereleases such as `0.1.42-prerelease.g1a2b3c4`, where the suffix carries the commit. The assembly, file, product, and package versions, and the versions in the generated `.mcp/server.json` (from `.mcp/server.template.json`), all follow it.

Every push to `main` publishes a GitHub pre-release, such as `v0.1.42-prerelease.g1a2b3c4`, with its package attached. Older pre-releases are deleted, except those pushed to NuGet. Nothing goes to NuGet until you run the **Publish** workflow from the Actions tab and choose:

- **prerelease**, to push a pre-release's package to NuGet as it is (the newest pre-release by default), or
- **release**, to build that pre-release's commit with the same build number and no suffix (such as `0.1.42`), test it, push it to NuGet, and publish it as the latest GitHub release.

Publishing to NuGet uses [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): add a policy on nuget.org for this repository's `publish.yml`, set the repository variable `NUGET_USER` to the nuget.org account name, and create a `nuget` environment (optionally with required reviewers).
