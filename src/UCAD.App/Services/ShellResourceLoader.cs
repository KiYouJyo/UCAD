namespace UCAD
{
    /// <summary>
    /// Keeps the existing shell resource calls stable while allowing the v0.3.9
    /// Start/Settings strings to live in their own resource map.
    /// </summary>
    internal sealed class ResourceLoader
    {
        private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader _default = new();
        private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader _v039 = new("UcadV039");

        public string GetString(string key)
        {
            var value = TryGet(_default, key);
            return string.IsNullOrWhiteSpace(value) ? TryGet(_v039, key) : value;
        }

        private static string TryGet(Microsoft.Windows.ApplicationModel.Resources.ResourceLoader loader, string key)
        {
            try
            {
                return loader.GetString(key);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return string.Empty;
            }
        }
    }
}

namespace UCAD.Views
{
    /// <summary>
    /// Start and Settings are backed exclusively by the v0.3.9 resource map.
    /// The same-name type intentionally scopes existing unqualified ResourceLoader
    /// usages in UCAD.Views without spreading map-name literals through view code.
    /// </summary>
    internal sealed class ResourceLoader
    {
        private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader _inner;

        public ResourceLoader() : this("UcadV039")
        {
        }

        public ResourceLoader(string mapName)
        {
            _inner = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader(mapName);
        }

        public string GetString(string key)
        {
            try
            {
                return _inner.GetString(key);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return string.Empty;
            }
        }
    }
}
