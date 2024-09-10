using Avalonia;
using ReactiveUI;
using System.Reactive;

namespace JuliaSynth.ViewModels;

public class MainViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";

    public ReactiveCommand<Vector, Unit> CtrlPan { get; }

    private Vector _scrollViewerOffset = Vector.Zero;

    public Vector ScrollViewerOffset
    {
        get => _scrollViewerOffset;
        set => this.RaiseAndSetIfChanged(ref _scrollViewerOffset, value);
    }

    public MainViewModel()
    {
        CtrlPan = ReactiveCommand.Create<Vector>(CtrlPanMove);

    }

    private void CtrlPanMove(Vector offset)
    {
        ScrollViewerOffset -= offset;

        ScrollViewerOffset = ScrollViewerOffset.X < 0 ? new Vector(0, ScrollViewerOffset.Y) : ScrollViewerOffset;
        ScrollViewerOffset = ScrollViewerOffset.Y < 0 ? new Vector(ScrollViewerOffset.X, 0) : ScrollViewerOffset;
    }
}
