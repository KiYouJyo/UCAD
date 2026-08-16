$ErrorActionPreference = 'Stop'

function Assert-Contains([string]$Text, [string[]]$Needles, [string]$Scope) {
  foreach ($needle in $Needles) {
    if (-not $Text.Contains($needle)) { throw "$Scope contract missing: $needle" }
  }
}

# v0.4.0 deliberately freezes the v0.3.x shell geometry while interaction work proceeds.
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
Assert-Contains $tokens @(
  '<SolidColorBrush x:Key="UcadAppBackgroundBrush" Color="#18181A" />',
  '<SolidColorBrush x:Key="UcadCardBrush" Color="#222225" />',
  '<SolidColorBrush x:Key="UcadAccentSelectedBrush" Color="#1F5275" />'
) 'Frozen Figma palette'

# Version SSOT.
$version = (Get-Content VERSION -Raw).Trim()
$release = Get-Content release/release.json -Raw | ConvertFrom-Json
[xml]$package = Get-Content src/UCAD.App/Package.appxmanifest -Raw
if ($version -ne '0.4.0') { throw "Expected VERSION 0.4.0, got $version" }
if ($release.product.version -ne $version) { throw 'release.json version must match VERSION.' }
if ($release.product.packageVersion -ne "$version.0") { throw 'release packageVersion must be VERSION + .0.' }
if ($package.Package.Identity.Version -ne "$version.0") { throw 'MSIX Identity.Version must match VERSION + .0.' }
$project = Get-Content src/UCAD.App/UCAD.App.csproj -Raw
if ($project -match '<Version>|<AssemblyVersion>|<FileVersion>|<InformationalVersion>') {
  throw 'UCAD.App.csproj must consume root VERSION instead of hardcoding version metadata.'
}

# Required v0.4 files / boundaries.
foreach ($path in @(
  'src/UCAD.App/MainWindow.Interaction.cs',
  'src/UCAD.App/MainWindow.Localization.cs',
  'src/UCAD.App/Services/LocalizationService.cs',
  'src/UCAD.App/Views/CadViewport.SelectionSemantics.cs',
  'src/UCAD.Core/Geometry/CadRect.cs',
  'src/UCAD.Core/Interaction/CadEntityGeometry.cs',
  'src/UCAD.Core/Interaction/CadInteractionState.cs',
  'src/UCAD.Core/Interaction/CadSelectionQuery.cs',
  'src/UCAD.Core/Interaction/ObjectSnap.cs',
  'src/UCAD.Core/Interaction/OrthoConstraint.cs',
  'src/UCAD.Core/Interaction/SelectionSet.cs'
)) {
  if (-not (Test-Path $path)) { throw "Missing v0.4 foundation file: $path" }
}

$viewport = Get-Content src/UCAD.App/Views/CadViewport.xaml.cs -Raw
Assert-Contains $viewport @(
  'CadSelectionQuery.HitTestNearest','CadSelectionQuery.QueryWindow','ObjectSnapResolver.Resolve',
  'OrthoConstraint.Apply','_interaction.Selection','DrawSelectionGrips','DrawSelectionWindow','DrawSnapMarker',
  'settings.SelectionPreview','ObjectSnapAperturePixels / _zoom'
) 'Viewport interaction'

$selectionSemantics = Get-Content src/UCAD.App/Views/CadViewport.SelectionSemantics.cs -Raw
Assert-Contains $selectionSemantics @('SelectedIds.ToArray()','Selection.Add(previous)','PointerReleasedEvent') 'Additive selection'

$interaction = Get-Content src/UCAD.App/MainWindow.Interaction.cs -Raw
Assert-Contains $interaction @(
  'EnsureInteractionUiInitialized','ScheduleInteractionSmoke',
  'VirtualKey.F3','VirtualKey.F8','VirtualKey.Delete','FocusManager.GetFocusedElement',
  'ObjectSnapEnabled','OrthoEnabled','ObjectSnapKind.Center',
  'RefreshInspectorSelection','ExecuteEraseSelection','RemoveRange(selectedIds)',
  'HasRegisteredCategory','CadCommandCategory.Modify','CadCommandCategory.View',
  'CommandSession.ActiveCommand?.Name == "ERASE"',
  'Interaction smoke: Selection + ERASE + OSNAP + ORTHO + Inspector initialized'
) 'Shell interaction'

$workspace = Get-Content src/UCAD.App/Workspace/CadWorkspaceSession.cs -Raw
Assert-Contains $workspace @(
  'CadInteractionState','ApplyDraftingDefaults','DefaultObjectSnap','DefaultSnapTypes','DefaultOrtho','ObjectSnapMode.Center'
) 'Workspace interaction'

$registry = Get-Content src/UCAD.Core/Commands/CommandRegistry.cs -Raw
Assert-Contains $registry @('"ERASE"','"E"','"DELETE"','CadCommandCategory.Edit') 'ERASE registry'

$document = Get-Content src/UCAD.Core/CadDocument.cs -Raw
Assert-Contains $document @('RemoveRange(IEnumerable<Guid> ids)','CadDocumentChangeKind.RemoveRange','RecordMutation()') 'Undoable ERASE'

$commandSession = Get-Content src/UCAD.Core/Commands/CommandSession.cs -Raw
Assert-Contains $commandSession @('event EventHandler? Changed','Changed?.Invoke') 'Observable CommandSession'

