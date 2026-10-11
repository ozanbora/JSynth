using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JSynth.ViewModels;

namespace JSynth.Controls;

/// <summary>
/// The body of a number input (Float, Integer, UnsignedInteger): its glyph, value, limits, ports and buttons. A node's
/// view sets the parts that are its own: the glyph, what kind of number it is, and the panel Ξ opens.
/// </summary>
public partial class NumericNodeBody : UserControl
{
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<NumericNodeBody, string?>(nameof(Glyph));

    public static readonly StyledProperty<string?> KindProperty = AvaloniaProperty.Register<NumericNodeBody, string?>(nameof(Kind));

    public static readonly StyledProperty<object?> DetailsProperty = AvaloniaProperty.Register<NumericNodeBody, object?>(nameof(Details));

    public NumericNodeBody()
    {
        // The ports stick out of the node, and its shadow falls outside it: as for the node itself (see Node).
        ClipToBounds = false;
        InitializeComponent();
    }

    /// <summary>
    /// The symbol at the top left: 𝑥, 𝑧, 𝑛.
    /// </summary>
    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>
    /// What kind of number, shown over the 𝑖 with its bitness: "Signed integer".
    /// </summary>
    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// The panel Ξ opens, to edit the value, limits and type.
    /// </summary>
    public object? Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DetailsProperty && EditButton?.Flyout is Flyout flyout)
        {
            flyout.Content = Details;
        }
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is Model.Node node)
        {
            _ = MainViewModel.Nodes.Remove(node);
        }
    }
}
