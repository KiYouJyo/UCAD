[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [string]$ZipPath
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ReleaseDirectory)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Release directory not found: $root" }

$required = @(
    '① 安装UCAD.cmd',
    '② 卸载UCAD.cmd',
    '请先阅读.txt',
    'payload\Install.ps1',
    'payload\Uninstall.ps1',
    'payload\InstallerMetadata.json'
)
foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing one-click installer file: $relative" }
    if ((Get-Item -LiteralPath $path).Length -le 0) { throw "Empty one-click installer file: $relative" }
}

$metadata = Get-Content -LiteralPath (Join-Path $root 'payload\InstallerMetadata.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($metadata.schemaVersion -ne 1) { throw 'Unexpected installer metadata schema.' }
if ($metadata.architecture -ne 'x64') { throw 'Unexpected installer architecture.' }
if ($metadata.releaseTag -notmatch '^v\d+\.\d+\.\d+$') { throw 'Invalid release tag in installer metadata.' }
if ($metadata.releaseApiUri -ne "https://api.github.com/repos/KiYouJyo/UCAD/releases/tags/$($metadata.releaseTag)") { throw 'Unexpected release API URI.' }
if ($metadata.remoteArchiveFileName -notmatch '^UCAD-v\d+\.\d+\.\d+-win-x64\.zip$') { throw 'Unexpected remote archive name.' }
if ($metadata.checksumFileName -ne 'SHA256SUMS.txt') { throw 'Unexpected checksum manifest name.' }
if ($metadata.executableName -ne 'UCAD.App.exe') { throw 'Unexpected executable name.' }

$installContent = Get-Content -LiteralPath (Join-Path $root 'payload\Install.ps1') -Raw -Encoding UTF8
foreach ($requiredText in @('Get-FileHash','SHA256','Expand-Archive','LOCALAPPDATA','WScript.Shell','Invoke-RestMethod')) {
    if (-not $installContent.Contains($requiredText)) { throw "Installer is missing required behavior marker: $requiredText" }
}

if ($ZipPath) {
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) { throw "ZIP not found: $ZipPath" }
    if ((Get-Item -LiteralPath $ZipPath).Length -le 0) { throw 'One-click ZIP is empty.' }
}

Write-Output "UCAD one-click installer package validation passed: $root"
