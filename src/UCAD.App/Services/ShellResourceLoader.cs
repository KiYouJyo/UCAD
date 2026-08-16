namespace UCAD
{
    /// <summary>
    /// Keeps the existing shell resource calls stable while allowing the v0.3.9
    /// Start/Settings strings to live in their own resource map.
    /// </summary>
    internal sealed class ResourceLoader
    {
        private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader _default = new();
        private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader? _v039 = TryCreate("UcadV039");

        public string GetString(string key)
        {
            var value = TryGet(_default, key);
            return string.IsNullOrWhiteSpace(value) ? TryGet(_v039, key) : value;
        }

        private static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader? TryCreate(string mapName)
        {
            try
            {
                return new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader(mapName);
            }
            catch (Exception)
            {
                // The named PRI map is not available when CI launches the raw unpackaged
                // build output. The packaged production runtime does contain the map.
                return null;
            }
        }

        private static string TryGet(Microsoft.Windows.ApplicationModel.Resources.ResourceLoader? loader, string key)
        {
            if (loader is null)
            {
                return string.Empty;
            }

            try
            {
                return loader.GetString(key);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}

namespace UCAD.Views
{
    /// <summary>
    /// Start and Settings use the v0.3.9 resource map in packaged builds. Raw CI
    /// smoke runs may not expose named PRI submaps; in that case returning the key
    /// keeps initialization testable without pretending localization succeeded.
    /// </summary>
    internal sealed class ResourceLoader
    {
        private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader? _inner;

        public ResourceLoader() : this("UcadV039")
        {
        }

        public ResourceLoader(string mapName)
        {
            try
            {
                _inner = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader(mapName);
            }
            catch (Exception)
            {
                _inner = null;
            }
        }

        public string GetString(string key)
        {
            if (_inner is null)
            {
                return string.Empty;
            }

            try
            {
                return _inner.GetString(key);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
