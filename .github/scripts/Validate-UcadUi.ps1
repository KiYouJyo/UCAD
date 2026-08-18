$ErrorActionPreference = 'Stop'

function Assert-Contains([string]$Text, [string[]]$Needles, [string]$Scope) {
  foreach ($needle in $Needles) {
    if (-not $Text.Contains($needle)) { throw "$Scope contract missing: $needle" }
  }
}

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

$iconService = Get-Content src/UCAD.App/Services/CadToolIconService.cs -Raw
Assert-Contains $iconService @('CadToolIconService','PathData','CommandKeys','UcadIconLine','UcadIconDimension','UcadIconBlock','UcadIconStretch','new PathGeometry','new PathFigure','new LineSegment','new BezierSegment') 'CAD icon registry'
Assert-Contains $xaml @('Data="M1,13 L2.5,14.5 L15,2 L13.5,0.5 Z"','Data="M2,3 L3,2 L8,7 L13,2 L14,3 L9,8 L14,13 L13,14 L8,9 L3,14 L2,13 L7,8 Z"') 'Static CAD vector icons'
if ($xaml -match '\{StaticResource UcadIcon|Data="M2,14 L14,2"|Data="M2,13 L6,7 L10,11 L14,3"|Glyph="&#xE7C2;"|Glyph="&#xE8C8;"|Glyph="&#xE78A;"') { throw 'Legacy/shared CAD toolbar icon markup remains.' }

$version = (Get-Content VERSION -Raw).Trim()
$release = Get-Content release/release.json -Raw | ConvertFrom-Json
[xml]$package = Get-Content src/UCAD.App/Package.appxmanifest -Raw
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION must be a three-part numeric version, got $version" }
if ($release.product.version -ne $version) { throw 'release.json version must match VERSION.' }
if ($release.product.packageVersion -ne "$version.0") { throw 'release packageVersion must be VERSION + .0.' }
if ($package.Package.Identity.Version -ne "$version.0") { throw 'MSIX Identity.Version must match VERSION + .0.' }
$project = Get-Content src/UCAD.App/UCAD.App.csproj -Raw
if ($project -match '<Version>|<AssemblyVersion>|<FileVersion>|<InformationalVersion>') { throw 'UCAD.App.csproj must consume root VERSION instead of hardcoding version metadata.' }

foreach ($path in @(
  'src/UCAD.App/Interop/TransparentInputCursor.cs','src/UCAD.App/MainWindow.Interaction.cs','src/UCAD.App/MainWindow.Localization.cs',
  'src/UCAD.App/MainWindow.Modify.cs','src/UCAD.App/MainWindow.ModifyShell.cs','src/UCAD.App/MainWindow.ModifySmoke.cs',
  'src/UCAD.App/MainWindow.Authoring.cs','src/UCAD.App/MainWindow.AuthoringSmoke.cs','src/UCAD.App/MainWindow.AutoCadMigration.cs',
  'src/UCAD.App/Services/AppSettings.cs','src/UCAD.App/Services/LocalizationService.cs',
  'src/UCAD.App/Views/CadViewport.SelectionSemantics.cs','src/UCAD.App/Views/CadViewport.ModifyInput.cs','src/UCAD.App/Views/CadViewport.AuthoringRender.cs',
  'src/UCAD.App/Views/UcadSettingsPage.CadPointer.cs','src/UCAD.Core/Geometry/CadRect.cs','src/UCAD.Core/Interaction/CadEntityGeometry.cs',
  'src/UCAD.Core/Interaction/CadInteractionState.cs','src/UCAD.Core/Interaction/CadSelectionQuery.cs','src/UCAD.Core/Interaction/ObjectSnap.cs',
  'src/UCAD.Core/Interaction/OrthoConstraint.cs','src/UCAD.Core/Interaction/SelectionSet.cs','src/UCAD.Core/Layers/CadLayer.cs',
  'src/UCAD.Core/Layers/CadEntityProperties.cs','src/UCAD.Core/Entities/TextEntity.cs','src/UCAD.Core/Entities/LinearDimensionEntity.cs',
  'src/UCAD.Core/Entities/HatchEntity.cs','src/UCAD.Core/Entities/BlockReferenceEntity.cs','src/UCAD.Core/Blocks/CadBlockDefinition.cs',
  'src/UCAD.Core/Blocks/CadBlockFactory.cs','src/UCAD.Core/Modify/CadEntityTransform.cs','src/UCAD.Core/Modify/CadOffset.cs',
  'src/UCAD.Core/Modify/CadTrimExtend.cs','src/UCAD.Core/IO/CadAcadEcosystemResourceCodec.cs','src/UCAD.Core/IO/CadDwfxCodec.cs',
  'src/UCAD.Core/IO/CadDxfFullInteropCodec.cs','tests/UCAD.Core.Tests/ModifyTests.cs','tests/UCAD.Core.Tests/LayerPropertyTests.cs',
  'tests/UCAD.Core.Tests/AuthoringTests.cs','.github/workflows/modify-smoke.yml','.github/workflows/authoring-smoke.yml'
)) { if (-not (Test-Path $path)) { throw "Missing authoring/interoperability foundation file: $path" } }

