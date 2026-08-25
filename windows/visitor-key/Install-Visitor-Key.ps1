param(
    [string]$PublishedPath = (Join-Path $PSScriptRoot 'publish')
)

$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'LOKWOD Visitor Key'
$appRoot = Join-Path $installRoot 'app'
$startupRoot = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startupRoot 'LOKWOD Visitor Key.lnk'
$sourceBackup = Join-Path $PSScriptRoot 'key8-original-raw.jpg'
$installedBackup = Join-Path $installRoot 'key8-original.jpg'

if (-not (Test-Path -LiteralPath (Join-Path $PublishedPath 'LOKWODVisitorKey.exe'))) {
    throw "Published app not found at $PublishedPath"
}

New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
Copy-Item -Path (Join-Path $PublishedPath '*') -Destination $appRoot -Recurse -Force

if ((Test-Path -LiteralPath $sourceBackup) -and -not (Test-Path -LiteralPath $installedBackup)) {
    Copy-Item -LiteralPath $sourceBackup -Destination $installedBackup
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $appRoot 'LOKWODVisitorKey.exe'
$shortcut.WorkingDirectory = $appRoot
$shortcut.Description = 'LOKWOD Visitor Key for be quiet! Dark Mount'
$shortcut.Save()

Write-Host "Installed: $appRoot"
Write-Host "Startup shortcut: $shortcutPath"
