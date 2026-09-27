using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.ViewModels;
using System;
using System.Collections.Specialized;

namespace JuliaSynth.Views;

public partial class MainView : UserControl
{
    // Where the canvas was right-clicked, in canvas coordinates; null when the menu was opened
    // from the keyboard.
    private Point? _menuPoint;

    private MainViewModel? _viewModel;

    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.Output.CollectionChanged -= OutputChanged;
        }

        _viewModel = DataContext as MainViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Output.CollectionChanged += OutputChanged;
        }
    }

    // Keep the latest output in view.
    private void OutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _viewModel?.Output.Count > 0)
        {
            OutputList.ScrollIntoView(_viewModel.Output.Count - 1);
        }
    }

    private Control? Canvas => NodesItems.ItemsPanelRoot;

    private void Canvas_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        _menuPoint = Canvas is Control canvas && e.TryGetPosition(canvas, out Point point) ? point : null;
    }

    private void AddAtPointer_Click(object? sender, RoutedEventArgs e)
    {
        Add(sender, _menuPoint ?? ViewCenter());
    }

    private void AddAtCenter_Click(object? sender, RoutedEventArgs e)
    {
        Add(sender, ViewCenter());
    }

    private static void Add(object? sender, Point point)
    {
        if (sender is MenuItem { Tag: string kind })
        {
            _ = MainViewModel.AddNode(kind, (int)(point.Y / Constants.HEIGHT), (int)(point.X / Constants.WIDTH));
        }
    }

    /// <summary>
    /// Middle of the visible part of the canvas, in canvas coordinates.
    /// </summary>
    private Point ViewCenter()
    {
        double zoom = DataContext is MainViewModel viewModel ? viewModel.SelectedZoomLevel : 1.0;
        Vector center = CanvasScroll.Offset + new Vector(CanvasScroll.Viewport.Width / 2, CanvasScroll.Viewport.Height / 2);
        return new Point(center.X / zoom, center.Y / zoom);
    }
}
