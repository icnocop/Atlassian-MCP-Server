# After updating the server

When you run the server through `dnx`, a new version is used the next time your MCP client starts it. A conversation that was already open, though, can keep using the tool definitions it had before: the names, descriptions, and parameters of the previous version.

## What happens

In Claude Code (seen in version 2.1.304), a resumed session (`claude --resume` or `--continue`) keeps the tool definitions it loaded earlier in the conversation:

- Claude Code starts the new version of the server, but tools that were loaded before still have their old definitions, including tools Claude looks up again with tool search.
- `/mcp` → **Reconnect** starts the server again, but does not refresh those definitions.
- A **new session** loads the current definitions.

So after an update, a resumed session may not know about a new parameter (for example `restrictions` on `atlassian_confluence_publish_page`), or about a new tool at all.

## How the server protects you

- Every call is checked against the tool's current parameters before it runs. A call with a parameter the tool does not have, or without one it requires, is refused with a message that lists the current parameters, instead of running with the wrong arguments.
- Changes to existing tools keep old callers safe: an existing parameter never changes meaning, and new parameters are optional, with defaults that keep the old behavior. See [Keep existing callers safe](../CONTRIBUTING.md#keep-existing-callers-safe).

## What to do

- **Start a new session after updating the server**, rather than resuming one.
- To update only when you choose, pin a version: use `Atlassian.Mcp.Server@<version>` in place of `Atlassian.Mcp.Server` in your client's configuration.

## Tip: warn when a resumed session is out of date

A Claude Code `SessionStart` hook can't refresh tool definitions, but it can warn you. This one records the server's version when a session starts, and when you resume it, warns you and Claude if the version has changed since.

Save the script as `~/.claude/hooks/Test-McpServerVersion.ps1` (requires [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell)):

```powershell
<#
.SYNOPSIS
    Claude Code SessionStart hook: warns when an MCP server's package version changed since a resumed
    session started.
#>
param(
    [string[]] $Packages = @('Atlassian.Mcp.Server'),
    [string] $StateFolder = (Join-Path $HOME '.claude/hooks/state/mcp-versions')
)

$ErrorActionPreference = 'Stop'

# The newest version on nuget.org is what "dnx <package> --prerelease" runs. Remove the prerelease
# versions from the list if you run the server without --prerelease. If nuget.org can't be reached,
# use the newest version in the local package cache.
function Get-CurrentVersion([string] $Package) {
    $id = $Package.ToLowerInvariant()
    try {
        $versions = (Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$id/index.json" -TimeoutSec 3).versions
        if ($versions) { return $versions[-1] }
    } catch { }

    $folder = Join-Path $HOME ".nuget/packages/$id"
    if (-not (Test-Path $folder)) { return $null }
    $newest = Get-ChildItem $folder -Directory |
        ForEach-Object { try { [pscustomobject] @{ Name = $_.Name; Version = [System.Management.Automation.SemanticVersion] $_.Name } } catch { } } |
        Sort-Object Version | Select-Object -Last 1
    return $newest.Name
}

try {
    $hookInput = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $sessionId = [string] $hookInput.session_id
    if (-not $sessionId) { exit 0 }

    New-Item -ItemType Directory $StateFolder -Force | Out-Null
    $stateFile = Join-Path $StateFolder "$sessionId.json"
    Get-ChildItem $StateFolder -Filter '*.json' | Where-Object LastWriteTime -lt (Get-Date).AddDays(-60) | Remove-Item -Force

    $current = @{}
    foreach ($package in $Packages) {
        $version = Get-CurrentVersion $package
        if ($version) { $current[$package] = $version }
    }

    # A new session or /clear loads the current tool definitions, so record the versions it starts with.
    if ($hookInput.source -ne 'resume' -or -not (Test-Path $stateFile)) {
        $current | ConvertTo-Json | Set-Content $stateFile -Encoding utf8
        exit 0
    }

    $recorded = Get-Content $stateFile -Raw | ConvertFrom-Json -AsHashtable
    $changes = foreach ($package in $current.Keys) {
        if ($recorded[$package] -and $recorded[$package] -ne $current[$package]) {
            "$package changed from $($recorded[$package]) to $($current[$package])"
        }
    }

    if (-not $changes) { exit 0 }

    $summary = ($changes -join '; ') + ' since this session started.'
    [pscustomobject] @{
        systemMessage      = "MCP server update: $summary This resumed session still has the old tool definitions, and /mcp Reconnect does not refresh them. Start a new session to use the new version safely."
        hookSpecificOutput = [pscustomobject] @{
            hookEventName     = 'SessionStart'
            additionalContext = "MCP server update: $summary Tool definitions loaded earlier in this conversation (including ones returned again by tool search) may be outdated: parameters may have been added, removed, or changed meaning. Before calling that server's tools, tell the user and recommend starting a new session; do not rely on remembered parameters for anything destructive."
        }
    } | ConvertTo-Json -Depth 4 -Compress
} catch {
    exit 0
}
```

Then add the hook to your user settings, `~/.claude/settings.json`, merging it with any `hooks` you already have:

```json
{
  "hooks": {
    "SessionStart": [
      {
        "matcher": "startup|resume|clear",
        "hooks": [
          {
            "type": "command",
            "command": "pwsh",
            "args": ["-NoProfile", "-File", "<home>/.claude/hooks/Test-McpServerVersion.ps1"],
            "timeout": 15,
            "statusMessage": "Checking MCP server versions"
          }
        ]
      }
    ]
  }
}
```

Replace `<home>` with the full path of your home folder, such as `C:\\Users\\you` (with doubled backslashes in JSON) or `/Users/you`. The hook applies from the next session you start or resume, and `/hooks` shows or disables it.

How it behaves:

- A new session, or `/clear`, records the current version of each package for the session.
- Resuming a session whose recorded version differs from the current one shows a warning to you, and tells Claude to recommend a new session before using that server's tools.
- A session it has never seen, such as one started before you added the hook, is recorded on resume without a warning.
- It never stops a session from starting: if anything fails, it does nothing.

To watch other MCP servers distributed on NuGet, add their package IDs to `$Packages`.
