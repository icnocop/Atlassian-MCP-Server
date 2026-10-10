# Troubleshooting

## The server does not start

MCP clients write the server's error output to their logs: run `/mcp` in Claude Code or the GitHub Copilot CLI, or open the MCP server's output in Visual Studio Code or Visual Studio.

- **"The ATLASSIAN_SITE_URL environment variable is not set"** (or `ATLASSIAN_EMAIL`, `ATLASSIAN_API_TOKEN`): add the variable to the server's `env` section in your client's configuration. See the guide for your client.
- **"atlassian.mcp.server is not found in NuGet feeds"**: `dnx` looks only for stable versions unless told otherwise. While only prerelease versions are published, add `--prerelease` after the package name, or name a version, as in `Atlassian.Mcp.Server@<version>`.
- **`dnx` is not recognized**: install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later.

## Installing as a global tool fails in a folder with a `global.json`

```
The settings file in the tool's NuGet package is invalid: Settings file 'DotnetToolSettings.xml' was not found in the package.
```

`dotnet tool install` ran under an older .NET SDK that a `global.json` file in the current folder, or a parent folder, selects. That SDK cannot install a .NET 10 tool, and reports it this way. Run the command from a folder without such a `global.json`, such as your user folder.

This affects only `dotnet tool install` and `dotnet tool update`. `dnx` runs the server from any folder, including one whose `global.json` pins an older SDK, so clients that run `dnx Atlassian.Mcp.Server` are not affected.

## The client reports too many tools

GitHub Copilot sends at most about 128 tools to the model. Set `ATLASSIAN_TOOLSETS` to fewer toolsets, as the Copilot guides show.

## Atlassian rejects a request

- **401 Unauthorized**: the email or API token is wrong, or the token has expired or been revoked. Create a new token at https://id.atlassian.com/manage-profile/security/api-tokens.
- **403 Forbidden, or 404 Not Found for something that exists**: your account lacks permission. Atlassian answers 404 rather than 403 for content you cannot see.
- **429 Too Many Requests**: the server already retries throttled requests up to three times, honoring the `Retry-After` header; wait and try again.

Use the `atlassian_jira_check_connection` tool to see which account and site the server uses, and `atlassian_jira_get_configuration` to see its toolsets.
