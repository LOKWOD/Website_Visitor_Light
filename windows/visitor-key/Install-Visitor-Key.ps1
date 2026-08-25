param(
    [string]$PublishedPath = (Join-Path $PSScriptRoot 'publish')
)

$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'LOKWOD Visitor Key'
$appRoot = Join-Path $installRoot 'app'
$startupRoot = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startupRoot 'LOKWOD Visitor Key.lnk'
$sourceBackup = Join-Path $PSScriptRoot 'key4-original-raw.jpg'
$installedBackup = Join-Path $installRoot 'key4-original.jpg'

if (-not (Test-Path -LiteralPath (Join-Path $PublishedPath 'LOKWODVisitorKey.exe'))) {
    throw "Published app not found at $PublishedPath"
}

# Files extracted from a browser-downloaded ZIP can inherit Windows'
# Mark-of-the-Web. Remove it before running the helper or starting the tray app
# so SmartScreen does not cancel the installation behind this script.
Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -ErrorAction SilentlyContinue |
    Unblock-File -ErrorAction SilentlyContinue

Get-Process -Name 'LOKWODVisitorKey' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
Copy-Item -Path (Join-Path $PublishedPath '*') -Destination $appRoot -Recurse -Force
Get-ChildItem -LiteralPath $appRoot -Recurse -File -ErrorAction SilentlyContinue |
    Unblock-File -ErrorAction SilentlyContinue

$installedHidApi = Join-Path $appRoot 'hidapi.dll'
if (-not (Test-Path -LiteralPath $installedHidApi)) {
    $hidApiCandidates = @(
        (Join-Path $env:ProgramFiles 'IO Center\hidapi.dll'),
        (Join-Path ${env:ProgramFiles(x86)} 'IO Center\hidapi.dll')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    if (-not $hidApiCandidates) {
        throw 'hidapi.dll was not found. Install or repair be quiet! IO Center, then run this installer again.'
    }
    Copy-Item -LiteralPath $hidApiCandidates[0] -Destination $installedHidApi -Force
}

if ((Test-Path -LiteralPath $sourceBackup) -and -not (Test-Path -LiteralPath $installedBackup)) {
    Copy-Item -LiteralPath $sourceBackup -Destination $installedBackup
}

& (Join-Path $appRoot 'LOKWODVisitorKey.exe') --migrate-key
if ($LASTEXITCODE -ne 0) {
    Write-Warning 'The old lower Display Key could not be restored yet. The companion will retry when the next visitor arrives.'
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $appRoot 'LOKWODVisitorKey.exe'
$shortcut.WorkingDirectory = $appRoot
$shortcut.Description = 'LOKWOD Visitor Key for be quiet! Dark Mount'
$shortcut.Save()

Start-Process -FilePath (Join-Path $appRoot 'LOKWODVisitorKey.exe') -WorkingDirectory $appRoot

Write-Host "Installed: $appRoot"
Write-Host "Startup shortcut: $shortcutPath"
Write-Host 'LOKWOD Visitor Key v1.0.3 is running.'
