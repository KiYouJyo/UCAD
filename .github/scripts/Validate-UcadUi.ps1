$ErrorActionPreference = 'Stop'

function Assert-Contains([string]$Text, [string[]]$Needles, [string]$Scope) {
  foreach ($needle in $Needles) {
    if (-not $Text.Contains($needle)) { throw "$Scope contract missing: $needle" }
  }
}

# Keep the accepted shell / HiDPI baseline while v0.5-v0.7 complete the first authoring loop.
$manifest = Get-Content src/UCAD.App/app.manifest -Raw
if ($manifest -notmatch 'PerMonitorV2') { throw 'UCAD must declare PerMonitorV2.' }

$xaml = Get-Content src/UCAD.App/MainWindow.xaml -Raw
if ($xaml -match '<TitleBar\b|TitleBar\.Content') { throw 'Shell must keep the explicit browser-style title strip.' }
Assert-Contains $xaml @('TitleDragRegion','DocumentTabs','PageOverlay','SettingsButton') 'Shell'

$allXaml = (Get-ChildItem src/UCAD.App -Recurse -File -Filter '*.xaml' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
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
if ($version -ne '0.8.0') { throw "Expected VERSION 0.8.0, got $version" }
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
  'src/UCAD.App/MainWindow.Modify.cs',
  'src/UCAD.App/MainWindow.ModifyShell.cs',
  'src/UCAD.App/MainWindow.ModifySmoke.cs',
  'src/UCAD.App/MainWindow.Authoring.cs',
  'src/UCAD.App/MainWindow.AuthoringSmoke.cs',
  'src/UCAD.App/Services/AppSettings.cs',
  'src/UCAD.App/Services/LocalizationService.cs',
  'src/UCAD.App/Views/CadViewport.SelectionSemantics.cs',
  'src/UCAD.App/Views/CadViewport.ModifyInput.cs',
  'src/UCAD.App/Views/CadViewport.AuthoringRender.cs',
  'src/UCAD.App/Views/UcadSettingsPage.CadPointer.cs',
  'src/UCAD.Core/Geometry/CadRect.cs',
  'src/UCAD.Core/Interaction/CadEntityGeometry.cs',
  'src/UCAD.Core/Interaction/CadInteractionState.cs',
  'src/UCAD.Core/Interaction/CadSelectionQuery.cs',
  'src/UCAD.Core/Interaction/ObjectSnap.cs',
  'src/UCAD.Core/Interaction/OrthoConstraint.cs',
  'src/UCAD.Core/Interaction/SelectionSet.cs',
  'src/UCAD.Core/Layers/CadLayer.cs',
  'src/UCAD.Core/Layers/CadEntityProperties.cs',
  'src/UCAD.Core/Entities/TextEntity.cs',
  'src/UCAD.Core/Entities/LinearDimensionEntity.cs',
  'src/UCAD.Core/Entities/HatchEntity.cs',
  'src/UCAD.Core/Entities/BlockReferenceEntity.cs',
  'src/UCAD.Core/Blocks/CadBlockDefinition.cs',
  'src/UCAD.Core/Blocks/CadBlockFactory.cs',
  'src/UCAD.Core/Modify/CadEntityTransform.cs',
  'src/UCAD.Core/Modify/CadOffset.cs',
  'src/UCAD.Core/Modify/CadTrimExtend.cs',
  'tests/UCAD.Core.Tests/ModifyTests.cs',
  'tests/UCAD.Core.Tests/LayerPropertyTests.cs',
  'tests/UCAD.Core.Tests/AuthoringTests.cs',
  '.github/workflows/modify-smoke.yml',
  '.github/workflows/authoring-smoke.yml'
)) {
  if (-not (Test-Path $path)) { throw "Missing v0.5-v0.7 foundation file: $path" }
}

# v0.4 viewport rendering/input contracts must survive later authoring work.
$viewport = Get-Content src/UCAD.App/Views/CadViewport.xaml.cs -Raw
Assert-Contains $viewport @(
  'CadSelectionQuery.HitTestNearest','ObjectSnapResolver.Resolve','OrthoConstraint.Apply',
  'DrawSelectionGrips','DrawSelectionWindow','DrawSnapMarker','DrawCadCursor',
  '_selectionWindowArmed','ArmTwoClickSelectionWindow','CommitSelectionWindow',
  '_crosshairSizePercent','_pickboxSizePixels','_objectSnapAperturePixels / _zoom',
  'settings.CrosshairSizePercent','settings.PickboxSize','settings.ObjectSnapAperture'
) 'Viewport interaction'

