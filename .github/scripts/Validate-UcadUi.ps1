$ErrorActionPreference = 'Stop'

$manifest = Get-Content src/UCAD.App/app.manifest -Raw
if ($manifest -notmatch 'PerMonitorV2') { throw 'UCAD must declare PerMonitorV2.' }

$xaml = Get-Content src/UCAD.App/MainWindow.xaml -Raw
if ($xaml -match '<TitleBar\b|TitleBar\.Content') { throw 'Shell must use the explicit browser-style title strip, not TitleBar.Content.' }
foreach ($required in @('TitleDragRegion','DocumentTabs','PageOverlay','SettingsButton')) {
  if ($xaml -notmatch [regex]::Escape($required)) { throw "Missing shell contract element: $required" }
}

$allXaml = (Get-ChildItem src/UCAD.App -Recurse -Filter '*.xaml' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
foreach ($fake in @('╱','⌁','▭','○','◜','↖','✥','⧉','▧','⬚','↗','＋','⇆','⌇','⌗','⌖','◇','◎','◫')) {
  if ($allXaml.Contains($fake)) { throw "Unicode placeholder icon remains in production XAML: $fake" }
}

$tokens = Get-Content src/UCAD.App/Styles/UcadDesignTokens.xaml -Raw
foreach ($required in @('UcadTitleBarHeight','UcadCategoryBarHeight','UcadToolShelfHeight','UcadDocumentTabWidth','UcadSettingsNavWidth','UcadSettingsCardWidth','UcadSettingsCardHeight','UcadSettingsTitleToSectionSpacing','UcadSettingsSectionToCardSpacing','UcadSettingsCardSpacing','UcadSettingsSectionSpacing')) {
  if ($tokens -notmatch [regex]::Escape($required)) { throw "Missing Figma design token: $required" }
}
foreach ($pair in @(@('UcadDocumentTabWidth','190'),@('UcadSettingsNavWidth','228'),@('UcadSettingsCardWidth','940'),@('UcadSettingsCardHeight','72'),@('UcadSettingsTitleToSectionSpacing','35'),@('UcadSettingsSectionToCardSpacing','12'),@('UcadSettingsCardSpacing','8'),@('UcadSettingsSectionSpacing','30'),@('UcadRadiusCard','7'))) {
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
  '<SolidColorBrush x:Key="UcadAccentSelectedBrush" Color="#1F5275" />',
  '<SolidColorBrush x:Key="UcadCategorySelectedBrush" Color="#1C4257" />',
  '<SolidColorBrush x:Key="ToggleButtonBackgroundChecked" Color="#1C4257" />'
)) {
  if (-not $tokens.Contains($visualContract)) { throw "Figma visual token mismatch: $visualContract" }
}

$version = (Get-Content VERSION -Raw).Trim()
$release = Get-Content release/release.json -Raw | ConvertFrom-Json
[xml]$package = Get-Content src/UCAD.App/Package.appxmanifest -Raw
if ($version -ne '0.3.10') { throw "Expected VERSION 0.3.10, got $version" }
if ($release.product.version -ne $version) { throw 'release.json version must match VERSION.' }
if ($release.product.packageVersion -ne "$version.0") { throw 'release packageVersion must be VERSION + .0.' }
if ($package.Package.Identity.Version -ne "$version.0") { throw 'MSIX Identity.Version must match VERSION + .0.' }
$project = Get-Content src/UCAD.App/UCAD.App.csproj -Raw
if ($project -match '<Version>|<AssemblyVersion>|<FileVersion>|<InformationalVersion>') { throw 'UCAD.App.csproj must not hardcode version metadata.' }

foreach ($view in @('src/UCAD.App/Views/StartPage.xaml','src/UCAD.App/Views/UcadSettingsPage.xaml','src/UCAD.App/Views/UcadSettingsPage.xaml.cs','src/UCAD.App/Controls/SettingCard.xaml')) {
  if (-not (Test-Path $view)) { throw "Missing UI foundation file: $view" }
}

$mainCode = Get-Content src/UCAD.App/MainWindow.xaml.cs -Raw
foreach ($functionalContract in @(
  '_settingsService.Settings.ShowStartOnNewTab',
  'CreateNewWorkspace();',
  'DocumentTabWidth',
  'SettingsService_SettingsChanged'
)) {
  if (-not $mainCode.Contains($functionalContract)) { throw "Missing shell behavior contract: $functionalContract" }
}
if ($mainCode -notmatch 'ShowStartOnNewTab[\s\S]{0,500}CreateStartTab\(\)[\s\S]{0,500}CreateNewWorkspace\(\)') {
  throw 'The + button must route to Start or a blank Drawing according to ShowStartOnNewTab.'
}

$viewportCode = Get-Content src/UCAD.App/Views/CadViewport.xaml.cs -Raw
foreach ($functionalContract in @('settings.CanvasTheme','_geometryColor','_transientColor','_gridBaseColor','_crosshairColor')) {
  if (-not $viewportCode.Contains($functionalContract)) { throw "Canvas theme is not wired to the runtime drawing palette: $functionalContract" }
}

$startCode = Get-Content src/UCAD.App/Views/StartPage.xaml -Raw
if ($startCode -notmatch 'x:Name="RecentShowAllButton"[^>]*IsEnabled="False"') {
  throw 'Recent Show All must remain disabled until real recent-file storage exists.'
}

