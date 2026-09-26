using Avalonia;
using Avalonia.Collections;
using JuliaSynth.Converters;
using JuliaSynth.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// The nodes on the designer; the starting design until nodes.json is loaded.
    /// </summary>
    public static AvaloniaList<Node> Nodes { get; } = [];

    /// <summary>
    /// Wires between node outputs and function parameters.
    /// </summary>
    public static AvaloniaList<Connection> Connections { get; } = [];

    static MainViewModel()
    {
        Nodes.CollectionChanged += NodesChanged;
        AddStartingDesign();
    }

    /// <summary>
    /// A small wired example using every node type: g(f(x, z), n), i.e. x^z / n.
    /// </summary>
    private static void AddStartingDesign()
    {
        // Bitness first: changing it resets the range and clamps the value.
        Float x = new() { SelectedType = "64-bit", Value = 1.5 };
        Integer z = new() { SelectedType = "64-bit", Value = 3 };
        UnsignedInteger n = new() { SelectedType = "64-bit", Value = 2 };

        Function f = new()
        {
            Name = "f",
            ReturnType = "Float64",
            Body = "x ^ z",
            Parameters = { new Parameter("x", "Float64"), new Parameter("z", "Int64") },
        };

        Function g = new()
        {
            Name = "g",
            ReturnType = "Float64",
            Body = "y / n",
            Parameters = { new Parameter("y", "Float64"), new Parameter("n", "UInt64") },
        };

        // Nodes are 9x6 cells and centred on their cell: inputs on the left, each function
        // between the nodes it takes, so the wires run left to right without crossing.
        (x.Row, x.Column) = (5, 8);
        (z.Row, z.Column) = (13, 8);
        (n.Row, n.Column) = (21, 8);
        (f.Row, f.Column) = (9, 22);
        (g.Row, g.Column) = (17, 36);

        Nodes.AddRange([x, z, n, f, g]);

        foreach ((Node source, Function target, int parameter) in new (Node, Function, int)[] { (x, f, 0), (z, f, 1), (f, g, 0), (n, g, 1) })
        {
            if (Connect(source, target, target.Parameters[parameter]) is string error)
            {
                throw new InvalidOperationException($"Invalid starting design: {error}");
            }
        }
    }

    /// <summary>
    /// Wires <paramref name="source"/>'s output to <paramref name="parameter"/> of
    /// <paramref name="target"/>, replacing the wire already on that parameter.
    /// </summary>
    /// <returns>Why the connection isn't allowed, or null when it was made.</returns>
    public static string? Connect(Node source, Function target, Parameter parameter)
    {
        List<Connection> replaced = Connections.Where(c => c.ParameterID == parameter.ID).ToList();
        string? error = Wiring.Validate(Connections.Except(replaced), source, target, parameter);
        if (error is not null)
        {
            return error;
        }

        Connections.RemoveAll(replaced);
        Connections.Add(new Connection(source.ID, target.ID, parameter.ID));
        return null;
    }

    public static Connection? ConnectionTo(Parameter parameter)
    {
        return Connections.FirstOrDefault(c => c.ParameterID == parameter.ID);
    }

    public static void Disconnect(Connection connection)
    {
        _ = Connections.Remove(connection);
    }

    private static void NodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (Function function in e.OldItems?.OfType<Function>() ?? [])
        {
            function.Parameters.CollectionChanged -= ParametersChanged;
        }

        foreach (Function function in e.NewItems?.OfType<Function>() ?? [])
        {
            function.Parameters.CollectionChanged += ParametersChanged;
        }

        PruneConnections();
    }

    private static void ParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PruneConnections();
    }

    /// <summary>
    /// Drops the wires whose node or parameter was deleted.
    /// </summary>
    private static void PruneConnections()
    {
        Connections.RemoveAll(Connections.Where(c => !Wiring.IsAttached(Nodes, c)).ToList());
    }

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
    /// <summary>
    /// What nodes.json holds.
    /// </summary>
    public sealed class DesignDocument
    {
        public AvaloniaList<Node> Nodes { get; set; } = [];

        public AvaloniaList<Connection> Connections { get; set; } = [];
    }

    public static void OnShutdown()
    {
        string json = JsonConvert.SerializeObject(new DesignDocument { Nodes = Nodes, Connections = Connections }, settings);

        using StreamWriter writer = new("nodes.json");
        writer.Write(json);
    }

    public static void OnStartup()
    {
        if (!File.Exists("nodes.json"))
        {
            return;
        }

        string json;
        using (StreamReader reader = new("nodes.json"))
        {
            json = reader.ReadToEnd();
        }

        // nodes.json used to hold just the node list (serialized as {"$type": ..., "$values": [...]}).
        DesignDocument? document = JObject.Parse(json).ContainsKey("$values")
            ? new DesignDocument { Nodes = JsonConvert.DeserializeObject<AvaloniaList<Node>>(json, settings) ?? [] }
            : JsonConvert.DeserializeObject<DesignDocument>(json, settings);

        if (document is null)
        {
            return;
        }

        Nodes.Clear();
        Connections.Clear();
        Nodes.AddRange(document.Nodes);
        Connections.AddRange(document.Connections);
        PruneConnections();
    }
}
