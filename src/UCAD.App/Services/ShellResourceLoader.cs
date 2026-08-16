using UCAD.Services;

namespace UCAD
{
    /// <summary>
    /// Compatibility wrapper for existing shell code. The actual resource lifetime and
    /// language context are owned by LocalizationService so live switching cannot leave
    /// a stale ResourceLoader context behind.
    /// </summary>
    internal sealed class ResourceLoader
    {
        public string GetString(string key) => LocalizationService.Current.GetString(key);
    }
}

namespace UCAD.Views
{
    /// <summary>
    /// Compatibility wrapper for Start/Settings, which live in UcadV039.resw.
    /// </summary>
    internal sealed class ResourceLoader
    {
        public ResourceLoader()
        {
        }

        public ResourceLoader(string mapName)
        {
            // mapName is retained for source compatibility; Start/Settings currently use
            // the UcadV039 resource subtree exclusively.
        }

        public string GetString(string key) => LocalizationService.Current.GetV039String(key);
    }
}
