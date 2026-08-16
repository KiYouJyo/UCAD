namespace UCAD.Services;

public sealed class AppSettings
{
    public string StartupBehavior { get; set; } = "StartPage";
    public bool ShowStartOnNewTab { get; set; } = true;
    public bool ConfirmUnsaved { get; set; } = true;
    public bool AutoCheckUpdates { get; set; }

    public string AppTheme { get; set; } = "System";
    public string CanvasTheme { get; set; } = "Dark";
    public string CanvasBackground { get; set; } = "#0E1012";
    public bool ShowGrid { get; set; } = true;
    public int GridOpacity { get; set; } = 22;
    public string UiScale { get; set; } = "System";

    public string LengthUnit { get; set; } = "Millimeters";
    public string Precision { get; set; } = "0.00";
    public string AngleUnit { get; set; } = "DecimalDegrees";
    public bool DefaultObjectSnap { get; set; } = true;
    public string DefaultSnapTypes { get; set; } = "EndpointMidpointIntersection";
    public bool DefaultOrtho { get; set; }

    public bool ZoomAroundCursor { get; set; } = true;
    public bool MiddleMousePan { get; set; } = true;
    public bool ReverseWheelZoom { get; set; }
    public string WindowCrossingSelection { get; set; } = "CadStandard";
    public bool SelectionPreview { get; set; } = true;
    public bool CommandSuggestions { get; set; } = true;

    // CAD pointer/selection defaults intentionally mirror familiar AutoCAD-style concepts:
    // CURSORSIZE is a drawing-area percentage; PICKBOX and APERTURE are screen-space sizes.
    public int CrosshairSizePercent { get; set; } = 100;
    public int PickboxSize { get; set; } = 10;
    public int ObjectSnapAperture { get; set; } = 10;

    public bool AutoSave { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 10;
    public bool BackupOnSave { get; set; } = true;
    public bool ShowRecentFiles { get; set; } = true;
    public int RecentFileCount { get; set; } = 20;

    public string DisplayLanguage { get; set; } = "System";
    public bool FollowSystemLanguage { get; set; } = true;
    public string NumberFormat { get; set; } = "System";
    public string UnitDisplay { get; set; } = "Metric";
    public string AngleDecimalFormat { get; set; } = "Automatic";
}
