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

    // Where a pan (a drag on the empty canvas) was last, relative to the scroll viewer; null when not panning.
    private Point? _panPoint;

    // Whether the pan went anywhere: if not, it was a click, which clears the selection.
    private bool _panMoved;

    public MainView()
    {
        InitializeComponent();

        // Ctrl + wheel zooms. Seen on the way down (tunnel), before the scroll viewer scrolls with it.
        CanvasScroll.AddHandler(PointerWheelChangedEvent, Canvas_PointerWheelChanged, RoutingStrategies.Tunnel);

        // A drag pans; presses on nodes and ports are theirs (handled), and Ctrl + drag selects, so only a plain drag
        // on the empty canvas pans.
        CanvasScroll.PointerPressed += Canvas_PointerPressed;
        CanvasScroll.PointerMoved += Canvas_PointerMoved;
        CanvasScroll.PointerReleased += Canvas_PointerReleased;

        InspectorGrip.PointerPressed += Grip_PointerPressed;
        InspectorGrip.PointerMoved += Grip_PointerMoved;
        InspectorGrip.PointerReleased += (_, _) => _gripStart = null;

        OutputGrip.PointerPressed += OutputGrip_PointerPressed;
        OutputGrip.PointerMoved += OutputGrip_PointerMoved;
        OutputGrip.PointerReleased += (_, _) => _outputGripStart = null;
    }

    // Where a resize of the output panel started (pointer y in this view, panel height); null when not resizing.
    private (double Y, double Height)? _outputGripStart;

    private void OutputGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _outputGripStart = (e.GetPosition(this).Y, OutputPanel.Bounds.Height);
        e.Pointer.Capture(OutputGrip);
        e.Handled = true;
    }

    private void OutputGrip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_outputGripStart is (double y, double height))
        {
            // Tall enough for its title and a few lines, and always leaving some canvas.
            double wanted = height - (e.GetPosition(this).Y - y);
            OutputPanel.Height = Math.Clamp(wanted, 80, Math.Max(80, Bounds.Height * 0.8));
        }
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
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.GetCurrentPoint(CanvasScroll).Properties.IsLeftButtonPressed)
        {
            _panPoint = e.GetPosition(CanvasScroll);
            _panMoved = false;
            e.Pointer.Capture(CanvasScroll);
            e.Handled = true;
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panPoint is Point last)
        {
            Point point = e.GetPosition(CanvasScroll);

            // A few pixels of a click's wobble aren't a drag.
            if (!_panMoved && Math.Abs(point.X - last.X) + Math.Abs(point.Y - last.Y) < 4)
            {
                return;
            }

            _panMoved = true;
            CanvasScroll.Cursor = new Cursor(StandardCursorType.SizeAll);
            _viewModel?.PanBy(point - last, CanvasScroll.ScrollBarMaximum);
            _panPoint = point;
        }
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_panPoint is null)
        {
            return;
        }

        // A click without a drag clears the selection, unless Shift keeps it.
        if (!_panMoved && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            MainViewModel.SelectOnly(null);
        }

        _panPoint = null;
        CanvasScroll.Cursor = null;
        e.Pointer.Capture(null);
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

        // On a wire: a breakpoint there, or none if it has one.
        _menuWire = Canvas is Controls.DesignerGrid grid && _menuPoint is Point at ? grid.WireAt(at) : null;
        PauseHereItem.IsVisible = _menuWire is not null;
        PauseHereItem.Header = _menuWire?.Breakpoint is null ? "Pause on this wire" : "Remove the breakpoint";
        FillLibrary(LibraryMenu, () => _menuPoint ?? ViewCenter());
    }

    private void AddFlyout_Opening(object? sender, EventArgs e)
    {
        FillLibrary(LibraryFlyoutMenu, ViewCenter);
    }

    /// <summary>
    /// The library's entries in <paramref name="menu"/>, each adding a copy of its nodes at <paramref name="where"/>;
    /// then saving the selection to it, and managing it.
    /// </summary>
    private void FillLibrary(MenuItem menu, Func<Point> where)
    {
        menu.Items.Clear();
        foreach (LibraryEntry entry in MainViewModel.LibraryEntries())
        {
            MenuItem item = new() { Header = entry.Name };
            item.Click += async (_, _) =>
            {
                Point point = where();
                try
                {
                    MainViewModel.AddFromLibrary(entry, (int)(point.Y / Constants.HEIGHT), (int)(point.X / Constants.WIDTH));
                }
                catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
                {
                    if (TopLevel.GetTopLevel(this) is Window window)
                    {
                        _ = await MessageDialog.ShowAsync(window, "Can't add from the library", $"{entry.Name} couldn't be read.\n\n{error.Message}", "OK");
                    }
                }
            };
            menu.Items.Add(item);
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "Nothing saved yet", IsEnabled = false });
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Save the selection to the library…", Command = _viewModel?.SaveToLibrary });
        MenuItem manage = new() { Header = "Manage library…" };
        manage.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                await new LibraryDialog().ShowDialog(window);
            }
        };
        menu.Items.Add(manage);
    }

    // The wire the canvas menu was opened on; null when it wasn't on one.
    private Model.Connection? _menuWire;

    private void PauseHere_Click(object? sender, RoutedEventArgs e)
    {
        if (_menuWire is Model.Connection wire)
        {
            MainViewModel.SetBreakpoint(wire, wire.Breakpoint is null ? Model.Breakpoint.Every : null);
        }
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
