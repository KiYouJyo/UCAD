$ErrorActionPreference = 'Stop'

function Assert-Contains([string]$Text, [string[]]$Needles, [string]$Scope) {
  foreach ($needle in $Needles) {
    if (-not $Text.Contains($needle)) { throw "$Scope contract missing: $needle" }
  }
}

# Keep the already accepted shell / HiDPI baseline while v0.4.1 focuses on CAD interaction.
$manifest = Get-Content src/UCAD.App/app.manifest -Raw
if ($manifest -notmatch 'PerMonitorV2') { throw 'UCAD must declare PerMonitorV2.' }

$xaml = Get-Content src/UCAD.App/MainWindow.xaml -Raw
if ($xaml -match '<TitleBar\b|TitleBar\.Content') { throw 'Shell must keep the explicit browser-style title strip.' }
Assert-Contains $xaml @('TitleDragRegion','DocumentTabs','PageOverlay','SettingsButton') 'Shell'

$allXaml = (Get-ChildItem src/UCAD.App -Recurse -Filter '*.xaml' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
foreach ($fake in @('╱','⌁','▭','○','◜','↖','✥','⧉','▧','⬚','↗','＋','⇆','⌇','⌗','⌖','◇','◎','◫')) {
  if ($allXaml.Contains($fake)) { throw "Unicode placeholder icon remains in production XAML: $fake" }
}

$tokens = Get-Content src/UCAD.App/Styles/UcadDesignTokens.xaml -Raw
foreach ($pair in @(
  @('UcadDocumentTabWidth','190'), @('UcadSettingsNavWidth','228'),
  @('UcadSettingsCardWidth','940'), @('UcadSettingsCardHeight','72'),
  @('UcadSettingsTitleToSectionSpacing','35'), @('UcadSettingsSectionToCardSpacing','12'),
  @('UcadSettingsCardSpacing','8'), @('UcadSettingsSectionSpacing','30'), @('UcadRadiusCard','7')
)) {
  $needle = ('x:Key="{0}">{1}<' -f $pair[0], $pair[1])
  if (-not $tokens.Contains($needle)) { throw "Frozen Figma token mismatch: $($pair[0])" }
}

# Version SSOT.
$version = (Get-Content VERSION -Raw).Trim()
$release = Get-Content release/release.json -Raw | ConvertFrom-Json
[xml]$package = Get-Content src/UCAD.App/Package.appxmanifest -Raw
if ($version -ne '0.4.1') { throw "Expected VERSION 0.4.1, got $version" }
if ($release.product.version -ne $version) { throw 'release.json version must match VERSION.' }
if ($release.product.packageVersion -ne "$version.0") { throw 'release packageVersion must be VERSION + .0.' }
if ($package.Package.Identity.Version -ne "$version.0") { throw 'MSIX Identity.Version must match VERSION + .0.' }
$project = Get-Content src/UCAD.App/UCAD.App.csproj -Raw
if ($project -match '<Version>|<AssemblyVersion>|<FileVersion>|<InformationalVersion>') {
  throw 'UCAD.App.csproj must consume root VERSION instead of hardcoding version metadata.'
}

foreach ($path in @(
  'src/UCAD.App/Interop/TransparentInputCursor.cs',
  'src/UCAD.App/MainWindow.Interaction.cs',
  'src/UCAD.App/MainWindow.Localization.cs',
  'src/UCAD.App/Services/AppSettings.cs',
  'src/UCAD.App/Services/LocalizationService.cs',
  'src/UCAD.App/Views/CadViewport.SelectionSemantics.cs',
  'src/UCAD.App/Views/UcadSettingsPage.CadPointer.cs',
  'src/UCAD.Core/Geometry/CadRect.cs',
  'src/UCAD.Core/Interaction/CadEntityGeometry.cs',
  'src/UCAD.Core/Interaction/CadInteractionState.cs',
  'src/UCAD.Core/Interaction/CadSelectionQuery.cs',
  'src/UCAD.Core/Interaction/ObjectSnap.cs',
  'src/UCAD.Core/Interaction/OrthoConstraint.cs',
  'src/UCAD.Core/Interaction/SelectionSet.cs'
)) {
  if (-not (Test-Path $path)) { throw "Missing interaction foundation file: $path" }
}

# Viewport rendering/input contracts.
$viewport = Get-Content src/UCAD.App/Views/CadViewport.xaml.cs -Raw
Assert-Contains $viewport @(
  'CadSelectionQuery.HitTestNearest','ObjectSnapResolver.Resolve','OrthoConstraint.Apply',
  'DrawSelectionGrips','DrawSelectionWindow','DrawSnapMarker','DrawCadCursor',
  '_selectionWindowArmed','ArmTwoClickSelectionWindow','CommitSelectionWindow',
  '_crosshairSizePercent','_pickboxSizePixels','_objectSnapAperturePixels / _zoom',
  'settings.CrosshairSizePercent','settings.PickboxSize','settings.ObjectSnapAperture'
) 'Viewport interaction'

# The native Windows pointer must be suppressed through the XAML cursor system itself,
# not by racing WinUI with SetCursor/WM_SETCURSOR hooks.
$selectionSemantics = Get-Content src/UCAD.App/Views/CadViewport.SelectionSemantics.cs -Raw
Assert-Contains $selectionSemantics @(
  'ProtectedCursor = TransparentInputCursor.GetOrCreate()',
  'VirtualKeyModifiers.Shift','CadSelectionQuery.QueryWindow',
  'ArmTwoClickSelectionWindow','CommitSelectionWindow',
  '_interaction.Selection.Add(ids)','_interaction.Selection.Remove(ids)',
  'ApplyPointSelection','CancelSelectionGesture'
) 'CAD selection/cursor semantics'
if ($selectionSemantics -match 'SetCursor\(|WM_SETCURSOR|InputSystemCursorShape\.Cross') {
  throw 'CAD viewport must not layer or race a native system cursor over the Win2D CAD cursor.'
}
if (Test-Path 'src/UCAD.App/Views/CadViewport.NativeCursorWindowSubclass.cs') {
  throw 'Obsolete WM_SETCURSOR subclass workaround must remain removed.'
}

$transparentCursor = Get-Content src/UCAD.App/Interop/TransparentInputCursor.cs -Raw
Assert-Contains $transparentCursor @(
  'CreateCursor(','IInputCursorStaticsInterop','CreateFromHCursor',
  'WinRT.MarshalInspectable<InputCursor>.FromAbi','Array.Fill(andPlane, (byte)0xFF)',
  'DestroyCursor(hCursor)'
) 'Transparent WinUI InputCursor'

$selectionSet = Get-Content src/UCAD.Core/Interaction/SelectionSet.cs -Raw
Assert-Contains $selectionSet @('Remove(IEnumerable<Guid> ids)','_selectedIds.Remove(id)') 'Shift-style selection removal'

$interaction = Get-Content src/UCAD.App/MainWindow.Interaction.cs -Raw
Assert-Contains $interaction @(
  'VirtualKey.F3','VirtualKey.F8','VirtualKey.Delete','VirtualKey.Escape',
  'session.Viewport.CancelSelectionGesture()','ExecuteEraseSelection','RemoveRange(selectedIds)',
  'TransparentInputCursor.GetOrCreate()',
  'Interaction smoke: Selection + ERASE + OSNAP + ORTHO + Inspector + transparent CAD cursor initialized'
) 'Shell interaction'

$appSettings = Get-Content src/UCAD.App/Services/AppSettings.cs -Raw
Assert-Contains $appSettings @(
  'CrosshairSizePercent { get; set; } = 100',
  'PickboxSize { get; set; } = 10',
  'ObjectSnapAperture { get; set; } = 10'
) 'CAD pointer defaults'

$cursorSettings = Get-Content src/UCAD.App/Views/UcadSettingsPage.CadPointer.cs -Raw
Assert-Contains $cursorSettings @(
  'CrosshairSizePercent','PickboxSize','ObjectSnapAperture','NumericSlider',
  '中心拾取框大小','3–20 px','CAD カーソル','CAD cursor','CAD 光标'
) 'CAD cursor settings'

# Existing v0.4.0 interaction foundation must not regress.
$workspace = Get-Content src/UCAD.App/Workspace/CadWorkspaceSession.cs -Raw
Assert-Contains $workspace @('CadInteractionState','DefaultObjectSnap','DefaultSnapTypes','DefaultOrtho','ObjectSnapMode.Center') 'Workspace interaction'
$registry = Get-Content src/UCAD.Core/Commands/CommandRegistry.cs -Raw
Assert-Contains $registry @('"ERASE"','"E"','"DELETE"','CadCommandCategory.Edit') 'ERASE registry'
$document = Get-Content src/UCAD.Core/CadDocument.cs -Raw
Assert-Contains $document @('RemoveRange(IEnumerable<Guid> ids)','CadDocumentChangeKind.RemoveRange','RecordMutation()') 'Undoable ERASE'

# Start/Settings honesty boundaries remain.
$startXaml = Get-Content src/UCAD.App/Views/StartPage.xaml -Raw
if ($startXaml -notmatch 'x:Name="RecentShowAllButton"[^>]*IsEnabled="False"') {
  throw 'Recent Show All must remain disabled until real recent-file storage exists.'
}
$settings = Get-Content src/UCAD.App/Views/UcadSettingsPage.xaml.cs -Raw
Assert-Contains $settings @(
  'BuildGeneral','BuildAppearance','BuildDrafting','BuildInput','BuildFiles','BuildLanguage','BuildAbout',
  'disabledValues: new HashSet<string>(StringComparer.Ordinal) { "RestoreSession" }',
  'displayLanguage.IsEnabled = !value'
) 'Settings'

# v0.3.10 restart-free localization must survive interaction changes.
$localization = Get-Content src/UCAD.App/Services/LocalizationService.cs -Raw
Assert-Contains $localization @(
  'new ResourceManager()','CreateResourceContext()','KnownResourceQualifierName.Language',
  'TryGetSubtree(mapName)','TryGetValue(key, _resourceContext!)','ApplyLanguagePreference',
  'ShellLiveMapName','GetShellString','Endpoint / Midpoint / Center / Intersection'
) 'Localization'
if ($localization -match 'PrimaryLanguageOverride\s*=') { throw 'Live localization must not mutate PrimaryLanguageOverride.' }

$liveUi = Get-Content src/UCAD.App/MainWindow.Localization.cs -Raw
Assert-Contains $liveUi @(
  'ApplyLiveLocalizationFromSettings','_startPage?.RefreshLocalization()',
  '_settingsPage?.RefreshLocalization()','Localization smoke: zh-CN -> ja-JP -> en-US refreshed without restart'
) 'Live localization UI'

# All three resource maps must retain key parity in the three supported languages.
$locales = @('zh-CN','ja-JP','en-US')
foreach ($mapName in @('Resources','UcadV039','ShellLive')) {
  $keySets = @{}
  foreach ($locale in $locales) {
    $path = "src/UCAD.App/Strings/$locale/$mapName.resw"
    if (-not (Test-Path $path)) { throw "Missing localized resource map: $path" }
    [xml]$xml = Get-Content $path -Raw
    $keySets[$locale] = @($xml.root.data | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
  }
  $baseline = $keySets['zh-CN']
  foreach ($locale in $locales) {
    $missing = @($baseline | Where-Object { $_ -notin $keySets[$locale] })
    $extra = @($keySets[$locale] | Where-Object { $_ -notin $baseline })
    if ($missing.Count -or $extra.Count) {
      throw ('{0} key mismatch in {1}. Missing: {2}; Extra: {3}' -f $mapName,$locale,($missing -join ', '),($extra -join ', '))
    }
  }
}

Write-Output 'Validated v0.4.1 two-click Window/Crossing, Shift removal, transparent WinUI ProtectedCursor + Win2D CAD cursor, adjustable pickbox/aperture, v0.4 interaction foundation, PMv2, version SSOT, frozen Figma tokens, and live trilingual resources.'
