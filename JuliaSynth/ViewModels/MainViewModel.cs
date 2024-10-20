using Avalonia;
using Avalonia.Collections;
using JuliaSynth.Model;
using ReactiveUI;
using System.Reactive;

namespace JuliaSynth.ViewModels;

public class MainViewModel : ReactiveObject
{
    private Vector _scrollViewerOffset = Vector.Zero;
    private double _selectedZoomLevel = 1.0;

    public ReactiveCommand<Vector, Unit> CtrlPan { get; }

    public ReactiveCommand<Vector, Unit> CtrlZoom { get; }

    public AvaloniaList<double> ZoomLevels { get; } = new([
        0.25, 0.50, 0.80, 1.00, 1.25, 1.50, 2.00, 3.00, 4.00, 5.00, 8.00, 10.00
        ]);

    public AvaloniaList<Node> Nodes { get; } = new([
        new Float() { Row = 5, Column = 10, Value = 0.0 },
        new UnsignedInteger() { Row = 3, Column = 4, Value = 0 },
        new Integer() { Row = 3, Column = 4, Value = 0 },
        ]);
    public double MinZoom { get; } = 0.25;

    public double MaxZoom { get; } = 10.00;

    public double SelectedZoomLevel
    {
        get => _selectedZoomLevel;
        set => this.RaiseAndSetIfChanged(ref _selectedZoomLevel, value);
    }


    public Vector ScrollViewerOffset
    {
        get => _scrollViewerOffset;
        set => this.RaiseAndSetIfChanged(ref _scrollViewerOffset, value);
    }

    public MainViewModel()
    {
        CtrlPan = ReactiveCommand.Create<Vector>(CtrlPanMove);
        CtrlZoom = ReactiveCommand.Create<Vector>(CtrlZoomMove);
    }

    private void CtrlPanMove(Vector offset)
    {
        ScrollViewerOffset -= offset;
    }

    private void CtrlZoomMove(Vector delta)
    {
        int index = ZoomLevels.IndexOf(SelectedZoomLevel);

        index = delta.Y == 1 ? index < ZoomLevels.Count - 1 ? index + 1 : index : index > 0 ? index - 1 : index;
        SelectedZoomLevel = ZoomLevels[index];
    }
}
