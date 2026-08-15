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
foreach ($pair in @(@('UcadDocumentTabWidth','190'),@('UcadSettingsNavWidth','228'),@('UcadSettingsCardWidth','940'),@('UcadSettingsCardHeight','72'),@('UcadSettingsTitleToSectionSpacing','35'),@('UcadSettingsSectionToCardSpacing','12'),@('UcadSettingsCardSpacing','8'),@('UcadSettingsSectionSpacing','30'))) {
  $needle = ('x:Key="{0}">{1}<' -f $pair[0], $pair[1])
  if (-not $tokens.Contains($needle)) { throw "Figma token mismatch: $($pair[0])" }
}

$version = (Get-Content VERSION -Raw).Trim()
$release = Get-Content release/release.json -Raw | ConvertFrom-Json
[xml]$package = Get-Content src/UCAD.App/Package.appxmanifest -Raw
if ($version -ne '0.3.9') { throw "Expected VERSION 0.3.9, got $version" }
if ($release.product.version -ne $version) { throw 'release.json version must match VERSION.' }
if ($release.product.packageVersion -ne "$version.0") { throw 'release packageVersion must be VERSION + .0.' }
if ($package.Package.Identity.Version -ne "$version.0") { throw 'MSIX Identity.Version must match VERSION + .0.' }
$project = Get-Content src/UCAD.App/UCAD.App.csproj -Raw
if ($project -match '<Version>|<AssemblyVersion>|<FileVersion>|<InformationalVersion>') { throw 'UCAD.App.csproj must not hardcode version metadata.' }

foreach ($view in @('src/UCAD.App/Views/StartPage.xaml','src/UCAD.App/Views/UcadSettingsPage.xaml','src/UCAD.App/Views/UcadSettingsPage.xaml.cs','src/UCAD.App/Controls/SettingCard.xaml')) {
  if (-not (Test-Path $view)) { throw "Missing UI foundation file: $view" }
}
$settingsCode = Get-Content src/UCAD.App/Views/UcadSettingsPage.xaml.cs -Raw
foreach ($section in @('BuildGeneral','BuildAppearance','BuildDrafting','BuildInput','BuildFiles','BuildLanguage','BuildAbout')) {
  if ($settingsCode -notmatch [regex]::Escape($section)) { throw "Missing Settings section: $section" }
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
  if ($missing.Count -gt 0) { throw ('Default RESW keys missing in {0}: {1}' -f $locale, ($missing -join ', ')) }
}

$v039 = @{}
foreach ($locale in $locales) {
  $path = "src/UCAD.App/Strings/$locale/UcadV039.resw"
  if (-not (Test-Path $path)) { throw "Missing v0.3.9 RESW: $path" }
  [xml]$xml = Get-Content $path -Raw
  $v039[$locale] = @($xml.root.data | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
}
$v039Baseline = $v039['zh-CN']
foreach ($locale in $locales) {
  $missing = @($v039Baseline | Where-Object { $_ -notin $v039[$locale] })
  $extra = @($v039[$locale] | Where-Object { $_ -notin $v039Baseline })
  if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
    throw ('v0.3.9 RESW key mismatch in {0}. Missing: {1}; Extra: {2}' -f $locale, ($missing -join ', '), ($extra -join ', '))
  }
}
foreach ($required in @('Start_TabTitle','Settings_TabTitle','Settings_General_Title','Settings_Appearance_Title','Settings_Drafting_Title','Settings_Input_Title','Settings_Files_Title','Settings_Language_Title','Settings_About_Title')) {
  if ($required -notin $v039Baseline) { throw "Missing required localized UI key: $required" }
}

Write-Output "Validated UI fidelity, PMv2, version SSOT, icon rules, and $($v039Baseline.Count) v0.3.9 keys in zh-CN/ja-JP/en-US."
