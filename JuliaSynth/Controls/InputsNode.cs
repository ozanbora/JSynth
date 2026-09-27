using Avalonia;
using Avalonia.Layout;
using System;
using System.Collections.Specialized;

namespace JuliaSynth.Controls;

/// <summary>
/// A node with a row per input under its header, growing downwards as inputs are added.
/// </summary>
public class InputsNode : Node
{
    // Header (30) + name row (48) + border (2 x 1), see Function.axaml.
    private const double HeaderHeight = 80;
    private const double InputRowHeight = 20;
    private const int MinInputRows = 2;

    private INotifyCollectionChanged? _inputs;

    public InputsNode()
    {
        // Nodes are centered on their designer cell. Anchor the top where a node of the
        // minimum height would be, so extra inputs grow the node downwards only.
        double minHeight = HeaderHeight + (MinInputRows * InputRowHeight);
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, (Constants.HEIGHT - minHeight) / 2, 0, 0);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_inputs is not null)
        {
            _inputs.CollectionChanged -= InputsChanged;
        }

        _inputs = (DataContext as Model.Node)?.Inputs as INotifyCollectionChanged;

        if (_inputs is not null)
        {
            _inputs.CollectionChanged += InputsChanged;
        }

        UpdateHeight();
    }

    private void InputsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateHeight();
    }

    // The node sits in a single 20x20 designer cell, so its size has to be explicit;
    // grow it so that every input gets its own connector row.
    private void UpdateHeight()
    {
        int rows = Math.Max(MinInputRows, (DataContext as Model.Node)?.Inputs.Count ?? 0);
        Height = HeaderHeight + (rows * InputRowHeight);
    }
}
