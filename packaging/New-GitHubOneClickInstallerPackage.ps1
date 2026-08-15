[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SignedBundlePath,
    [Parameter(Mandatory)][string]$PublicCertificatePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$DisplayVersion,
    [Parameter(Mandatory)][string]$PackageVersion,
    [Parameter(Mandatory)][string]$PackageIdentityName,
    [Parameter(Mandatory)][string]$Publisher,
    [Parameter(Mandatory)][string]$CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
if ($DisplayVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid display version.' }
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
foreach ($path in @($SignedBundlePath, $PublicCertificatePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input: $path" }
}

$bundle = Get-Item -LiteralPath $SignedBundlePath
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path $PublicCertificatePath))
if ($certificate.HasPrivateKey) { throw 'The one-click package must contain only a public certificate.' }
if ($certificate.Subject -cne $Publisher) { throw "Certificate publisher mismatch: $($certificate.Subject)" }
if ($certificate.Thumbprint -cne $CertificateThumbprint) { throw "Certificate thumbprint mismatch: $($certificate.Thumbprint)" }

$root = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) "UCAD-v$DisplayVersion-x64-one-click"
if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
$payload = Join-Path $root 'payload'
New-Item -ItemType Directory -Force -Path $payload | Out-Null

$certificateFileName = "UCAD-v$DisplayVersion-release.cer"
$metadata = [ordered]@{
    schemaVersion = 2
    displayVersion = $DisplayVersion
    packageVersion = $PackageVersion
    releaseTag = "v$DisplayVersion"
    packageIdentityName = $PackageIdentityName
    publisher = $Publisher
    applicationId = 'App'
    architecture = 'x64'
    remoteBundleFileName = $bundle.Name
    certificateFileName = $certificateFileName
    certificateThumbprint = $CertificateThumbprint
    releaseApiUri = "https://api.github.com/repos/KiYouJyo/UCAD/releases/tags/v$DisplayVersion"
    checksumFileName = 'SHA256SUMS.txt'
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8

foreach ($name in @('① 安装UCAD.cmd','② 卸载UCAD.cmd','请先阅读.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $root
}
foreach ($name in @('Install.ps1','Uninstall.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "payload\$name") -Destination $payload
}
Copy-Item -LiteralPath $PublicCertificatePath -Destination (Join-Path $payload $certificateFileName)

$hashLines = Get-ChildItem -LiteralPath $payload -File | Where-Object Name -ne 'PAYLOAD-SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
    "{0} *{1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant(), $_.Name
}
Set-Content -LiteralPath (Join-Path $payload 'PAYLOAD-SHA256SUMS.txt') -Value $hashLines -Encoding ASCII
Write-Output $root