$selectionSemantics = Get-Content src/UCAD.App/Views/CadViewport.SelectionSemantics.cs -Raw
Assert-Contains $selectionSemantics @(
  'ProtectedCursor = TransparentInputCursor.GetOrCreate()',
  'EnsureModifyInputHooks()',
  'VirtualKeyModifiers.Shift','CadSelectionQuery.QueryWindow',
  'ArmTwoClickSelectionWindow','CommitSelectionWindow',
  '_interaction.Selection.Add(ids)','_interaction.Selection.Remove(ids)',
  '_document.SelectableEntities','ApplyPointSelection','CancelSelectionGesture'
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
Assert-Contains $selectionSet @(
  'Remove(IEnumerable<Guid> ids)','_selectedIds.Remove(id)',
  '_document.SelectableEntities','RemoveWhere'
) 'Layer-aware selection'

$interaction = Get-Content src/UCAD.App/MainWindow.Interaction.cs -Raw
Assert-Contains $interaction @(
  'VirtualKey.F3','VirtualKey.F8','VirtualKey.Delete','VirtualKey.Escape',
  'session.Viewport.CancelSelectionGesture()','ExecuteEraseSelection','RemoveRange(selectedIds)',
  'HandleModifyCommandSessionChanged(session)','ActivateModifyToolSurfaces()',
  'TransparentInputCursor.GetOrCreate()',
  'Interaction smoke: Selection + ERASE + OSNAP + ORTHO + Inspector + transparent CAD cursor initialized'
) 'Shell interaction'

$registry = Get-Content src/UCAD.Core/Commands/CommandRegistry.cs -Raw
Assert-Contains $registry @(
  '"MOVE"','"M"','"COPY"','"CO"','"ROTATE"','"RO"','"SCALE"','"SC"',
  '"MIRROR"','"MI"','"OFFSET"','"O"','"TRIM"','"TR"','"EXTEND"','"EX"',
  '"HATCH"','"TEXT"','"DIM"','"LAYER"','"CHPROP"','"BLOCK"','"INSERT"','"EXPLODE"',
  'CadCommandCategory.Modify','CadCommandCategory.Annotate','CadCommandCategory.Layer','CadCommandCategory.Block',
  '"ERASE"','"E"','"DELETE"','CadCommandCategory.Edit'
) 'Command registry'

$document = Get-Content src/UCAD.Core/CadDocument.cs -Raw
Assert-Contains $document @(
  'RemoveRange(IEnumerable<Guid> ids)','CadDocumentChangeKind.RemoveRange',
  'Replace(Guid id, IEnumerable<ICadEntity> replacements)',
  'ReplaceRange(IEnumerable<ICadEntity> replacements)','CadDocumentChangeKind.ReplaceRange','RecordMutation()',
  'IReadOnlyList<CadLayer> Layers','CurrentLayerName','VisibleEntities','SelectableEntities',
  'CreateLayer(','DeleteLayer(','RenameLayer(','UpdateLayer(','SetCurrentLayer(',
  'SetEntityProperties(','SetEntitiesLayer(','SetEntitiesColor(','SetEntitiesLineWeight(','SetEntitiesLineType(',
  'IReadOnlyList<CadBlockDefinition> Blocks','DefineBlock(','DeleteBlock('
) 'Undoable document authoring state'

$layer = Get-Content src/UCAD.Core/Layers/CadLayer.cs -Raw
Assert-Contains $layer @('DefaultLayerName = "0"','ColorHex','LineWeight','LineType','IsVisible','IsLocked') 'Layer model'
$properties = Get-Content src/UCAD.Core/Layers/CadEntityProperties.cs -Raw
Assert-Contains $properties @('LayerName','ColorHex','LineWeight','LineType','ByLayer') 'Entity properties model'

$transform = Get-Content src/UCAD.Core/Modify/CadEntityTransform.cs -Raw
Assert-Contains $transform @(
  'Translate(ICadEntity entity','Rotate(ICadEntity entity','Scale(ICadEntity entity','Mirror(ICadEntity entity',
  'TextEntity','LinearDimensionEntity','HatchEntity','BlockReferenceEntity','preserveIdentity = true','MirrorArc'
) 'Shared entity transforms'

$offset = Get-Content src/UCAD.Core/Modify/CadOffset.cs -Raw
Assert-Contains $offset @('OffsetLine','OffsetPolyline','OffsetCircle','OffsetArc','TryCreate') 'OFFSET geometry'
$trimExtend = Get-Content src/UCAD.Core/Modify/CadTrimExtend.cs -Raw
Assert-Contains $trimExtend @('TryTrim(','TryExtend(','TrimLine','TrimPolyline','TrimCircle','TrimArc','ExtendLine','ExtendPolyline','ExtendArc','RayIntersections') 'TRIM/EXTEND geometry'

$modifyViewport = Get-Content src/UCAD.App/Views/CadViewport.ModifyInput.cs -Raw
Assert-Contains $modifyViewport @('BeginModifyPointInput','BeginModifyEntityPickInput','ModifyPointAccepted','ModifyEntityPicked','ObjectSnapResolver.Resolve','OrthoConstraint.Apply','DrawModifyPreview','DrawModifySnapMarker') 'Modify viewport input'
$modifyShell = Get-Content src/UCAD.App/MainWindow.Modify.cs -Raw
Assert-Contains $modifyShell @('HandleModifyCommandSessionChanged','BeginModifyCommand','BeginSelectionBackedModify','CommitMove','CommitCopy','CommitRotation','CommitScale','CommitMirror','CommitOffset','CadTrimExtend.TryTrim','CadTrimExtend.TryExtend','CadEntityTransform.Translate','CadEntityTransform.Rotate','CadEntityTransform.Scale','CadEntityTransform.Mirror','CadOffset.TryCreate') 'Modify command controller'
$modifySmoke = Get-Content src/UCAD.App/MainWindow.ModifySmoke.cs -Raw
Assert-Contains $modifySmoke @('MOVE','COPY','ROTATE','SCALE','MIRROR','OFFSET','TRIM','EXTEND','Modify smoke: MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND initialized') 'Modify runtime smoke'

$authoring = Get-Content src/UCAD.App/MainWindow.Authoring.cs -Raw
Assert-Contains $authoring @(
  'EnsureAuthoringUiInitialized','EnsureAuthoringSessionSubscribed','RunTextCommandAsync','RunDimensionCommandAsync',
  'RunHatchCommand','RunBlockCommandAsync','RunInsertCommandAsync','RunExplodeCommand','ShowLayerManagerAsync','ShowEntityPropertiesAsync',
  'CadBlockFactory.CreateReference','CadBlockFactory.Explode','new HatchEntity','new LinearDimensionEntity','new TextEntity'
) 'v0.6-v0.7 authoring controller'
$authoringRender = Get-Content src/UCAD.App/Views/CadViewport.AuthoringRender.cs -Raw
Assert-Contains $authoringRender @(
  'EnsureAuthoringRenderHooks','_document.VisibleEntities','_document.SelectableEntities','ResolveEntityColor',
  'TextEntity','LinearDimensionEntity','HatchEntity','BlockReferenceEntity','DrawAuthoringModifyPreview'
) 'Layer-aware authoring renderer'
$authoringSmoke = Get-Content src/UCAD.App/MainWindow.AuthoringSmoke.cs -Raw
Assert-Contains $authoringSmoke @(
  'LAYERS + PROPERTIES + TEXT + DIM + HATCH + BLOCK + INSERT + EXPLODE initialized',
  'CadLayer','CadEntityProperties','TextEntity','LinearDimensionEntity','HatchEntity','CadBlockDefinition','CadBlockFactory.CreateReference','CadBlockFactory.Explode'
) 'Authoring runtime smoke'

$appSettings = Get-Content src/UCAD.App/Services/AppSettings.cs -Raw
Assert-Contains $appSettings @('CrosshairSizePercent { get; set; } = 100','PickboxSize { get; set; } = 10','ObjectSnapAperture { get; set; } = 10') 'CAD pointer defaults'
$cursorSettings = Get-Content src/UCAD.App/Views/UcadSettingsPage.CadPointer.cs -Raw
Assert-Contains $cursorSettings @('CrosshairSizePercent','PickboxSize','ObjectSnapAperture','NumericSlider','中心拾取框大小','3–20 px','CAD カーソル','CAD cursor','CAD 光标') 'CAD cursor settings'
$workspace = Get-Content src/UCAD.App/Workspace/CadWorkspaceSession.cs -Raw
Assert-Contains $workspace @('CadInteractionState','DefaultObjectSnap','DefaultSnapTypes','DefaultOrtho','ObjectSnapMode.Center') 'Workspace interaction'

# Start/Settings honesty boundaries remain.
$startXaml = Get-Content src/UCAD.App/Views/StartPage.xaml -Raw
if ($startXaml -notmatch 'x:Name="RecentShowAllButton"[^>]*IsEnabled="False"') { throw 'Recent Show All must remain disabled until real recent-file storage exists.' }
$settings = Get-Content src/UCAD.App/Views/UcadSettingsPage.xaml.cs -Raw
Assert-Contains $settings @('BuildGeneral','BuildAppearance','BuildDrafting','BuildInput','BuildFiles','BuildLanguage','BuildAbout','disabledValues: new HashSet<string>(StringComparer.Ordinal) { "RestoreSession" }','displayLanguage.IsEnabled = !value') 'Settings'

# Restart-free localization remains mandatory for all authoring prompts.
$localization = Get-Content src/UCAD.App/Services/LocalizationService.cs -Raw
Assert-Contains $localization @(
  'new ResourceManager()','CreateResourceContext()','KnownResourceQualifierName.Language',
  'TryGetSubtree(mapName)','TryGetValue(key, _resourceContext!)','ApplyLanguagePreference',
  'ShellLiveMapName','AuthoringLiveMapName','GetShellString','Endpoint / Midpoint / Center / Intersection'
) 'Localization'
if ($localization -match 'PrimaryLanguageOverride\s*=') { throw 'Live localization must not mutate PrimaryLanguageOverride.' }
$liveUi = Get-Content src/UCAD.App/MainWindow.Localization.cs -Raw
Assert-Contains $liveUi @('ApplyLiveLocalizationFromSettings','_startPage?.RefreshLocalization()','_settingsPage?.RefreshLocalization()','Localization smoke: zh-CN -> ja-JP -> en-US refreshed without restart') 'Live localization UI'

$locales = @('zh-CN','ja-JP','en-US')
foreach ($mapName in @('Resources','UcadV039','ShellLive','AuthoringLive')) {
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
    if ($missing.Count -or $extra.Count) { throw ('{0} key mismatch in {1}. Missing: {2}; Extra: {3}' -f $mapName,$locale,($missing -join ', '),($extra -join ', ')) }
  }
}

