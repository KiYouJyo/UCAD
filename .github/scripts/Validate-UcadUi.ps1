$ErrorActionPreference = 'Stop'

# Window / HiDPI / shell contracts.
$manifest = Get-Content src/UCAD.App/app.manifest -Raw
if ($manifest -notmatch 'PerMonitorV2') { throw 'UCAD must declare PerMonitorV2.' }

$xaml = Get-Content src/UCAD.App/MainWindow.xaml -Raw
if ($xaml -match '<TitleBar\b|TitleBar\.Content') { throw 'Shell must use the explicit browser-style title strip, not TitleBar.Content.' }
foreach ($required in @('TitleDragRegion','DocumentTabs','PageOverlay','SettingsButton')) {
  if (-not $xaml.Contains($required)) { throw "Missing shell contract element: $required" }
}

$allXaml = (Get-ChildItem src/UCAD.App -Recurse -Filter '*.xaml' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
foreach ($fake in @('╱','⌁','▭','○','◜','↖','✥','⧉','▧','⬚','↗','＋','⇆','⌇','⌗','⌖','◇','◎','◫')) {
  if ($allXaml.Contains($fake)) { throw "Unicode placeholder icon remains in production XAML: $fake" }
}

# Figma-derived token SSOT remains mandatory even though screenshot overlay is non-gating.
$tokens = Get-Content src/UCAD.App/Styles/UcadDesignTokens.xaml -Raw
foreach ($required in @(
  'UcadTitleBarHeight','UcadCategoryBarHeight','UcadToolShelfHeight','UcadDocumentTabWidth',
  'UcadSettingsNavWidth','UcadSettingsCardWidth','UcadSettingsCardHeight',
  'UcadSettingsTitleToSectionSpacing','UcadSettingsSectionToCardSpacing',
  'UcadSettingsCardSpacing','UcadSettingsSectionSpacing'
)) {
  if (-not $tokens.Contains($required)) { throw "Missing Figma design token: $required" }
}
foreach ($pair in @(
  @('UcadDocumentTabWidth','190'), @('UcadSettingsNavWidth','228'),
  @('UcadSettingsCardWidth','940'), @('UcadSettingsCardHeight','72'),
  @('UcadSettingsTitleToSectionSpacing','35'), @('UcadSettingsSectionToCardSpacing','12'),
  @('UcadSettingsCardSpacing','8'), @('UcadSettingsSectionSpacing','30'), @('UcadRadiusCard','7')
)) {
  $needle = ('x:Key="{0}">{1}<' -f $pair[0], $pair[1])
  if (-not $tokens.Contains($needle)) { throw "Figma token mismatch: $($pair[0])" }
}
foreach ($visualContract in @(
  '<SolidColorBrush x:Key="UcadAppBackgroundBrush" Color="#18181A" />',
  '<SolidColorBrush x:Key="UcadTitleBarBrush" Color="#202022" />',
  '<SolidColorBrush x:Key="UcadCategoryBarBrush" Color="#252528" />',
  '<SolidColorBrush x:Key="UcadNavigationBrush" Color="#1D1D20" />',
  '<SolidColorBrush x:Key="UcadCardBrush" Color="#222225" />',
  '<SolidColorBrush x:Key="UcadCardBorderBrush" Color="#99404047" />',
  '<SolidColorBrush x:Key="UcadAccentSelectedBrush" Color="#1F5275" />'
)) {
  if (-not $tokens.Contains($visualContract)) { throw "Figma visual token mismatch: $visualContract" }
}

# Release/version SSOT.
$version = (Get-Content VERSION -Raw).Trim()
$release = Get-Content release/release.json -Raw | ConvertFrom-Json
[xml]$package = Get-Content src/UCAD.App/Package.appxmanifest -Raw
if ($version -ne '0.3.10') { throw "Expected VERSION 0.3.10, got $version" }
if ($release.product.version -ne $version) { throw 'release.json version must match VERSION.' }
if ($release.product.packageVersion -ne "$version.0") { throw 'release packageVersion must be VERSION + .0.' }
if ($package.Package.Identity.Version -ne "$version.0") { throw 'MSIX Identity.Version must match VERSION + .0.' }
$project = Get-Content src/UCAD.App/UCAD.App.csproj -Raw
if ($project -match '<Version>|<AssemblyVersion>|<FileVersion>|<InformationalVersion>') {
  throw 'UCAD.App.csproj must consume root VERSION instead of hardcoding version metadata.'
}

# Page and behavior boundaries.
foreach ($view in @(
  'src/UCAD.App/Views/StartPage.xaml',
  'src/UCAD.App/Views/UcadSettingsPage.xaml',
  'src/UCAD.App/Views/UcadSettingsPage.xaml.cs',
  'src/UCAD.App/Controls/SettingCard.xaml',
  'src/UCAD.App/MainWindow.Localization.cs',
  'src/UCAD.App/Services/LocalizationService.cs'
)) {
  if (-not (Test-Path $view)) { throw "Missing UI/localization foundation file: $view" }
}

$mainCode = Get-Content src/UCAD.App/MainWindow.xaml.cs -Raw
foreach ($contract in @('_settingsService.Settings.ShowStartOnNewTab','CreateNewWorkspace();','DocumentTabWidth','SettingsService_SettingsChanged')) {
  if (-not $mainCode.Contains($contract)) { throw "Missing shell behavior contract: $contract" }
}
if ($mainCode -notmatch 'ShowStartOnNewTab[\s\S]{0,500}CreateStartTab\(\)[\s\S]{0,500}CreateNewWorkspace\(\)') {
  throw 'The + button must route to Start or a blank Drawing according to ShowStartOnNewTab.'
}

$viewportCode = Get-Content src/UCAD.App/Views/CadViewport.xaml.cs -Raw
foreach ($contract in @('settings.CanvasTheme','_geometryColor','_transientColor','_gridBaseColor','_crosshairColor')) {
  if (-not $viewportCode.Contains($contract)) { throw "Canvas theme is not wired to the runtime palette: $contract" }
}

$startXaml = Get-Content src/UCAD.App/Views/StartPage.xaml -Raw
if ($startXaml -notmatch 'x:Name="RecentShowAllButton"[^>]*IsEnabled="False"') {
  throw 'Recent Show All must remain disabled until real recent-file storage exists.'
}

$settingsCode = Get-Content src/UCAD.App/Views/UcadSettingsPage.xaml.cs -Raw
foreach ($section in @('BuildGeneral','BuildAppearance','BuildDrafting','BuildInput','BuildFiles','BuildLanguage','BuildAbout')) {
  if (-not $settingsCode.Contains($section)) { throw "Missing Settings section: $section" }
}
foreach ($contract in @(
  'disabledValues: new HashSet<string>(StringComparer.Ordinal) { "RestoreSession" }',
  'enabled: false','displayLanguage.IsEnabled = !value','TokenDouble("UcadSettingsCardWidth")'
)) {
  if (-not $settingsCode.Contains($contract)) { throw "Missing Settings behavior contract: $contract" }
}

# v0.3.10 localization architecture: explicit MRT ResourceContext, never process-global override.
$localizationCode = Get-Content src/UCAD.App/Services/LocalizationService.cs -Raw
foreach ($contract in @(
  'new ResourceManager()',
  'CreateResourceContext()',
  'KnownResourceQualifierName.Language',
  'TryGetSubtree(mapName)',
  'TryGetValue(key, _resourceContext!)',
  'ValueAsString',
  'ResolveSystemLanguage()',
  'ApplyLanguagePreference',
  'ShellLiveMapName',
  'GetShellString'
)) {
  if (-not $localizationCode.Contains($contract)) { throw "Missing explicit ResourceContext localization contract: $contract" }
}
if ($localizationCode -match 'PrimaryLanguageOverride\s*=') {
  throw 'Live localization must not mutate the process-global PrimaryLanguageOverride.'
}
if ($localizationCode -match 'new\s+ResourceLoader\s*\(\s*"UcadV039"\s*\)') {
  throw 'The broken single-argument UcadV039 ResourceLoader pattern must not return.'
}

$liveUiCode = Get-Content src/UCAD.App/MainWindow.Localization.cs -Raw
foreach ($contract in @(
  'ApplyLiveLocalizationFromSettings', 'RefreshLocalization()', 'ShellString(',
  '_startPage?.RefreshLocalization()', '_settingsPage?.RefreshLocalization()',
  'UpdateDisplayName', 'Localization smoke: zh-CN -> ja-JP -> en-US refreshed without restart'
)) {
  if (-not $liveUiCode.Contains($contract)) { throw "Missing live UI refresh contract: $contract" }
}
if ($liveUiCode -match 'GetString\("[^"]+\.(Content|Text|PlaceholderText)"\)') {
  throw "Hot refresh must not imperatively request x:Uid property resources: $($Matches[0])"
}
$appCode = Get-Content src/UCAD.App/App.xaml.cs -Raw
foreach ($contract in @('SettingsService.Current.SettingsChanged','ApplyLiveLocalizationFromSettings','mainWindow.RefreshLocalization()','ScheduleLocalizationSmoke')) {
  if (-not $appCode.Contains($contract)) { throw "App does not wire live localization: $contract" }
}

# Three languages must have identical keys across all three runtime resource maps.
$locales = @('zh-CN','ja-JP','en-US')
$maps = @('Resources','UcadV039','ShellLive')
$keySets = @{}
foreach ($mapName in $maps) {
  $keySets[$mapName] = @{}
  foreach ($locale in $locales) {
    $path = "src/UCAD.App/Strings/$locale/$mapName.resw"
    if (-not (Test-Path $path)) { throw "Missing localized resource map: $path" }
    [xml]$xml = Get-Content $path -Raw
    $keySets[$mapName][$locale] = @($xml.root.data | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
  }

  $baseline = $keySets[$mapName]['zh-CN']
  foreach ($locale in $locales) {
    $missing = @($baseline | Where-Object { $_ -notin $keySets[$mapName][$locale] })
    $extra = @($keySets[$mapName][$locale] | Where-Object { $_ -notin $baseline })
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
      throw ('{0} key mismatch in {1}. Missing: {2}; Extra: {3}' -f $mapName, $locale, ($missing -join ', '), ($extra -join ', '))
    }
  }
}

$v039Baseline = $keySets['UcadV039']['zh-CN']
foreach ($required in @('Start_TabTitle','Settings_TabTitle','Settings_General_Title','Settings_Appearance_Title','Settings_Drafting_Title','Settings_Input_Title','Settings_Files_Title','Settings_Language_Title','Settings_About_Title')) {
  if ($required -notin $v039Baseline) { throw "Missing required localized UI key: $required" }
}

# Representative strings must be actual translated values, not identifiers echoed into UI.
$v039Representatives = @{
  'zh-CN' = @{ Start_TabTitle='开始'; Settings_Nav_Title='设置' }
  'ja-JP' = @{ Start_TabTitle='スタート'; Settings_Nav_Title='設定' }
  'en-US' = @{ Start_TabTitle='Start'; Settings_Nav_Title='Settings' }
}
$shellRepresentatives = @{
  'zh-CN' = @{ File='文件'; CategoryDraw='绘图'; InspectorProperties='属性' }
  'ja-JP' = @{ File='ファイル'; CategoryDraw='作図'; InspectorProperties='プロパティ' }
  'en-US' = @{ File='File'; CategoryDraw='Draw'; InspectorProperties='Properties' }
}
foreach ($locale in $locales) {
  [xml]$v039Xml = Get-Content "src/UCAD.App/Strings/$locale/UcadV039.resw" -Raw
  [xml]$shellXml = Get-Content "src/UCAD.App/Strings/$locale/ShellLive.resw" -Raw
  foreach ($key in $v039Representatives[$locale].Keys) {
    $node = @($v039Xml.root.data | Where-Object { $_.name -eq $key }) | Select-Object -First 1
    if (-not $node -or [string]$node.value -ne $v039Representatives[$locale][$key]) {
      throw "Representative UcadV039 translation mismatch in $locale for $key"
    }
  }
  foreach ($key in $shellRepresentatives[$locale].Keys) {
    $node = @($shellXml.root.data | Where-Object { $_.name -eq $key }) | Select-Object -First 1
    if (-not $node -or [string]$node.value -ne $shellRepresentatives[$locale][$key]) {
      throw "Representative ShellLive translation mismatch in $locale for $key"
    }
  }
}

Write-Output "Validated v0.3.10 UI/behavior contracts, PMv2, version SSOT, Figma tokens, explicit MRT ResourceContext hot switching, $($v039Baseline.Count) Start/Settings keys, and $($keySets['ShellLive']['zh-CN'].Count) plain ShellLive keys in zh-CN/ja-JP/en-US."
