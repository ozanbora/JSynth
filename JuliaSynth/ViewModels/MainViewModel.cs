using Avalonia;
using Avalonia.Collections;
using ReactiveUI;
using System.Linq;
using System.Reactive;

namespace JuliaSynth.ViewModels;

public class MainViewModel : ViewModelBase
{
    private Vector _scrollViewerOffset = Vector.Zero;
    private double _selectedZoomLevel = 1.0;

    public ReactiveCommand<Vector, Unit> CtrlPan { get; }

    public ReactiveCommand<Vector, Unit> CtrlZoom { get; }

    public AvaloniaList<double> ZoomLevels { get; }

    public double MinZoom => ZoomLevels.Min();

    public double MaxZoom => ZoomLevels.Max();

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

        ZoomLevels = new AvaloniaList<double>([1.00, 1.25, 1.50, 2.00, 3.00, 4.00, 5.00, 8.00, 10.00]);
        SelectedZoomLevel = ZoomLevels[1];
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
