using Avalonia.Controls;
using Avalonia.Media;
using JuliaSynth.Controls;
using ScottPlot.Avalonia;
using System;
using System.ComponentModel;

namespace JuliaSynth.Views.Outputs;

/// <summary>
/// A plot in a window of its own: bigger, with a colour bar for heatmaps, and zoom (wheel) and pan (drag).
/// It follows the node: a new run redraws it.
/// </summary>
public sealed class PlotWindow : Window
{
    private readonly Model.PlotNode _model;
    private readonly AvaPlot _chart = new();

    public PlotWindow(Model.PlotNode model)
    {
        _model = model;
        Title = string.IsNullOrEmpty(model.Title) ? "Plot - JuliaSynth" : $"{model.Title} - JuliaSynth";
        Width = 900;
        Height = 600;
        Content = _chart;
        _model.PropertyChanged += ModelChanged;
    }

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
        // A cleared plot (the design changed) keeps the last one here until the next run.
        if ((e.PropertyName == nameof(Model.PlotNode.Plot) && _model.Plot is not null) || e.PropertyName == nameof(Model.PlotNode.Title))
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        PlotRenderer.Draw(_chart.Plot, _model.Plot, _model.Title, PlotNode.ForegroundColor(this), compact: false);
        _chart.Refresh();
    }
}
