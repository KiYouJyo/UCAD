param(
  [Parameter(Mandatory=$true)][string]$ExePath,
  [Parameter(Mandatory=$true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class UcadCaptureNative {
  [DllImport("user32.dll", SetLastError=true)] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$settingsRoot = Join-Path $env:LOCALAPPDATA 'UCAD'
$settingsPath = Join-Path $settingsRoot 'settings.json'
New-Item -ItemType Directory -Force -Path $settingsRoot | Out-Null

function Write-CaptureSettings([string]$startup, [string]$language) {
  $settings = [ordered]@{
    StartupBehavior = $startup
    ShowStartOnNewTab = $true
    ConfirmUnsaved = $true
    AutoCheckUpdates = $false
    AppTheme = 'Dark'
    CanvasTheme = 'Dark'
    CanvasBackground = '#0E1012'
    ShowGrid = $true
    GridOpacity = 22
    UiScale = 'System'
    LengthUnit = 'Millimeters'
    Precision = '0.00'
    AngleUnit = 'DecimalDegrees'
    DefaultObjectSnap = $true
    DefaultSnapTypes = 'EndpointMidpointIntersection'
    DefaultOrtho = $false
    ZoomAroundCursor = $true
    MiddleMousePan = $true
    ReverseWheelZoom = $false
    WindowCrossingSelection = 'CadStandard'
    SelectionPreview = $true
    CommandSuggestions = $true
    AutoSave = $true
    AutoSaveIntervalMinutes = 10
    BackupOnSave = $true
    ShowRecentFiles = $true
    RecentFileCount = 20
    DisplayLanguage = $language
    FollowSystemLanguage = $false
    NumberFormat = 'System'
    UnitDisplay = 'Metric'
    AngleDecimalFormat = 'Automatic'
  }
  $settings | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding UTF8
}

function Click-At([int]$x, [int]$y) {
  [UcadCaptureNative]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 150
  [UcadCaptureNative]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero)
  [UcadCaptureNative]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero)
  Start-Sleep -Milliseconds 700
}

function Capture-Screen([string]$path) {
  $bitmap = New-Object System.Drawing.Bitmap 1440,900
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $graphics.CopyFromScreen(0,0,0,0,$bitmap.Size)
    $bitmap.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
  } finally {
    $graphics.Dispose()
    $bitmap.Dispose()
  }
}

function Start-Ucad([string]$startup, [string]$language) {
  Write-CaptureSettings $startup $language
  Remove-Item Env:UCAD_STARTUP_SMOKE -ErrorAction SilentlyContinue
  $process = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path $ExePath) -PassThru
  $deadline = [DateTime]::UtcNow.AddSeconds(20)
  do {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    if ($process.HasExited) { throw "UCAD exited during screenshot startup with code $($process.ExitCode)." }
  } until ($process.MainWindowHandle -ne 0 -or [DateTime]::UtcNow -gt $deadline)
  if ($process.MainWindowHandle -eq 0) { Stop-Process -Id $process.Id -Force; throw 'UCAD did not expose a main window handle.' }
  [UcadCaptureNative]::MoveWindow($process.MainWindowHandle,0,0,1440,900,$true) | Out-Null
  [UcadCaptureNative]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
  Start-Sleep -Seconds 2
  return $process
}

function Stop-Ucad($process) {
  if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
  Start-Sleep -Milliseconds 500
}

function Capture-View([string]$name, [string]$startup, [Nullable[int]]$settingsNavY, [string]$language = 'zh-CN') {
  $process = $null
  try {
    $process = Start-Ucad $startup $language
    if ($settingsNavY.HasValue) {
      # Figma Settings frames are entered from a drawing tab. The shared category-bar
      # gear is 36 px wide immediately to the right of the 172 px command search.
      Click-At 1412 66
      if ($settingsNavY.Value -gt 0) { Click-At 110 $settingsNavY.Value }
    }
    Start-Sleep -Seconds 1
    Capture-Screen (Join-Path $OutputDirectory "$name.png")
  } finally {
    Stop-Ucad $process
  }
}

# Required zh-CN fidelity set.
Capture-View 'drawing' 'BlankDrawing' $null 'zh-CN'
Capture-View 'start' 'StartPage' $null 'zh-CN'
Capture-View 'settings-general' 'BlankDrawing' 0 'zh-CN'
Capture-View 'settings-appearance' 'BlankDrawing' 212 'zh-CN'
Capture-View 'settings-input' 'BlankDrawing' 296 'zh-CN'
Capture-View 'settings-about' 'BlankDrawing' 870 'zh-CN'

# Localization layout smoke: real packaged runtime, same 1440x900 viewport.
Capture-View 'start-ja' 'StartPage' $null 'ja-JP'
Capture-View 'settings-general-ja' 'BlankDrawing' 0 'ja-JP'
Capture-View 'start-en' 'StartPage' $null 'en-US'
Capture-View 'settings-general-en' 'BlankDrawing' 0 'en-US'

$files = Get-ChildItem $OutputDirectory -Filter '*.png'
if ($files.Count -ne 10) { throw "Expected 10 screenshots, produced $($files.Count)." }
foreach ($file in $files) {
  $image = [System.Drawing.Image]::FromFile($file.FullName)
  try {
    if ($image.Width -ne 1440 -or $image.Height -ne 900) { throw "Unexpected screenshot size for $($file.Name): $($image.Width)x$($image.Height)" }
  } finally { $image.Dispose() }
}
Write-Output "Captured ten 1440x900 UCAD fidelity/localization screenshots to $OutputDirectory."
