using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JuliaSynth.ViewModels;

namespace JuliaSynth.Views;

public partial class MainView : UserControl
{
    // Where the canvas was right-clicked, in canvas coordinates; null when the menu was opened
    // from the keyboard.
    private Point? _menuPoint;

    public MainView()
    {
        InitializeComponent();
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
