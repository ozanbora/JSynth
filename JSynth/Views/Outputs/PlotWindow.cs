using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JSynth.Controls;
using JSynth.Runtime;
using ScottPlot;
using ScottPlot.Avalonia;
using System;
using System.ComponentModel;

namespace JSynth.Views.Outputs;

/// <summary>
/// A plot in a window of its own: bigger, with a colour bar for heatmaps and contour lines, and zoom (wheel) and pan (drag).
/// A crosshair follows the mouse, and the bar below says where it points: the coordinates, and the data
/// point, heatmap value or matrix entry there. It follows the node: a new run redraws it.
/// </summary>
public sealed class PlotWindow : Window
{
    private readonly Model.PlotNode _model;
    private readonly AvaPlot _chart = new();
    private readonly SelectableTextBlock _readout = new()
    {
        FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
        FontSize = 13,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        Text = PlotReadout.Hint,
    };

    private PlotReadout? _describe;
    private PlotData? _drawn;
    private ScottPlot.Plottables.Crosshair? _crosshair;
    private ScottPlot.Plottables.Marker? _point;

    public PlotWindow(Model.PlotNode model)
    {
        _model = model;
        Title = string.IsNullOrEmpty(model.Title) ? "Plot - JSynth" : $"{model.Title} - JSynth";
        Icon = AppIcon.Window;
        Width = 900;
        Height = 600;

        Border bar = new() { Padding = new Thickness(12, 4), BorderThickness = new Thickness(0, 1, 0, 0), Child = _readout };
        _ = bar.Bind(Border.BackgroundProperty, bar.GetResourceObservable("StatusBarBackgroundBrush"));
        _ = bar.Bind(Border.BorderBrushProperty, bar.GetResourceObservable("StatusBarBorderBrush"));
        DockPanel.SetDock(bar, Dock.Bottom);
        Content = new DockPanel { Children = { bar, _chart } };

        _chart.PointerMoved += Chart_PointerMoved;
        _chart.PointerExited += (_, _) => Point(null);
        _model.PropertyChanged += ModelChanged;
    }

    /// <summary>
    /// The text below the plot; tests read it.
    /// </summary>
    internal string? Readout => _readout.Text;

    internal ScottPlot.Plottables.Crosshair? Crosshair => _crosshair;

    internal Plot Chart => _chart.Plot;

    /// <summary>
    /// Draws once the window has its theme: its colours are only known then.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // The canvas colour of the current theme (it lives in the app's styles, found through the window).
        Background = this.TryFindResource("CanvasBackgroundBrush", ActualThemeVariant, out object? brush) && brush is IBrush canvas
            ? canvas
            : Brushes.Black;
        Redraw();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _model.PropertyChanged -= ModelChanged;
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        // New numbers for the same heatmap (live, in a stream) only change its colours: the colour bar, axes,
        // zoom and crosshair stay.
        if (e.PropertyName == nameof(Model.PlotNode.Plot) && PlotRenderer.Update(_chart.Plot, _drawn, _model.Plot))
        {
            _drawn = _model.Plot;
            _describe = new PlotReadout(_model.Plot!, _model.Levels);
            _chart.Refresh();
        }

        // A cleared plot (the design changed) keeps the last one here until the next run.
        else if ((e.PropertyName == nameof(Model.PlotNode.Plot) && _model.Plot is not null) || e.PropertyName is nameof(Model.PlotNode.Title) or nameof(Model.PlotNode.Levels))
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        // A matrix drawn again at the same size (live, in a stream) keeps where it was zoomed and panned to.
        bool sameMatrix = _drawn is { Kind: PlotKind.Heatmap or PlotKind.Contour or PlotKind.Spy } last && _model.Plot is PlotData now
            && now.Kind == last.Kind && now.Rows == last.Rows && now.Columns == last.Columns;
        AxisLimits limits = _chart.Plot.Axes.GetLimits();

        ScottPlot.Color foreground = PlotNode.ForegroundColor(this);
        PlotRenderer.Draw(_chart.Plot, _model.Plot, _model.Title, foreground, compact: false, _model.Levels);
        if (sameMatrix)
        {
            _chart.Plot.Axes.SetLimits(limits);
        }

        _drawn = _model.Plot;
        _describe = _model.Plot is null ? null : new PlotReadout(_model.Plot, _model.Levels);

        // Added last, so they are drawn over the data.
        _crosshair = _chart.Plot.Add.Crosshair(0, 0);
        _crosshair.LineColor = foreground.WithAlpha(0.45);
        _crosshair.LineWidth = 1;
        _crosshair.IsVisible = false;

        _point = _chart.Plot.Add.Marker(0, 0, MarkerShape.OpenCircle, 14, foreground);
        _point.MarkerLineWidth = 2;
        _point.IsVisible = false;

        Point(null);
        _chart.Refresh();
    }

    /// <summary>
    /// Moves the crosshair to the mouse and says what is there.
    /// </summary>
    private void Chart_PointerMoved(object? sender, PointerEventArgs e)
    {
        Avalonia.Point position = e.GetPosition(_chart);
        Pixel pixel = new((float)(position.X * _chart.DisplayScale), (float)(position.Y * _chart.DisplayScale));
        Point(_chart.Plot.LastRender.DataRect.Contains(pixel) ? _chart.Plot.GetCoordinates(pixel) : null);
    }

    /// <summary>
    /// Shows <paramref name="at"/> (plot coordinates) with the crosshair and in the bar; hides them for null.
    /// </summary>
    internal void Point(Coordinates? at)
    {
        if (_crosshair is null || _point is null)
        {
            return;
        }

        if (at is not Coordinates where || _describe is null)
        {
            _crosshair.IsVisible = false;
            _point.IsVisible = false;
            _readout.Text = PlotReadout.Hint;
        }
        else
        {
            AxisLimits limits = _chart.Plot.Axes.GetLimits();
            (string text, (double X, double Y)? point) = _describe.Describe(where.X, where.Y, Math.Abs(limits.HorizontalSpan), Math.Abs(limits.VerticalSpan));
            _crosshair.Position = where;
            _crosshair.IsVisible = true;
            _point.IsVisible = point is not null;
            if (point is (double x, double y))
            {
                _point.Location = new Coordinates(x, y);
            }

            _readout.Text = text;
        }

        _chart.Refresh();
    }
}