foreach ($key in @('ModifySelectObjects','ModifyBasePoint','ModifyMoveTarget','ModifyCopyTarget','ModifyRotationAngle','ModifyScaleFactor','ModifyMirrorFirstPoint','ModifyMirrorSecondPoint','ModifyOffsetDistance','ModifyTrimPick','ModifyExtendPick')) {
  foreach ($locale in $locales) {
    [xml]$shellLive = Get-Content "src/UCAD.App/Strings/$locale/ShellLive.resw" -Raw
    if (-not ($shellLive.root.data | Where-Object { $_.name -eq $key })) { throw "Missing v0.5 ShellLive key $key in $locale" }
  }
}
foreach ($key in @('LayerManagerTitle','PropertyLayer','TextInsertionPoint','DimFirstPoint','HatchNeedsBoundary','BlockNameTitle','InsertPoint','ExplodeNeedsOneBlock')) {
  foreach ($locale in $locales) {
    [xml]$authoringLive = Get-Content "src/UCAD.App/Strings/$locale/AuthoringLive.resw" -Raw
    if (-not ($authoringLive.root.data | Where-Object { $_.name -eq $key })) { throw "Missing v0.6-v0.7 AuthoringLive key $key in $locale" }
  }
}

Write-Output 'Validated v0.7.0 combined MOVE/COPY/ROTATE/SCALE/MIRROR/OFFSET/TRIM/EXTEND, Layers/Properties, TEXT/DIM/HATCH/BLOCK/INSERT/EXPLODE, layer-aware rendering/selection, authoring runtime smoke, v0.4 cursor contracts, PMv2, version SSOT, frozen Figma tokens, and live trilingual resources.'
