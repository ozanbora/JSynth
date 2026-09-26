using Avalonia;
using Avalonia.Layout;
using JuliaSynth.Controls;
using System;
using System.Collections.Specialized;

namespace JuliaSynth.Views;

public partial class Function : Node
{
    // Header (30) + name row (48) + border (2 x 1), see Function.axaml.
    private const double HeaderHeight = 80;
    private const double ParameterRowHeight = 20;
    private const int MinParameterRows = 2;

    private Model.Function? _model;

    public Function()
    {
        InitializeComponent();

        // Nodes are centered on their designer cell. Anchor the top where a node of the
        // minimum height would be, so extra parameters grow the node downwards only.
        double minHeight = HeaderHeight + (MinParameterRows * ParameterRowHeight);
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, (Constants.HEIGHT - minHeight) / 2, 0, 0);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Parameters.CollectionChanged -= ParametersChanged;
        }

        _model = DataContext as Model.Function;

        if (_model is not null)
        {
            _model.Parameters.CollectionChanged += ParametersChanged;
        }

        UpdateHeight();
    }

    private void ParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateHeight();
    }

    // The node sits in a single 20x20 designer cell, so its size has to be explicit;
    // grow it so that every parameter gets its own input connector row.
    private void UpdateHeight()
    {
        int rows = Math.Max(MinParameterRows, _model?.Parameters.Count ?? 0);
        Height = HeaderHeight + (rows * ParameterRowHeight);
    }
}
