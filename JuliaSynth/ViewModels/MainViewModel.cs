using Avalonia;
using Avalonia.Collections;
using JuliaSynth.Converters;
using JuliaSynth.Model;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;

namespace JuliaSynth.ViewModels;

public class MainViewModel : ReactiveObject
{
    private Vector _scrollViewerOffset = Vector.Zero;
    private double _selectedZoomLevel = 1.0;

    public ReactiveCommand<Vector, Unit> CtrlPan { get; }

    public ReactiveCommand<Vector, Unit> CtrlZoom { get; }

    public ReactiveCommand<Unit, Unit> ZoomIn { get; }

    public ReactiveCommand<Unit, Unit> ZoomOut { get; }

    public ReactiveCommand<Unit, Unit> ResetZoom { get; }

    // Capped at 3x: a node is 180x120, so beyond that a single node no longer fits on screen.
    public AvaloniaList<double> ZoomLevels { get; } = new([
        0.25, 0.33, 0.50, 0.67, 0.75, 0.90, 1.00, 1.10, 1.25, 1.50, 1.75, 2.00, 2.50, 3.00
        ]);

    // Nodes are 9x6 cells and centred on their cell; keep the defaults apart so none is hidden.
    public static AvaloniaList<Node> Nodes { get; } = new([
        new Float() { Row = 6, Column = 8, Value = 0.0 },
        new UnsignedInteger() { Row = 6, Column = 20, Value = 0 },
        new Integer() { Row = 6, Column = 32, Value = 0 },
        new Function()
        {
            Row = 16,
            Column = 20,
            Name = "f",
            ReturnType = "Float64",
            Body = "x * y",
            Parameters = { new Parameter("x", "Float64"), new Parameter("y", "Float64") },
        },
        ]);
    public double MinZoom { get; } = 0.25;

    public double MaxZoom { get; } = 3.00;

    public double SelectedZoomLevel
    {
        get => _selectedZoomLevel;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _selectedZoomLevel, value);
            this.RaisePropertyChanged(nameof(ShowShadows));
        }
    }

    /// <summary>
    /// Shadows are scaled with the zoom, and blurring them gets very expensive when zoomed in.
    /// </summary>
    public bool ShowShadows => SelectedZoomLevel <= MaxShadowZoom;

    private const double MaxShadowZoom = 2.0;


    public Vector ScrollViewerOffset
    {
        get => _scrollViewerOffset;
        set => this.RaiseAndSetIfChanged(ref _scrollViewerOffset, value);
    }

    public MainViewModel()
    {
        CtrlPan = ReactiveCommand.Create<Vector>(CtrlPanMove);
        CtrlZoom = ReactiveCommand.Create<Vector>(CtrlZoomMove);
        ZoomIn = ReactiveCommand.Create(() => StepZoom(1));
        ZoomOut = ReactiveCommand.Create(() => StepZoom(-1));
        ResetZoom = ReactiveCommand.Create(() => { SelectedZoomLevel = 1.0; });
    }

    private void CtrlPanMove(Vector offset)
    {
        ScrollViewerOffset -= offset;
    }

    private void CtrlZoomMove(Vector delta)
    {
        if (delta.Y != 0)
        {
            StepZoom(delta.Y > 0 ? 1 : -1);
        }
    }

    /// <summary>
    /// Moves to the next (<paramref name="direction"/> &gt; 0) or previous zoom level.
    /// </summary>
    private void StepZoom(int direction)
    {
        // Use the nearest level instead of IndexOf, which returns -1 for values not in the list.
        int index = 0;
        for (int i = 1; i < ZoomLevels.Count; i++)
        {
            if (Math.Abs(ZoomLevels[i] - SelectedZoomLevel) < Math.Abs(ZoomLevels[index] - SelectedZoomLevel))
            {
                index = i;
            }
        }

        index = direction > 0
            ? Math.Min(index + 1, ZoomLevels.Count - 1)
            : Math.Max(index - 1, 0);
        SelectedZoomLevel = ZoomLevels[index];
    }

    private static readonly JsonSerializerSettings settings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        Converters = { new UInt128JsonConverter() }
    };
    public static void OnShutdown()
    {
        string json = JsonConvert.SerializeObject(Nodes, settings);

        using StreamWriter writer = new("nodes.json");
        writer.Write(json);
    }

    public static void OnStartup()
    {
        if (!File.Exists("nodes.json"))
        {
            return;
        }
        Nodes.Clear();
        using StreamReader reader = new("nodes.json");
        AvaloniaList<Node>? nodes = JsonConvert.DeserializeObject<AvaloniaList<Node>>(reader.ReadToEnd(), settings);
        if (nodes is not null)
        {
            Nodes.AddRange(nodes);
        }
    }
}
