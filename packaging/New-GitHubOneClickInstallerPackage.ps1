[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$DisplayVersion,
    [Parameter(Mandatory)][string]$ArchiveName
)

$ErrorActionPreference = 'Stop'
if ($DisplayVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid display version.' }
if ([string]::IsNullOrWhiteSpace($ArchiveName)) { throw 'ArchiveName is required.' }

$root = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) "UCAD-v$DisplayVersion-x64-one-click"
if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
$payload = Join-Path $root 'payload'
New-Item -ItemType Directory -Force -Path $payload | Out-Null

foreach ($name in @('① 安装UCAD.cmd','② 卸载UCAD.cmd')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $root
}
$readme = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '请先阅读.txt') -Encoding UTF8
$readme = $readme.Replace('{{DISPLAY_VERSION}}',$DisplayVersion).Replace('{{ARCHIVE_NAME}}',$ArchiveName)
Set-Content -LiteralPath (Join-Path $root '请先阅读.txt') -Value $readme -Encoding UTF8

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\Install.ps1') -Destination $payload
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload\Uninstall.ps1') -Destination $payload

$metadata = [ordered]@{
    schemaVersion = 1
    displayVersion = $DisplayVersion
    releaseTag = "v$DisplayVersion"
    architecture = 'x64'
    releaseApiUri = "https://api.github.com/repos/KiYouJyo/UCAD/releases/tags/v$DisplayVersion"
    remoteArchiveFileName = $ArchiveName
    checksumFileName = 'SHA256SUMS.txt'
    executableName = 'UCAD.App.exe'
    installScope = 'CurrentUser'
    installDirectory = '%LOCALAPPDATA%\Programs\UCAD'
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8

Write-Output $root
