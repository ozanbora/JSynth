using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using JuliaSynth.Controls;
using System;
using System.ComponentModel;

namespace JuliaSynth.Views.Outputs;

/// <summary>
/// Shows the plot of the values wired in, as the last run gave them. Bigger than other nodes.
/// </summary>
public partial class PlotNode : Node
{
    private Model.PlotNode? _model;
    private Runtime.PlotData? _drawn;

    public PlotNode() : base()
    {
        InitializeComponent();
        Width = 320;
        Height = 260;

        // On the canvas a plot is a picture: dragging moves the node. The larger window zooms and pans.
        Chart.UserInputProcessor.Disable();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.PropertyChanged -= ModelChanged;
        }

        _model = DataContext as Model.PlotNode;

        if (_model is not null)
        {
            _model.PropertyChanged += ModelChanged;
        }

        Redraw();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The text colour follows the node colour; so do the axes.
        if (change.Property == ForegroundProperty)
        {
            Redraw();
        }
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        // New numbers for the same heatmap (live, in a stream) only change its colours.
        if (e.PropertyName is nameof(Model.PlotNode.Plot) && PlotRenderer.Update(Chart.Plot, _drawn, _model?.Plot))
        {
            _drawn = _model?.Plot;
            Chart.Refresh();
        }
        else if (e.PropertyName is nameof(Model.PlotNode.Plot) or nameof(Model.PlotNode.Title))
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        PlotRenderer.Draw(Chart.Plot, _model?.Plot, _model?.Title ?? string.Empty, ForegroundColor(this), compact: true);
        _drawn = _model?.Plot;
        Chart.Refresh();
    }

    /// <summary>
    /// The colour of text on <paramref name="control"/>, as ScottPlot takes it.
    /// </summary>
    internal static ScottPlot.Color ForegroundColor(TemplatedControl control)
    {
        Color color = (control.Foreground as ISolidColorBrush)?.Color ?? Colors.White;
        return new ScottPlot.Color(color.R, color.G, color.B, color.A);
    }

    private void OpenLarger_Click(object? sender, RoutedEventArgs e)
    {
        if (_model?.Plot is not null && TopLevel.GetTopLevel(this) is Window owner)
        {
            new PlotWindow(_model).Show(owner);
        }
    }
}
