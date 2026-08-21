using Microsoft.UI.Xaml.Controls;

namespace UCAD;

public sealed partial class MainWindow
{
    private bool _advancedAuthoringToolbarBridgeInitialized;

    internal void EnsureAdvancedAuthoringToolbarBridgeInitialized()
    {
        if (_advancedAuthoringToolbarBridgeInitialized) return;
        _advancedAuthoringToolbarBridgeInitialized = true;

        AddVisibleDrawCommand("HATCHADV", "HA");
        AddVisibleDrawCommand("HATCHEDIT", "HE");
    }

    private void AddVisibleDrawCommand(string command, string alias)
    {
        if (DrawToolShelf.Children
            .OfType<Button>()
            .Any(button => string.Equals(button.Tag?.ToString(), command, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        DrawToolShelf.Children.Add(CreateShelfButton(command, alias));
    }
}