$viewport = Get-Content src/UCAD.App/Views/CadViewport.xaml.cs -Raw
Assert-Contains $viewport @('CadSelectionQuery.HitTestNearest','ObjectSnapResolver.Resolve','OrthoConstraint.Apply','DrawSelectionGrips','DrawSelectionWindow','DrawSnapMarker','DrawCadCursor','_selectionWindowArmed','ArmTwoClickSelectionWindow','CommitSelectionWindow','_crosshairSizePercent','_pickboxSizePixels','_objectSnapAperturePixels / _zoom','settings.CrosshairSizePercent','settings.PickboxSize','settings.ObjectSnapAperture') 'Viewport interaction'

$selectionSemantics = Get-Content src/UCAD.App/Views/CadViewport.SelectionSemantics.cs -Raw
Assert-Contains $selectionSemantics @('ProtectedCursor = TransparentInputCursor.GetOrCreate()','EnsureModifyInputHooks()','VirtualKeyModifiers.Shift','CadSelectionQuery.QueryWindow','ArmTwoClickSelectionWindow','CommitSelectionWindow','_interaction.Selection.Add(ids)','_interaction.Selection.Remove(ids)','_document.SelectableEntities','ApplyPointSelection','CancelSelectionGesture') 'CAD selection/cursor semantics'
if ($selectionSemantics -match 'SetCursor\(|WM_SETCURSOR|InputSystemCursorShape\.Cross') { throw 'CAD viewport must not layer or race a native system cursor over the Win2D CAD cursor.' }
if (Test-Path 'src/UCAD.App/Views/CadViewport.NativeCursorWindowSubclass.cs') { throw 'Obsolete WM_SETCURSOR subclass workaround must remain removed.' }

$transparentCursor = Get-Content src/UCAD.App/Interop/TransparentInputCursor.cs -Raw
Assert-Contains $transparentCursor @('CreateCursor(','IInputCursorStaticsInterop','CreateFromHCursor','WinRT.MarshalInspectable<InputCursor>.FromAbi','Array.Fill(andPlane, (byte)0xFF)','DestroyCursor(hCursor)') 'Transparent WinUI InputCursor'

$selectionSet = Get-Content src/UCAD.Core/Interaction/SelectionSet.cs -Raw
Assert-Contains $selectionSet @('Remove(IEnumerable<Guid> ids)','_selectedIds.Remove(id)','_document.SelectableEntities','RemoveWhere') 'Layer-aware selection'

$interaction = Get-Content src/UCAD.App/MainWindow.Interaction.cs -Raw
Assert-Contains $interaction @('VirtualKey.F3','VirtualKey.F8','VirtualKey.Delete','VirtualKey.Escape','session.Viewport.CancelSelectionGesture()','ExecuteEraseSelection','RemoveRange(selectedIds)','HandleModifyCommandSessionChanged(session)','ActivateModifyToolSurfaces()','TransparentInputCursor.GetOrCreate()','Interaction smoke: Selection + ERASE + OSNAP + ORTHO + Inspector + transparent CAD cursor initialized') 'Shell interaction'

