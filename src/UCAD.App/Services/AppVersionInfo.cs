namespace UCAD.Services;

public static class AppVersionInfo
{
    public const string Channel = "Preview";

    public static string Version
    {
        get
        {
            var version = typeof(AppVersionInfo).Assembly.GetName().Version;
            if (version is null)
            {
                return "0.0.0";
            }

            return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        }
    }

    public static string DisplayVersion => $"v{Version}";

    public static string ProductDisplayVersion => $"UCAD {DisplayVersion}";
}