# Settings remain honest while the three drafting defaults now initialize real v0.4 state.
$startXaml = Get-Content src/UCAD.App/Views/StartPage.xaml -Raw
if ($startXaml -notmatch 'x:Name="RecentShowAllButton"[^>]*IsEnabled="False"') {
  throw 'Recent Show All must remain disabled until real recent-file storage exists.'
}
$settings = Get-Content src/UCAD.App/Views/UcadSettingsPage.xaml.cs -Raw
Assert-Contains $settings @(
  'BuildGeneral','BuildAppearance','BuildDrafting','BuildInput','BuildFiles','BuildLanguage','BuildAbout',
  'disabledValues: new HashSet<string>(StringComparer.Ordinal) { "RestoreSession" }',
  'displayLanguage.IsEnabled = !value','TokenDouble("UcadSettingsCardWidth")'
) 'Settings'

# v0.3.10 no-restart localization must survive v0.4 interaction changes.
$localization = Get-Content src/UCAD.App/Services/LocalizationService.cs -Raw
Assert-Contains $localization @(
  'new ResourceManager()','CreateResourceContext()','KnownResourceQualifierName.Language',
  'TryGetSubtree(mapName)','TryGetValue(key, _resourceContext!)','ValueAsString',
  'ResolveSystemLanguage()','ApplyLanguagePreference','ShellLiveMapName','GetShellString',
  'CoreSnapOptionKey','Endpoint / Midpoint / Center / Intersection',
  'StatusEraseNothing','StatusEraseCountFormat'
) 'Localization'
if ($localization -match 'PrimaryLanguageOverride\s*=') { throw 'Live localization must not mutate PrimaryLanguageOverride.' }
if ($localization -match 'new\s+ResourceLoader\s*\(\s*"UcadV039"\s*\)') { throw 'Broken UcadV039 ResourceLoader pattern returned.' }

$liveUi = Get-Content src/UCAD.App/MainWindow.Localization.cs -Raw
Assert-Contains $liveUi @(
  'ApplyLiveLocalizationFromSettings','RefreshLocalization()','ShellString(',
  '_startPage?.RefreshLocalization()','_settingsPage?.RefreshLocalization()',
  'UpdateDisplayName','Localization smoke: zh-CN -> ja-JP -> en-US refreshed without restart'
) 'Live localization UI'
if ($liveUi -match 'GetString\("[^"]+\.(Content|Text|PlaceholderText)"\)') {
  throw "Hot refresh must not request x:Uid property resources: $($Matches[0])"
}

$app = Get-Content src/UCAD.App/App.xaml.cs -Raw
Assert-Contains $app @(
  'SettingsService.Current.SettingsChanged','ApplyLiveLocalizationFromSettings','mainWindow.RefreshLocalization()',
  'EnsureInteractionUiInitialized','ScheduleLocalizationSmoke','ScheduleInteractionSmoke'
) 'App startup'

# Resource-map parity + representative values.
$locales = @('zh-CN','ja-JP','en-US')
$maps = @('Resources','UcadV039','ShellLive')
$keySets = @{}
foreach ($mapName in $maps) {
  $keySets[$mapName] = @{}
  foreach ($locale in $locales) {
    $path = "src/UCAD.App/Strings/$locale/$mapName.resw"
    [xml]$xml = Get-Content $path -Raw
    $keySets[$mapName][$locale] = @($xml.root.data | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
  }
  $baseline = $keySets[$mapName]['zh-CN']
  foreach ($locale in $locales) {
    $missing = @($baseline | Where-Object { $_ -notin $keySets[$mapName][$locale] })
    $extra = @($keySets[$mapName][$locale] | Where-Object { $_ -notin $baseline })
    if ($missing.Count -or $extra.Count) {
      throw ('{0} key mismatch in {1}. Missing: {2}; Extra: {3}' -f $mapName,$locale,($missing -join ', '),($extra -join ', '))
    }
  }
}

$v039Expected = @{
  'zh-CN' = @{ Start_TabTitle='开始'; Settings_Nav_Title='设置' }
  'ja-JP' = @{ Start_TabTitle='スタート'; Settings_Nav_Title='設定' }
  'en-US' = @{ Start_TabTitle='Start'; Settings_Nav_Title='Settings' }
}
$shellExpected = @{
  'zh-CN' = @{ File='文件'; CategoryDraw='绘图'; InspectorProperties='属性'; StatusOsnapOnMessage='对象捕捉已开启（F3）' }
  'ja-JP' = @{ File='ファイル'; CategoryDraw='作図'; InspectorProperties='プロパティ'; StatusOsnapOnMessage='オブジェクトスナップをオン（F3）' }
  'en-US' = @{ File='File'; CategoryDraw='Draw'; InspectorProperties='Properties'; StatusOsnapOnMessage='Object snap on (F3)' }
}
foreach ($locale in $locales) {
  [xml]$v039 = Get-Content "src/UCAD.App/Strings/$locale/UcadV039.resw" -Raw
  [xml]$shell = Get-Content "src/UCAD.App/Strings/$locale/ShellLive.resw" -Raw
  foreach ($key in $v039Expected[$locale].Keys) {
    $node = @($v039.root.data | Where-Object { $_.name -eq $key }) | Select-Object -First 1
    if (-not $node -or [string]$node.value -ne $v039Expected[$locale][$key]) { throw "UcadV039 translation mismatch: $locale / $key" }
  }
  foreach ($key in $shellExpected[$locale].Keys) {
    $node = @($shell.root.data | Where-Object { $_.name -eq $key }) | Select-Object -First 1
    if (-not $node -or [string]$node.value -ne $shellExpected[$locale][$key]) { throw "ShellLive translation mismatch: $locale / $key" }
  }
}

Write-Output "Validated v0.4.0 Selection/ERASE/OSNAP/Ortho/Inspector contracts, PMv2, version SSOT, frozen Figma tokens, and live zh-CN/ja-JP/en-US resources."
