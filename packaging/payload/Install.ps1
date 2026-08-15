[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$payloadRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$metadataPath = Join-Path $payloadRoot 'InstallerMetadata.json'
$logRoot = Join-Path $env:LOCALAPPDATA 'UCAD\Logs'
$logPath = Join-Path $logRoot 'installer.log'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

function Write-InstallerLog([string]$Message) {
    $line = '[{0:yyyy-MM-dd HH:mm:ss}] {1}' -f (Get-Date), $Message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    Write-Host $Message
}

function Download-File([string]$Uri, [string]$Destination) {
    Write-InstallerLog "Downloading $Uri"
    Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $Destination -Headers @{ 'User-Agent' = 'UCAD-OneClickInstaller' }
}

$tempRoot = $null
try {
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $certificatePath = Join-Path $payloadRoot $metadata.certificateFileName
    if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) { throw 'Release certificate is missing from the installer.' }
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
    if ($certificate.HasPrivateKey) { throw 'Installer certificate must not contain a private key.' }
    if ($certificate.Subject -cne $metadata.publisher -or $certificate.Thumbprint -cne $metadata.certificateThumbprint) { throw 'Installer certificate identity mismatch.' }

    $tempRoot = Join-Path $env:TEMP ("UCAD-Installer-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $bundlePath = Join-Path $tempRoot $metadata.remoteBundleFileName
    $checksumPath = Join-Path $tempRoot $metadata.checksumFileName

    Write-InstallerLog "UCAD $($metadata.displayVersion) installation started."
    $release = Invoke-RestMethod -Uri $metadata.releaseApiUri -Headers @{ 'User-Agent' = 'UCAD-OneClickInstaller' }
    if ($release.tag_name -ne $metadata.releaseTag) { throw "Release tag mismatch: $($release.tag_name)" }
    $bundleAsset = @($release.assets | Where-Object name -eq $metadata.remoteBundleFileName)
    $checksumAsset = @($release.assets | Where-Object name -eq $metadata.checksumFileName)
    if ($bundleAsset.Count -ne 1 -or $checksumAsset.Count -ne 1) { throw 'Required GitHub Release assets are missing or duplicated.' }
    Download-File $bundleAsset[0].browser_download_url $bundlePath
    Download-File $checksumAsset[0].browser_download_url $checksumPath

    $expectedHash = $null
    foreach ($line in Get-Content -LiteralPath $checksumPath -Encoding ASCII) {
        if ($line -match '^([a-fA-F0-9]{64})\s{2}(.+)$' -and $Matches[2] -eq $metadata.remoteBundleFileName) { $expectedHash = $Matches[1].ToLowerInvariant(); break }
    }
    if (-not $expectedHash) { throw 'No SHA-256 entry was found for the MSIX bundle.' }
    $actualHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) { throw 'MSIX bundle SHA-256 verification failed.' }
    Write-InstallerLog "SHA-256 verified: $actualHash"

    $trusted = Get-ChildItem Cert:\CurrentUser\TrustedPeople | Where-Object Thumbprint -eq $metadata.certificateThumbprint | Select-Object -First 1
    if (-not $trusted) {
        Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
        Write-InstallerLog "Trusted release certificate for current user: $($metadata.certificateThumbprint)"
    }

    $signature = Get-AuthenticodeSignature -FilePath $bundlePath
    if (-not $signature.SignerCertificate) { throw 'Signed MSIX bundle has no signer certificate.' }
    if ($signature.SignerCertificate.Thumbprint -cne $metadata.certificateThumbprint) { throw 'MSIX signer certificate does not match installer metadata.' }
    if ($signature.Status -ne 'Valid') { throw "MSIX signature validation failed: $($signature.Status) / $($signature.StatusMessage)" }

    $existing = Get-AppxPackage -Name $metadata.packageIdentityName -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1
    if ($existing -and [version]$existing.Version -eq [version]$metadata.packageVersion) {
        Write-InstallerLog "UCAD $($metadata.packageVersion) is already installed."
    }
    else {
        Add-AppxPackage -Path $bundlePath -ForceApplicationShutdown
        Write-InstallerLog 'MSIX package installed successfully.'
    }

    $installed = Get-AppxPackage -Name $metadata.packageIdentityName -ErrorAction Stop | Sort-Object Version -Descending | Select-Object -First 1
    if ([version]$installed.Version -ne [version]$metadata.packageVersion) { throw "Installed package version mismatch: $($installed.Version)" }
    Start-Process explorer.exe "shell:AppsFolder\$($installed.PackageFamilyName)!$($metadata.applicationId)"
    Write-InstallerLog 'UCAD installation completed successfully.'
}
catch {
    Write-InstallerLog ("ERROR: " + $_.Exception.Message)
    Write-Error $_
    exit 1
}
finally {
    if ($tempRoot -and (Test-Path -LiteralPath $tempRoot)) { Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
exit 0
