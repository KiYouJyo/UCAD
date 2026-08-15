[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\UCAD'
$logRoot = Join-Path $env:LOCALAPPDATA 'UCAD\Logs'
$logPath = Join-Path $logRoot 'uninstaller.log'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

function Write-UninstallerLog([string]$Message) {
    $line = '[{0:yyyy-MM-dd HH:mm:ss}] {1}' -f (Get-Date), $Message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    Write-Host $Message
}

try {
    Write-UninstallerLog 'UCAD uninstallation started.'
    Get-Process -Name 'UCAD.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300

    $shortcutPaths = @(
        (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\UCAD.lnk'),
        (Join-Path ([Environment]::GetFolderPath('Desktop')) 'UCAD.lnk')
    )
    foreach ($shortcutPath in $shortcutPaths) {
        if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force }
    }

    if (Test-Path -LiteralPath $installRoot) {
        Remove-Item -LiteralPath $installRoot -Recurse -Force
        Write-UninstallerLog "Removed $installRoot"
    }
    else {
        Write-UninstallerLog 'UCAD installation directory was already absent.'
    }

    Write-UninstallerLog 'UCAD uninstallation completed successfully.'
}
catch {
    Write-UninstallerLog ("ERROR: " + $_.Exception.Message)
    Write-Error $_
    exit 1
}

exit 0
