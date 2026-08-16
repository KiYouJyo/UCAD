namespace UCAD.Core.Interaction;

public sealed class CadInteractionState
{
    private bool _objectSnapEnabled;
    private ObjectSnapMode _objectSnapModes = ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint | ObjectSnapMode.Intersection;
    private bool _orthoEnabled;

    public CadInteractionState(CadDocument document)
    {
        Selection = new SelectionSet(document ?? throw new ArgumentNullException(nameof(document)));
    }

    public event EventHandler? Changed;

    public SelectionSet Selection { get; }

    public bool ObjectSnapEnabled
    {
        get => _objectSnapEnabled;
        set
        {
            if (_objectSnapEnabled == value) return;
            _objectSnapEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public ObjectSnapMode ObjectSnapModes
    {
        get => _objectSnapModes;
        set
        {
            if (_objectSnapModes == value) return;
            _objectSnapModes = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool OrthoEnabled
    {
        get => _orthoEnabled;
        set
        {
            if (_orthoEnabled == value) return;
            _orthoEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
