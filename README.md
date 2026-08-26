# Claude Account Switcher

Windows tray app for switching between multiple Claude subscription accounts.
Stores each account's OAuth tokens DPAPI-encrypted under
`%APPDATA%\ClaudeAccountSwitcher`, and swaps the active one into
`~/.claude/.credentials.json` on click.

## Install

    irm https://raw.githubusercontent.com/narcosteam/ClaudeAccountSwitcher/main/install.ps1 | iex

Or grab `ClaudeAccountSwitcherSetup.exe` from the
[latest release](https://github.com/narcosteam/ClaudeAccountSwitcher/releases/latest)
and run it. Installs to `%LocalAppData%\Programs\ClaudeAccountSwitcher`, no
admin rights needed, .NET runtime bundled.

### Updating

Tray icon menu → "Check for Updates". Also checks once a day on its own and
badges the tray icon when an update is found — never installs without a
click.

## Requirements (building from source)

- Windows
- .NET 10 SDK

## Build & run

    dotnet build
    dotnet run --project src/ClaudeAccountSwitcher.csproj

## Releasing a new version

    git tag 1.3.0
    git push origin 1.3.0

Tag `x.y.z` for stable, `x.y.z-pre` for pre-release (hidden from "latest").
`.github/workflows/release.yml` builds a self-contained win-x64 release
through `installer/setup.iss` (Inno Setup) and publishes it as a GitHub
Release.

## Usage

1. Double-click the tray icon (or right-click → "Restore") to open the
   account list window.
2. "+ Add Account" → "Sign in with Claude" — logs in through Claude's normal
   browser OAuth flow. Repeat per account.
3. Click an account's row to switch to it — this rewrites
   `~/.claude/.credentials.json`. Each row shows that account's 5h/7d
   rate-limit usage, refreshed about once a minute in the background.
4. ✎ renames an account, ⋯ → "Sign out" removes it from the switcher.

## Known limitations

- Manual switching only — no automatic failover when a limit is hit yet.
- Does not check for or close a running `claude.exe` before switching.
- Windows only.
