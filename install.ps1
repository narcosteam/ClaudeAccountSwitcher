# Downloads and silently installs the latest stable Claude Account Switcher release.
# Usage: irm https://raw.githubusercontent.com/narcosteam/ClaudeAccountSwitcher/main/install.ps1 | iex

$ErrorActionPreference = 'Stop'
$repo = 'narcosteam/ClaudeAccountSwitcher'

$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers @{ 'User-Agent' = 'ClaudeAccountSwitcher-Installer' }
$asset = $release.assets | Where-Object { $_.name -like '*.exe' } | Select-Object -First 1
if (-not $asset) {
    throw "No installer asset found on the latest release ($($release.tag_name))."
}

$installer = Join-Path $env:TEMP $asset.name
Write-Host "Downloading Claude Account Switcher $($release.tag_name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $installer

Write-Host "Installing..."
Start-Process -FilePath $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES' -Wait

Write-Host "Done — Claude Account Switcher $($release.tag_name) installed."