$modifyShellRuntime = Get-Content src/UCAD.App/MainWindow.ModifyShell.cs -Raw
Assert-Contains $modifyShellRuntime @('new KeyboardAccelerator','Key = VirtualKey.Delete','RootLayout.KeyboardAccelerators.Add(_deleteDrawingAccelerator)','DeleteDrawingAccelerator_Invoked','TryExecuteDeleteShortcut','IsTextEditingFocus','StartToolbarCommand("ERASE")','EnsureAutoCadMigrationUi()') 'Focus-independent Delete accelerator'

$registry = Get-Content src/UCAD.Core/Commands/CommandRegistry.cs -Raw
Assert-Contains $registry @('"MOVE"','"M"','"COPY"','"CO"','"ROTATE"','"RO"','"SCALE"','"SC"','"MIRROR"','"MI"','"OFFSET"','"O"','"TRIM"','"TR"','"EXTEND"','"EX"','"HATCH"','"TEXT"','"DIM"','"LAYER"','"CHPROP"','"BLOCK"','"INSERT"','"EXPLODE"','CadCommandCategory.Modify','CadCommandCategory.Annotate','CadCommandCategory.Layer','CadCommandCategory.Block','"ERASE"','"E"','"DELETE"','CadCommandCategory.Edit') 'Command registry'

$document = Get-Content src/UCAD.Core/CadDocument.cs -Raw
Assert-Contains $document @('RemoveRange(IEnumerable<Guid> ids)','CadDocumentChangeKind.RemoveRange','Replace(Guid id, IEnumerable<ICadEntity> replacements)','ReplaceRange(IEnumerable<ICadEntity> replacements)','CadDocumentChangeKind.ReplaceRange','RecordMutation()','IReadOnlyList<CadLayer> Layers','CurrentLayerName','VisibleEntities','SelectableEntities','CreateLayer(','DeleteLayer(','RenameLayer(','UpdateLayer(','SetCurrentLayer(','SetEntityProperties(','SetEntitiesLayer(','SetEntitiesColor(','SetEntitiesLineWeight(','SetEntitiesLineType(','IReadOnlyList<CadBlockDefinition> Blocks','DefineBlock(','DeleteBlock(') 'Undoable document authoring state'

$layer = Get-Content src/UCAD.Core/Layers/CadLayer.cs -Raw
Assert-Contains $layer @('DefaultLayerName = "0"','ColorHex','LineWeight','LineType','IsVisible','IsLocked') 'Layer model'
$properties = Get-Content src/UCAD.Core/Layers/CadEntityProperties.cs -Raw
Assert-Contains $properties @('LayerName','ColorHex','LineWeight','LineType','ByLayer') 'Entity properties model'

$transform = Get-Content src/UCAD.Core/Modify/CadEntityTransform.cs -Raw
Assert-Contains $transform @('Translate(ICadEntity entity','Rotate(ICadEntity entity','Scale(ICadEntity entity','Mirror(ICadEntity entity','TextEntity','LinearDimensionEntity','HatchEntity','BlockReferenceEntity','preserveIdentity = true','MirrorArc') 'Shared entity transforms'
$offset = Get-Content src/UCAD.Core/Modify/CadOffset.cs -Raw
Assert-Contains $offset @('OffsetLine','OffsetPolyline','OffsetCircle','OffsetArc','TryCreate') 'OFFSET geometry'
$trimExtend = Get-Content src/UCAD.Core/Modify/CadTrimExtend.cs -Raw
Assert-Contains $trimExtend @('TryTrim(','TryExtend(','TrimLine','TrimPolyline','TrimCircle','TrimArc','ExtendLine','ExtendPolyline','ExtendArc','RayIntersections') 'TRIM/EXTEND geometry'

