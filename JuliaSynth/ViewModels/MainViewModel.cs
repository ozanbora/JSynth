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

    public AvaloniaList<double> ZoomLevels { get; } = new([
        0.25, 0.50, 0.80, 1.00, 1.25, 1.50, 2.00, 3.00, 4.00, 5.00, 8.00, 10.00
        ]);

    public static AvaloniaList<Node> Nodes { get; } = new([
        new Float() { Row = 5, Column = 10, Value = 0.0 },
        new UnsignedInteger() { Row = 3, Column = 4, Value = 0 },
        new Integer() { Row = 3, Column = 4, Value = 0 },
        new Function()
        {
            Row = 6,
            Column = 9,
            Name = "f",
            ReturnType = "Float64",
            Body = "x * y",
            Parameters = { new Parameter("x", "Float64"), new Parameter("y", "Float64") },
        },
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
        if (delta.Y == 0)
        {
            return;
        }

        // Use the nearest level instead of IndexOf, which returns -1 for values not in the list.
        int index = 0;
        for (int i = 1; i < ZoomLevels.Count; i++)
        {
            if (Math.Abs(ZoomLevels[i] - SelectedZoomLevel) < Math.Abs(ZoomLevels[index] - SelectedZoomLevel))
            {
                index = i;
            }
        }

        index = delta.Y > 0
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