$settingsCode = Get-Content src/UCAD.App/Views/UcadSettingsPage.xaml.cs -Raw
foreach ($section in @('BuildGeneral','BuildAppearance','BuildDrafting','BuildInput','BuildFiles','BuildLanguage','BuildAbout')) {
  if ($settingsCode -notmatch [regex]::Escape($section)) { throw "Missing Settings section: $section" }
}
foreach ($functionalContract in @('disabledValues: new HashSet<string>(StringComparer.Ordinal) { "RestoreSession" }','enabled: false','displayLanguage.IsEnabled = !value','TokenDouble("UcadSettingsCardWidth")')) {
  if (-not $settingsCode.Contains($functionalContract)) { throw "Missing Settings behavior contract: $functionalContract" }
}

$localizationCode = Get-Content src/UCAD.App/Services/LocalizationService.cs -Raw
foreach ($contract in @(
  'MrtResourceLoader.GetDefaultResourceFilePath()',
  'new MrtResourceLoader(MrtResourceLoader.GetDefaultResourceFilePath(), mapName)',
  'ApplicationLanguages.PrimaryLanguageOverride',
  '_loaders.Clear()',
  'IsSettingsLanguageApplied',
  'ApplyLanguagePreference'
)) {
  if (-not $localizationCode.Contains($contract)) { throw "Missing live localization contract: $contract" }
}
if ($localizationCode -match 'new MrtResourceLoader\("UcadV039"\)') {
  throw 'Do not use the single-argument ResourceLoader constructor for the UcadV039 named map.'
}

$liveUiCode = Get-Content src/UCAD.App/MainWindow.Localization.cs -Raw
foreach ($contract in @(
  'ApplyLiveLocalizationFromSettings',
  'RefreshLocalization()',
  '_startPage?.RefreshLocalization()',
  '_settingsPage?.RefreshLocalization()',
  'UpdateDisplayName',
  'Localization smoke: zh-CN -> ja-JP -> en-US refreshed without restart'
)) {
  if (-not $liveUiCode.Contains($contract)) { throw "Missing live UI refresh contract: $contract" }
}
$appCode = Get-Content src/UCAD.App/App.xaml.cs -Raw
foreach ($contract in @('SettingsService.Current.SettingsChanged','ApplyLiveLocalizationFromSettings','ScheduleLocalizationSmoke')) {
  if (-not $appCode.Contains($contract)) { throw "App does not wire live localization: $contract" }
}

$locales = @('zh-CN','ja-JP','en-US')
$defaultSets = @{}
foreach ($locale in $locales) {
  $path = "src/UCAD.App/Strings/$locale/Resources.resw"
  [xml]$xml = Get-Content $path -Raw
  $defaultSets[$locale] = @($xml.root.data | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
}
$baseline = $defaultSets['zh-CN']
foreach ($locale in $locales) {
  $missing = @($baseline | Where-Object { $_ -notin $defaultSets[$locale] })
  $extra = @($defaultSets[$locale] | Where-Object { $_ -notin $baseline })
  if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
    throw ('Default RESW key mismatch in {0}. Missing: {1}; Extra: {2}' -f $locale, ($missing -join ', '), ($extra -join ', '))
  }
}

$v039 = @{}
foreach ($locale in $locales) {
  $path = "src/UCAD.App/Strings/$locale/UcadV039.resw"
  if (-not (Test-Path $path)) { throw "Missing Start/Settings RESW: $path" }
  [xml]$xml = Get-Content $path -Raw
  $v039[$locale] = @($xml.root.data | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
}
$v039Baseline = $v039['zh-CN']
foreach ($locale in $locales) {
  $missing = @($v039Baseline | Where-Object { $_ -notin $v039[$locale] })
  $extra = @($v039[$locale] | Where-Object { $_ -notin $v039Baseline })
  if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
    throw ('Start/Settings RESW key mismatch in {0}. Missing: {1}; Extra: {2}' -f $locale, ($missing -join ', '), ($extra -join ', '))
  }
}
foreach ($required in @('Start_TabTitle','Settings_TabTitle','Settings_General_Title','Settings_Appearance_Title','Settings_Drafting_Title','Settings_Input_Title','Settings_Files_Title','Settings_Language_Title','Settings_About_Title')) {
  if ($required -notin $v039Baseline) { throw "Missing required localized UI key: $required" }
}

$representatives = @{
  'zh-CN' = @{ Start_TabTitle='开始'; Settings_Nav_Title='设置' }
  'ja-JP' = @{ Start_TabTitle='スタート'; Settings_Nav_Title='設定' }
  'en-US' = @{ Start_TabTitle='Start'; Settings_Nav_Title='Settings' }
}
foreach ($locale in $locales) {
  [xml]$xml = Get-Content "src/UCAD.App/Strings/$locale/UcadV039.resw" -Raw
  foreach ($key in $representatives[$locale].Keys) {
    $node = @($xml.root.data | Where-Object { $_.name -eq $key }) | Select-Object -First 1
    if (-not $node -or [string]$node.value -ne $representatives[$locale][$key]) {
      throw "Representative translation mismatch in $locale for $key"
    }
  }
}

Write-Output "Validated v0.3.10 UI/behavior contracts, PMv2, version SSOT, Figma tokens, real named-map localization, hot refresh wiring, and $($v039Baseline.Count) Start/Settings keys in zh-CN/ja-JP/en-US."
