namespace UCAD.Core.Interaction;

public sealed class CadInteractionState
{
    private bool _objectSnapEnabled;
    private ObjectSnapMode _objectSnapModes = ObjectSnapMode.Endpoint | ObjectSnapMode.Midpoint | ObjectSnapMode.Intersection;
    private bool _orthoEnabled;
    private bool _gridDisplayEnabled = true;
    private bool _gridSnapEnabled;
    private double _gridSnapSpacing = 10;
    private bool _polarTrackingEnabled;
    private double _polarIncrementDegrees = 45;
    private bool _objectSnapTrackingEnabled;

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

    public bool GridDisplayEnabled
    {
        get => _gridDisplayEnabled;
        set
        {
            if (_gridDisplayEnabled == value) return;
            _gridDisplayEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool GridSnapEnabled
    {
        get => _gridSnapEnabled;
        set
        {
            if (_gridSnapEnabled == value) return;
            _gridSnapEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double GridSnapSpacing
    {
        get => _gridSnapSpacing;
        set
        {
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (Math.Abs(_gridSnapSpacing - value) <= 1e-9) return;
            _gridSnapSpacing = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool PolarTrackingEnabled
    {
        get => _polarTrackingEnabled;
        set
        {
            if (_polarTrackingEnabled == value) return;
            _polarTrackingEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double PolarIncrementDegrees
    {
        get => _polarIncrementDegrees;
        set
        {
            if (!double.IsFinite(value) || value <= 0 || value > 180) throw new ArgumentOutOfRangeException(nameof(value));
            if (Math.Abs(_polarIncrementDegrees - value) <= 1e-9) return;
            _polarIncrementDegrees = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ObjectSnapTrackingEnabled
    {
        get => _objectSnapTrackingEnabled;
        set
        {
            if (_objectSnapTrackingEnabled == value) return;
            _objectSnapTrackingEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}