$modifyViewport = Get-Content src/UCAD.App/Views/CadViewport.ModifyInput.cs -Raw
Assert-Contains $modifyViewport @('BeginModifyPointInput','BeginModifyEntityPickInput','ModifyPointAccepted','ModifyEntityPicked','ObjectSnapResolver.Resolve','OrthoConstraint.Apply','DrawModifyPreview','DrawModifySnapMarker') 'Modify viewport input'
$modifyShell = Get-Content src/UCAD.App/MainWindow.Modify.cs -Raw
Assert-Contains $modifyShell @('HandleModifyCommandSessionChanged','BeginModifyCommand','BeginSelectionBackedModify','CommitMove','CommitCopy','CommitRotation','CommitScale','CommitMirror','CommitOffset','CadTrimExtend.TryTrim','CadTrimExtend.TryExtend','CadEntityTransform.Translate','CadEntityTransform.Rotate','CadEntityTransform.Scale','CadEntityTransform.Mirror','CadOffset.TryCreate') 'Modify command controller'
$modifySmoke = Get-Content src/UCAD.App/MainWindow.ModifySmoke.cs -Raw
Assert-Contains $modifySmoke @('MOVE','COPY','ROTATE','SCALE','MIRROR','OFFSET','TRIM','EXTEND','VirtualKey.Delete','TryExecuteDeleteShortcut()','Modify smoke: physical Delete accelerator + ERASE + MOVE + COPY + ROTATE + SCALE + MIRROR + OFFSET + TRIM + EXTEND initialized') 'Modify runtime smoke'

$authoring = Get-Content src/UCAD.App/MainWindow.Authoring.cs -Raw
Assert-Contains $authoring @('EnsureAuthoringUiInitialized','EnsureAuthoringSessionSubscribed','RunTextCommandAsync','RunDimensionCommandAsync','RunHatchCommand','RunBlockCommandAsync','RunInsertCommandAsync','RunExplodeCommand','ShowLayerManagerAsync','ShowEntityPropertiesAsync','CadBlockFactory.CreateReference','CadBlockFactory.Explode','new HatchEntity','new LinearDimensionEntity','new TextEntity') 'v0.6-v0.7 authoring controller'
$authoringRender = Get-Content src/UCAD.App/Views/CadViewport.AuthoringRender.cs -Raw
Assert-Contains $authoringRender @('EnsureAuthoringRenderHooks','_document.VisibleEntities','_document.SelectableEntities','ResolveEntityColor','TextEntity','LinearDimensionEntity','HatchEntity','BlockReferenceEntity','DrawAuthoringModifyPreview') 'Layer-aware authoring renderer'
$authoringSmoke = Get-Content src/UCAD.App/MainWindow.AuthoringSmoke.cs -Raw
Assert-Contains $authoringSmoke @('LAYERS + PROPERTIES + TEXT + DIM + HATCH + BLOCK + INSERT + EXPLODE initialized','CadLayer','CadEntityProperties','TextEntity','LinearDimensionEntity','HatchEntity','CadBlockDefinition','CadBlockFactory.CreateReference','CadBlockFactory.Explode') 'Authoring runtime smoke'

$acadMigration = Get-Content src/UCAD.App/MainWindow.AutoCadMigration.cs -Raw
Assert-Contains $acadMigration @('CadAcadFileFormatRegistry.MigratableAutoCadFormats','CadDwfxCodec.Import','ParseScript','AnalyzeLispSource','ImportCuix','ParsePat','ParseLin','ParsePgpAliases','RunSafeAutoCadScript') 'AutoCAD migration UI routing'

$appSettings = Get-Content src/UCAD.App/Services/AppSettings.cs -Raw
Assert-Contains $appSettings @('CrosshairSizePercent { get; set; } = 100','PickboxSize { get; set; } = 10','ObjectSnapAperture { get; set; } = 10') 'CAD pointer defaults'
$cursorSettings = Get-Content src/UCAD.App/Views/UcadSettingsPage.CadPointer.cs -Raw
Assert-Contains $cursorSettings @('CrosshairSizePercent','PickboxSize','ObjectSnapAperture','NumericSlider','中心拾取框大小','3–20 px','CAD カーソル','CAD cursor','CAD 光标') 'CAD cursor settings'
$workspace = Get-Content src/UCAD.App/Workspace/CadWorkspaceSession.cs -Raw
Assert-Contains $workspace @('CadInteractionState','DefaultObjectSnap','DefaultSnapTypes','DefaultOrtho','ObjectSnapMode.Center') 'Workspace interaction'

$startXaml = Get-Content src/UCAD.App/Views/StartPage.xaml -Raw
if ($startXaml -notmatch 'x:Name="RecentShowAllButton"[^>]*IsEnabled="False"') { throw 'Recent Show all must remain disabled until the feature is real.' }

Write-Host "UCAD UI contracts passed for version $version."
