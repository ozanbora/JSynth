using JSynth.Model;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace JSynth.ViewModels;

/// <summary>
/// An input set from the parameters panel: a number with a slider and a box, an array typed in, or a folder.
/// </summary>
public sealed class PanelParameter : ReactiveObject, IDisposable
{
    private readonly string _wiredName;

    // Told when the user sets the value in the panel, e.g. so the design runs again.
    private readonly Action<PanelParameter>? _edited;

    // The slider's range where the number has no limit of its own (still its type's, like ±1.8e308 for a float): around
    // the value, fixed while the parameter is shown, so moving the slider doesn't move it.
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
    /// The slider's position on its scale, the number's place in the range: setting it sets the number to the nearest
    /// step from <see cref="From"/>, within the range and the node's limits.
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

            double steps = Math.Round(value / SliderScale * (To - From) / Step);
            double number = Math.Clamp(From + (steps * Step), From, To);

            // 0.3, not 0.30000000000000004: as many decimals as the step and the start have.
            SetNumber(Math.Round(number, Decimals));
            _edited?.Invoke(this);
        }
    }

    /// <summary>
    /// How far one press of an arrow key moves the slider on its scale: one step. Page Up and Down move ten.
    /// </summary>
    public double SliderSmallChange => To > From ? Step / (To - From) * SliderScale : 1;

    public double SliderLargeChange => SliderSmallChange * 10;

    /// <summary>
    /// The step the slider moves in: the one set (<see cref="Node.PanelStep"/>), else <see cref="DefaultStep"/>.
    /// </summary>
    public double Step => Node.PanelStep ?? DefaultStep;

    private bool IsInteger => Node is Integer or UnsignedInteger;

    /// <summary>
    /// A hundredth of the range, rounded down to 1, 2 or 5 times a power of ten: 5000 for 0 to 500000, 0.001 for 0 to
    /// 0.1. At least 1 for integers.
    /// </summary>
    public double DefaultStep => SliderStep.Default(From, To, IsInteger);

    /// <summary>
    /// <see cref="Step"/> as typed in the panel: a number above 0, a whole one of at least 1 for integers; emptied, the
    /// default again.
    /// </summary>
    public string StepText
    {
        get => Step.ToString("G", CultureInfo.InvariantCulture);
        set
        {
            if (SliderStep.TryParse(value, IsInteger, out double? step))
            {
                Node.PanelStep = step;
            }

            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// The decimals numbers are shown with and the slider rounds to: as many as the step and the start of the range have.
    /// </summary>
    private int Decimals => Math.Max(DecimalsOf(Step), DecimalsOf(From));

    private static int DecimalsOf(double value)
    {
        // Through decimal: 0.05 has 2, where its double is 0.05000000000000000277.
        return double.IsFinite(value) && Math.Abs(value) < 7.9e27 ? Math.Min(15, (int)((decimal)value).Scale) : 0;
    }

    /// <summary>
    /// The number in its box: a float cut off at the step's decimals (<see cref="NumberTip"/> has the rest), an integer
    /// whole, also one too large for a slider. Typed in, kept exactly as typed.
    /// </summary>
    public string NumberText
    {
        get => Node switch
        {
            Float f => Truncated(f.Value, Decimals),
            Integer z => z.Value.ToString(CultureInfo.InvariantCulture),
            UnsignedInteger n => n.Value.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty,
        };
        set
        {
            // The box gives its text back when it loses focus: as shown, cut off, it isn't a new value.
            if (value == NumberText)
            {
                return;
            }

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
            StepChanged();
            _edited?.Invoke(this);

            static bool Set(Action set)
            {
                set();
                return true;
            }
        }
    }

    /// <summary>
    /// A float's whole value, over its box when the box shows it cut off; else none.
    /// </summary>
    public string? NumberTip => Node is Float f && f.Value.ToString("R", CultureInfo.InvariantCulture) is string full && full != NumberText ? full : null;

    /// <summary>
    /// <paramref name="value"/> with no more than <paramref name="decimals"/> decimals, cut off rather than rounded.
    /// </summary>
    internal static string Truncated(double value, int decimals)
    {
        // Through decimal: in double, 0.57 × 100 is 56.99999999999999, which would cut off to 0.56.
        if (double.IsFinite(value) && Math.Abs(value) < 7.9e13)
        {
            decimal scale = (decimal)Math.Pow(10, decimals);
            return (decimal.Truncate((decimal)value * scale) / scale).ToString("G29", CultureInfo.InvariantCulture);
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
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
    /// Where the slider starts: the number's Min. Set, it sets the Min, and the value moves into the range.
    /// </summary>
    public double From
    {
        get => Limits is { MinSet: true } limits ? limits.Min : Math.Max(_from, Limits.Min);
        set => SetLimits(value, null);
    }

    /// <summary>
    /// Where the slider ends: the number's Max. Set, it sets the Max, and the value moves into the range.
    /// </summary>
    public double To
    {
        get => Limits is { MaxSet: true } limits ? limits.Max : Math.Min(_to, Limits.Max);
        set => SetLimits(null, value);
    }

    /// <summary>
    /// The number's Min and Max, and whether each is its own: not still its type's extreme.
    /// </summary>
    private (double Min, double Max, bool MinSet, bool MaxSet) Limits => Node switch
    {
        Float f => (f.Min, f.Max, f.Min != f.HardMin, f.Max != f.HardMax),
        Integer z => (z.Min, z.Max, z.Min != z.HardMin, z.Max != z.HardMax),
        UnsignedInteger n => ((double)n.Min, (double)n.Max, n.Min != n.HardMin, n.Max != n.HardMax),
        _ => (0, 0, false, false),
    };

    /// <summary>
    /// Gives a number with no limits of its own (only its type's, like ±1.8e308 for a float) limits around its value: the
    /// range its slider would have, so the node and the panel show the same.
    /// </summary>
    internal static void LimitAroundValue(Node node)
    {
        using PanelParameter parameter = new(node, string.Empty, string.Empty);
        if (parameter.Node is Float or Integer or UnsignedInteger && parameter.Limits is { } limits && !(limits.MinSet && limits.MaxSet))
        {
            parameter.SetLimits(limits.MinSet ? null : parameter.From, limits.MaxSet ? null : parameter.To);
        }
    }

    private void SetLimits(double? min, double? max)
    {
        switch (Node)
        {
            case Float f:
                f.Min = Math.Clamp(min ?? f.Min, f.HardMin, f.HardMax);
                f.Max = Math.Clamp(max ?? f.Max, f.HardMin, f.HardMax);
                f.Value = Math.Clamp(f.Value, f.Min, f.Max);
                break;
            case Integer z:
                // Whole numbers inside the range typed: 1.5 to 9.5 is 2 to 9.
                z.Min = min is double lo ? (lo <= z.HardMin ? z.HardMin : (long)Math.Ceiling(lo)) : z.Min;
                z.Max = max is double hi ? (hi >= z.HardMax ? z.HardMax : (long)Math.Floor(hi)) : z.Max;
                z.Value = Math.Clamp(z.Value, z.Min, z.Max);
                break;
            case UnsignedInteger n:
                n.Min = min is double low ? (low <= 0 ? n.HardMin : low >= (double)n.HardMax ? n.HardMax : (UInt128)Math.Ceiling(low)) : n.Min;
                n.Max = max is double high ? (high >= (double)n.HardMax ? n.HardMax : (UInt128)Math.Floor(Math.Max(0, high))) : n.Max;
                n.Value = UInt128.Clamp(n.Value, n.Min, n.Max);
                break;
        }
    }

    /// <summary>
    /// <see cref="From"/> as typed in the panel.
    /// </summary>
    public string FromText
    {
        get => From.ToString("G", CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double from) && from < To && double.IsFinite(from))
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
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double to) && to > From && double.IsFinite(to))
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

    // The step, its default following the range, and what it shapes.
    private void StepChanged()
    {
        this.RaisePropertyChanged(nameof(Step));
        this.RaisePropertyChanged(nameof(StepText));
        this.RaisePropertyChanged(nameof(SliderSmallChange));
        this.RaisePropertyChanged(nameof(SliderLargeChange));
        this.RaisePropertyChanged(nameof(NumberText));
        this.RaisePropertyChanged(nameof(NumberTip));
    }

    private void NodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "Value":
                this.RaisePropertyChanged(nameof(SliderPosition));
                this.RaisePropertyChanged(nameof(NumberText));
                this.RaisePropertyChanged(nameof(NumberTip));
                break;
            case nameof(ArrayInput.Text) or nameof(FilesNode.Folder):
                this.RaisePropertyChanged(nameof(Text));
                break;
            case nameof(Node.PanelName):
                this.RaisePropertyChanged(nameof(Name));
                break;
            // The limits are the slider's range; the type's change them.
            case "Min" or "Max" or "HardMin" or "HardMax":
                this.RaisePropertyChanged(nameof(From));
                this.RaisePropertyChanged(nameof(FromText));
                this.RaisePropertyChanged(nameof(To));
                this.RaisePropertyChanged(nameof(ToText));
                this.RaisePropertyChanged(nameof(SliderPosition));
                StepChanged();
                break;
            case nameof(Node.PanelStep):
                StepChanged();
                break;
        }
    }
}
