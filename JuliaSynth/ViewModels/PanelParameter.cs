using JuliaSynth.Model;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace JuliaSynth.ViewModels;

/// <summary>
/// An input set from the parameters panel: a number with a slider and a box, an array typed in, or a folder.
/// </summary>
public sealed class PanelParameter : ReactiveObject, IDisposable
{
    private readonly string _wiredName;

    // Told when the user sets the value in the panel, e.g. so the design runs again.
    private readonly Action<PanelParameter>? _edited;

    // The slider's range when none was set: around the value, fixed while the parameter is shown, so moving the slider
    // doesn't move it.
    private double _from;
    private double _to;

    /// <param name="node">The input.</param>
    /// <param name="wiredName">The name of the parameter it is wired into, or else its variable's.</param>
    /// <param name="source">Where its value goes, e.g. <c>x1 → payment(P)</c>.</param>
    /// <param name="edited">Told when the user sets the value in the panel.</param>
    public PanelParameter(Node node, string wiredName, string source, Action<PanelParameter>? edited = null)
    {
        (Node, _wiredName, Source, _edited) = (node, wiredName, source, edited);
        (_from, _to) = RangeAround(NumberValue);
        node.PropertyChanged += NodeChanged;
    }

    public Node Node { get; }

    /// <summary>
    /// Where its value goes, e.g. <c>x1 → payment(P)</c>.
    /// </summary>
    public string Source { get; }

    public void Dispose()
    {
        Node.PropertyChanged -= NodeChanged;
    }

    /// <summary>
    /// Its name in the panel: the one given (its variable in the script too), else the name of the parameter it is wired
    /// into. Emptied, it is named so again.
    /// </summary>
    public string Name
    {
        get => Node.PanelName.Length > 0 ? Node.PanelName : _wiredName;
        set => Node.PanelName = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public bool IsNumber => Node is Float or Integer or UnsignedInteger;

    public bool IsArray => Node is ArrayInput;

    public bool IsFolder => Node is FilesNode;

    #region Numbers
    /// <summary>
    /// The number, as a double for the slider.
    /// </summary>
    private double NumberValue => Node switch
    {
        Float f => f.Value,
        Integer z => z.Value,
        UnsignedInteger n => (double)n.Value,
        _ => 0,
    };

    /// <summary>
    /// The end of the slider's scale: it goes from 0 to this, whatever the range, so the slider never has a value
    /// outside its own range to clamp and write back to the node.
    /// </summary>
    public const double SliderScale = 1000;

    /// <summary>
    /// The slider's position on its scale, the number's place in the range: setting it sets the number, a whole one for
    /// integers, within the node's limits.
    /// </summary>
    public double SliderPosition
    {
        get => To > From ? Math.Clamp((NumberValue - From) / (To - From) * SliderScale, 0, SliderScale) : 0;
        set
        {
            if (Math.Abs(value - SliderPosition) < 1e-9)
            {
                return;
            }

            SetNumber(From + (value / SliderScale * (To - From)));
            _edited?.Invoke(this);
        }
    }

    /// <summary>
    /// The number as typed in its box: exactly, also for integers too large for a slider.
    /// </summary>
    public string NumberText
    {
        get => Node switch
        {
            Float f => f.Value.ToString("R", CultureInfo.InvariantCulture),
            Integer z => z.Value.ToString(CultureInfo.InvariantCulture),
            UnsignedInteger n => n.Value.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty,
        };
        set
        {
            bool parsed = Node switch
            {
                Float f when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) => Set(() => f.Value = Math.Clamp(x, f.Min, f.Max)),
                Integer z when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long x) => Set(() => z.Value = Math.Clamp(x, z.Min, z.Max)),
                UnsignedInteger n when UInt128.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out UInt128 x) => Set(() => n.Value = UInt128.Clamp(x, n.Min, n.Max)),
                _ => false,
            };

            if (!parsed)
            {
                // Not a number of its kind: shown as it was.
                this.RaisePropertyChanged();
                return;
            }

