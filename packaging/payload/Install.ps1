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

try {
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { throw 'InstallerMetadata.json is missing.' }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($required in @('displayVersion','releaseTag','releaseApiUri','remoteArchiveFileName','checksumFileName','executableName')) {
        if ([string]::IsNullOrWhiteSpace([string]$metadata.$required)) { throw "Installer metadata is missing: $required" }
    }

    $installRoot = Join-Path $env:LOCALAPPDATA 'Programs\UCAD'
    $tempRoot = Join-Path $env:TEMP ("UCAD-Installer-" + [Guid]::NewGuid().ToString('N'))
    $archivePath = Join-Path $tempRoot $metadata.remoteArchiveFileName
    $checksumPath = Join-Path $tempRoot $metadata.checksumFileName
    $extractRoot = Join-Path $tempRoot 'extracted'
    New-Item -ItemType Directory -Force -Path $tempRoot,$extractRoot | Out-Null

    Write-InstallerLog "UCAD $($metadata.displayVersion) installation started."
    $release = Invoke-RestMethod -Uri $metadata.releaseApiUri -Headers @{ 'User-Agent' = 'UCAD-OneClickInstaller' }
    if ($release.tag_name -ne $metadata.releaseTag) { throw "Release tag mismatch: $($release.tag_name)" }

    $archiveAsset = @($release.assets | Where-Object name -eq $metadata.remoteArchiveFileName)
    $checksumAsset = @($release.assets | Where-Object name -eq $metadata.checksumFileName)
    if ($archiveAsset.Count -ne 1) { throw "Expected exactly one release asset named $($metadata.remoteArchiveFileName)." }
    if ($checksumAsset.Count -ne 1) { throw "Expected exactly one release asset named $($metadata.checksumFileName)." }

    Download-File $archiveAsset[0].browser_download_url $archivePath
    Download-File $checksumAsset[0].browser_download_url $checksumPath

    $expectedHash = $null
    foreach ($line in Get-Content -LiteralPath $checksumPath -Encoding ASCII) {
        if ($line -match '^([a-fA-F0-9]{64})\s{2}(.+)$' -and $Matches[2] -eq $metadata.remoteArchiveFileName) {
            $expectedHash = $Matches[1].ToLowerInvariant()
            break
        }
    }
    if (-not $expectedHash) { throw "No SHA-256 entry found for $($metadata.remoteArchiveFileName)." }
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) { throw "SHA-256 mismatch for $($metadata.remoteArchiveFileName)." }
    Write-InstallerLog "SHA-256 verified: $actualHash"

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
    $sourceExe = Join-Path $extractRoot $metadata.executableName
    if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw "Published application executable is missing: $($metadata.executableName)" }

    Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($metadata.executableName)) -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300

    if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
    Get-ChildItem -LiteralPath $extractRoot -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $installRoot -Recurse -Force }

    $installedExe = Join-Path $installRoot $metadata.executableName
    if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) { throw 'Installed executable verification failed.' }
    Set-Content -LiteralPath (Join-Path $installRoot 'installed-version.txt') -Value $metadata.displayVersion -Encoding ASCII

    $shell = New-Object -ComObject WScript.Shell
    $startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $desktopDir = [Environment]::GetFolderPath('Desktop')
    foreach ($shortcutPath in @((Join-Path $startMenuDir 'UCAD.lnk'), (Join-Path $desktopDir 'UCAD.lnk'))) {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $installedExe
        $shortcut.WorkingDirectory = $installRoot
        $shortcut.IconLocation = "$installedExe,0"
        $shortcut.Description = 'UCAD — Urban Computer-Aided Design'
        $shortcut.Save()
    }

    Write-InstallerLog "Installed to $installRoot"
    Start-Process -FilePath $installedExe -WorkingDirectory $installRoot
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
