[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$payloadRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$metadata = Get-Content -LiteralPath (Join-Path $payloadRoot 'InstallerMetadata.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$packages = @(Get-AppxPackage -Name $metadata.packageIdentityName -ErrorAction SilentlyContinue)
if ($packages.Count -eq 0) {
    Write-Host 'UCAD is not installed for the current Windows user.'
    exit 0
}
foreach ($package in $packages) {
    Write-Host "Removing $($package.Name) $($package.Version)..."
    Remove-AppxPackage -Package $package.PackageFullName
}
Write-Host 'UCAD was removed. The trusted release certificate is intentionally retained for future UCAD updates.'
exit 0
