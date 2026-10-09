using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace JuliaSynth.Controls;

/// <summary>
/// A line icon: <see cref="Data"/> drawn on a 24 × 24 grid, scaled to the icon's size, in the text's colour. The same
/// size whatever the font, unlike a symbol in text.
/// </summary>
public class Icon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icon>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 1.8);

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty, StrokeThicknessProperty);
        WidthProperty.OverrideDefaultValue<Icon>(16);
        HeightProperty.OverrideDefaultValue<Icon>(16);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// The lines' width on the 24 × 24 grid.
    /// </summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Data is not Geometry data || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        double scale = System.Math.Min(Bounds.Width, Bounds.Height) / 24;
        Pen pen = new(Foreground ?? Brushes.Gray, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation((Bounds.Width - (24 * scale)) / 2, (Bounds.Height - (24 * scale)) / 2)))
        {
            context.DrawGeometry(null, pen, data);
        }
    }
}
