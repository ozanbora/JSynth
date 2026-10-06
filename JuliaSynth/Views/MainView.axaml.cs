using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    // Where a Ctrl + drag pan was last, relative to the scroll viewer; null when not panning.
    private Point? _panPoint;

    public MainView()
    {
        InitializeComponent();

        // Ctrl + wheel zooms. Seen on the way down (tunnel), before the scroll viewer scrolls with it.
        CanvasScroll.AddHandler(PointerWheelChangedEvent, Canvas_PointerWheelChanged, RoutingStrategies.Tunnel);

        // Ctrl + drag pans; presses on nodes and ports are theirs (handled), so only the empty canvas pans.
        CanvasScroll.PointerPressed += Canvas_PointerPressed;
        CanvasScroll.PointerMoved += Canvas_PointerMoved;
        CanvasScroll.PointerReleased += Canvas_PointerReleased;

        InspectorGrip.PointerPressed += Grip_PointerPressed;
        InspectorGrip.PointerMoved += Grip_PointerMoved;
        InspectorGrip.PointerReleased += (_, _) => _gripStart = null;
    }

    // Where a resize of the function panel started (pointer x in this view, panel width); null when not resizing.
    private (double X, double Width)? _gripStart;

    private void Grip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _gripStart = (e.GetPosition(this).X, InspectorPanel.Bounds.Width);
        e.Pointer.Capture(InspectorGrip);
        e.Handled = true;
    }

    private void Grip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_gripStart is (double x, double width))
        {
            // Wide enough for the settings, and always leaving some canvas.
            double wanted = width - (e.GetPosition(this).X - x);
            InspectorPanel.Width = Math.Clamp(wanted, 340, Math.Max(340, Bounds.Width * 0.75));
        }
    }

    private void Canvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && _viewModel is not null)
        {
            _viewModel.ZoomByWheel(e.Delta);
            e.Handled = true;
        }
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.GetCurrentPoint(CanvasScroll).Properties.IsLeftButtonPressed)
        {
            _panPoint = e.GetPosition(CanvasScroll);
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panPoint is Point last && e.KeyModifiers == KeyModifiers.Control)
        {
            Point point = e.GetPosition(CanvasScroll);
            _viewModel?.PanBy(point - last);
            _panPoint = point;
        }
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Always end the pan, even when Ctrl was let go first.
        _panPoint = null;
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
        double zoom = DataContext is MainViewModel viewModel ? viewModel.Scale : MainViewModel.BaseScale;
        Vector center = CanvasScroll.Offset + new Vector(CanvasScroll.Viewport.Width / 2, CanvasScroll.Viewport.Height / 2);
        return new Point(center.X / zoom, center.Y / zoom);
    }
}