            // Typed in beyond the slider: the slider reaches it.
            (_from, _to) = (Math.Min(From, NumberValue), Math.Max(To, NumberValue));
            this.RaisePropertyChanged(nameof(From));
            this.RaisePropertyChanged(nameof(To));
            this.RaisePropertyChanged(nameof(FromText));
            this.RaisePropertyChanged(nameof(ToText));
            this.RaisePropertyChanged(nameof(SliderPosition));
            _edited?.Invoke(this);

            static bool Set(Action set)
            {
                set();
                return true;
            }
        }
    }

    private void SetNumber(double value)
    {
        switch (Node)
        {
            case Float f:
                f.Value = Math.Clamp(value, f.Min, f.Max);
                break;
            case Integer z:
                z.Value = Math.Clamp((long)Math.Round(value), z.Min, z.Max);
                break;
            case UnsignedInteger n:
                n.Value = UInt128.Clamp((UInt128)Math.Max(0, Math.Round(value)), n.Min, n.Max);
                break;
        }
    }

    /// <summary>
    /// Where the slider starts.
    /// </summary>
    public double From
    {
        get => Node.PanelFrom ?? _from;
        set => Node.PanelFrom = value;
    }

    /// <summary>
    /// Where the slider ends.
    /// </summary>
    public double To
    {
        get => Node.PanelTo ?? _to;
        set => Node.PanelTo = value;
    }

    /// <summary>
    /// <see cref="From"/> as typed in the panel.
    /// </summary>
    public string FromText
    {
        get => From.ToString("G", CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double from) && from < To)
            {
                From = from;
            }

            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// <see cref="To"/> as typed in the panel.
    /// </summary>
    public string ToText
    {
        get => To.ToString("G", CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double to) && to > From)
            {
                To = to;
            }

            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// A range for a slider at <paramref name="value"/>: from 0 to a round number past it, about twice it; from as far
    /// below 0 for a negative one.
    /// </summary>
    internal static (double From, double To) RangeAround(double value)
    {
        double size = Math.Abs(value) * 2;
        if (size == 0)
        {
            return (0, 10);
        }

        // 1, 2 or 5 times a power of ten.
        double power = Math.Pow(10, Math.Floor(Math.Log10(size)));
        double round = new[] { 1, 2, 5, 10 }.Select(m => m * power).First(r => r >= size);
        return value > 0 ? (0, round) : (-round, round);
    }
    #endregion

    #region Arrays and folders
    /// <summary>
    /// An array's values as typed, or a folder's path.
    /// </summary>
    public string Text
    {
        get => Node switch
        {
            ArrayInput array => array.Text,
            FilesNode files => files.Folder,
            _ => string.Empty,
        };
        set
        {
            if (value == Text)
            {
                return;
            }

            switch (Node)
            {
                case ArrayInput array:
                    array.Text = value;
                    break;
                case FilesNode files:
                    files.Folder = value;
                    break;
            }

            _edited?.Invoke(this);
        }
    }
    #endregion

    private void NodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "Value":
                this.RaisePropertyChanged(nameof(SliderPosition));
                this.RaisePropertyChanged(nameof(NumberText));
                break;
            case nameof(ArrayInput.Text) or nameof(FilesNode.Folder):
                this.RaisePropertyChanged(nameof(Text));
                break;
            case nameof(Node.PanelName):
                this.RaisePropertyChanged(nameof(Name));
                break;
            case nameof(Node.PanelFrom):
                this.RaisePropertyChanged(nameof(From));
                this.RaisePropertyChanged(nameof(FromText));
                this.RaisePropertyChanged(nameof(SliderPosition));
                break;
            case nameof(Node.PanelTo):
                this.RaisePropertyChanged(nameof(To));
                this.RaisePropertyChanged(nameof(ToText));
                this.RaisePropertyChanged(nameof(SliderPosition));
                break;
        }
    }
}
