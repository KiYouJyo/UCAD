[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseDirectory)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ReleaseDirectory -PathType Container)) { throw 'Release directory not found.' }
$payload = Join-Path $ReleaseDirectory 'payload'
foreach ($file in @('① 安装UCAD.cmd','② 卸载UCAD.cmd','请先阅读.txt','payload\Install.ps1','payload\Uninstall.ps1','payload\InstallerMetadata.json','payload\PAYLOAD-SHA256SUMS.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $ReleaseDirectory $file) -PathType Leaf)) { throw "Missing one-click file: $file" }
}
$metadata = Get-Content -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($name in @('displayVersion','packageVersion','packageIdentityName','publisher','remoteBundleFileName','certificateFileName','certificateThumbprint','releaseApiUri','checksumFileName')) {
    if ([string]::IsNullOrWhiteSpace([string]$metadata.$name)) { throw "Missing metadata field: $name" }
}
$certificatePath = Join-Path $payload $metadata.certificateFileName
if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) { throw 'Public certificate missing.' }
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if ($certificate.HasPrivateKey) { throw 'Public installer certificate unexpectedly contains a private key.' }
if ($certificate.Subject -cne $metadata.publisher -or $certificate.Thumbprint -cne $metadata.certificateThumbprint) { throw 'Public certificate metadata mismatch.' }

$install = Get-Content -LiteralPath (Join-Path $payload 'Install.ps1') -Raw
foreach ($needle in @('Get-FileHash','Import-Certificate','Get-AuthenticodeSignature','Add-AppxPackage','TrustedPeople')) {
    if (-not $install.Contains($needle)) { throw "Install script is missing required behavior: $needle" }
}
$uninstall = Get-Content -LiteralPath (Join-Path $payload 'Uninstall.ps1') -Raw
if (-not $uninstall.Contains('Remove-AppxPackage')) { throw 'Uninstall script must use Remove-AppxPackage.' }

foreach ($line in Get-Content -LiteralPath (Join-Path $payload 'PAYLOAD-SHA256SUMS.txt') -Encoding ASCII) {
    if ($line -notmatch '^([A-F0-9]{64}) \*(.+)$') { throw "Invalid payload checksum line: $line" }
    $file = Join-Path $payload $Matches[2]
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Payload checksum target missing: $($Matches[2])" }
    $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $Matches[1]) { throw "Payload checksum mismatch: $($Matches[2])" }
}
Write-Output 'One-click installer package validation passed.'
