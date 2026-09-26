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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

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

    /// <summary>
    /// Undo and redo for everything saved in nodes.json.
    /// </summary>
    public static UndoHistory History { get; } = new(CaptureDesign, RestoreDesign);

    static MainViewModel()
    {
        Nodes.CollectionChanged += NodesChanged;
        Connections.CollectionChanged += (_, _) => History.ScheduleCommit();
        AddStartingDesign();
        History.Reset();
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

    #region Change tracking
    private static void NodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (Node node in e.OldItems?.OfType<Node>() ?? [])
        {
            Track(node, false);
        }

        foreach (Node node in e.NewItems?.OfType<Node>() ?? [])
        {
            Track(node, true);
        }

        PruneConnections();
        History.ScheduleCommit();
    }

    private static void Track(Node node, bool track)
    {
        if (track)
        {
            node.PropertyChanged += DesignPropertyChanged;
        }
        else
        {
            node.PropertyChanged -= DesignPropertyChanged;
        }

        if (node is Function function)
        {
            if (track)
            {
                function.Parameters.CollectionChanged += ParametersChanged;
            }
            else
            {
                function.Parameters.CollectionChanged -= ParametersChanged;
            }

            foreach (Parameter parameter in function.Parameters)
            {
                Track(parameter, track);
            }
        }
    }

    private static void Track(Parameter parameter, bool track)
    {
        if (track)
        {
            parameter.PropertyChanged += DesignPropertyChanged;
        }
        else
        {
            parameter.PropertyChanged -= DesignPropertyChanged;
        }
    }

    private static void ParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (Parameter parameter in e.OldItems?.OfType<Parameter>() ?? [])
        {
            Track(parameter, false);
        }

        foreach (Parameter parameter in e.NewItems?.OfType<Parameter>() ?? [])
        {
            Track(parameter, true);
        }

        PruneConnections();
        History.ScheduleCommit();
    }

    private static void DesignPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Selection isn't part of the design.
        if (e.PropertyName != nameof(Node.IsSelected))
        {
            History.ScheduleCommit();
        }
    }

    private static string CaptureDesign()
    {
        return JsonConvert.SerializeObject(new DesignDocument { Nodes = Nodes, Connections = Connections }, settings);
    }

    private static void RestoreDesign(string json)
    {
        HashSet<string> existing = Nodes.Select(n => n.ID).ToHashSet();
        HashSet<string> selected = Nodes.Where(n => n.IsSelected).Select(n => n.ID).ToHashSet();

        if (JsonConvert.DeserializeObject<DesignDocument>(json, settings) is DesignDocument document)
        {
            Load(document);
        }

        // Keep the selection, and select what came back (e.g. undoing a delete) so it is easy to spot.
        foreach (Node node in Nodes)
        {
            node.IsSelected = selected.Contains(node.ID) || !existing.Contains(node.ID);
        }
    }

    private static void Load(DesignDocument document)
    {
        Nodes.Clear();
        Connections.Clear();
        Nodes.AddRange(document.Nodes);
        Connections.AddRange(document.Connections);
        PruneConnections();
    }
    #endregion

    #region Editing
    /// <summary>
    /// Adds a new node of <paramref name="kind"/> (Float, Integer, UnsignedInteger or Function)
    /// at the first free spot at or below the given cell, and selects it.
    /// </summary>
    public static Node AddNode(string kind, int row, int column)
    {
        Node node = kind switch
        {
            nameof(Float) => new Float(),
            nameof(Integer) => new Integer(),
            nameof(UnsignedInteger) => new UnsignedInteger(),
            _ => new Function() { Name = FreeFunctionName(), Parameters = { new Parameter("x", Function.AnyType) } },
        };

        // Nodes are 9x6 cells centred on their cell; step down until this one overlaps none.
        row = Math.Clamp(row, 3, Constants.ROWS - 4);
        column = Math.Clamp(column, 5, Constants.COLUMNS - 5);
        while (row < Constants.ROWS - 4 && Nodes.Any(n => Math.Abs(n.Row - row) < 7 && Math.Abs(n.Column - column) < 10))
        {
            row++;
        }

        (node.Row, node.Column) = (row, column);
        SelectOnly(node);
        Nodes.Add(node);
        return node;
    }

    private static string FreeFunctionName()
    {
        HashSet<string> used = Nodes.OfType<Function>().Select(f => f.Name).ToHashSet();
        return "fghkpqrstuvw".Select(c => c.ToString()).FirstOrDefault(n => !used.Contains(n))
            ?? $"f{Nodes.Count + 1}";
    }

    public static void SelectOnly(Node? node)
    {
        foreach (Node other in Nodes)
        {
            other.IsSelected = other == node;
        }

        if (node is not null)
        {
            node.IsSelected = true;
        }
    }

    public static void DeleteSelectedNodes()
    {
        Nodes.RemoveAll(Nodes.Where(n => n.IsSelected).ToList());
    }
    #endregion

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

        IObservable<Unit> historyChanged = Observable.FromEventPattern(h => History.Changed += h, h => History.Changed -= h)
            .Select(_ => Unit.Default)
            .StartWith(Unit.Default);
        Undo = ReactiveCommand.Create(History.Undo, historyChanged.Select(_ => History.CanUndo));
        Redo = ReactiveCommand.Create(History.Redo, historyChanged.Select(_ => History.CanRedo));
        DeleteSelected = ReactiveCommand.Create(DeleteSelectedNodes);
        SelectAll = ReactiveCommand.Create(() => { foreach (Node node in Nodes) { node.IsSelected = true; } });
        ClearSelection = ReactiveCommand.Create(() => SelectOnly(null));

        History.Changed += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(IsDirty));
            this.RaisePropertyChanged(nameof(Title));
        };
    }

    public ReactiveCommand<Unit, Unit> Undo { get; }

    public ReactiveCommand<Unit, Unit> Redo { get; }

    public ReactiveCommand<Unit, Unit> DeleteSelected { get; }

    public ReactiveCommand<Unit, Unit> SelectAll { get; }

    public ReactiveCommand<Unit, Unit> ClearSelection { get; }

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
        Converters = { new UInt128JsonConverter() },
        // Design files name the types to create; only allow the design's own.
        SerializationBinder = new DesignSerializationBinder(),
    };

    #region Files
    /// <summary>
    /// What a design file holds.
    /// </summary>
    public sealed class DesignDocument
    {
        public AvaloniaList<Node> Nodes { get; set; } = [];

        public AvaloniaList<Connection> Connections { get; set; } = [];
    }

    public const string FileExtension = ".jsynth";

    // Before design files existed, the design was always saved here, next to the program.
    private const string LegacyFile = "nodes.json";

    private string? _filePath;

    /// <summary>
    /// The open design file, or null for a design that was never saved.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref _filePath, value);
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    public bool IsDirty => History.IsDirty;

    public string Title => $"{(FilePath is null ? "Untitled" : Path.GetFileName(FilePath))}{(IsDirty ? " •" : string.Empty)} - JuliaSynth";

    /// <summary>
    /// Opens the last design, else a nodes.json from before design files, else keeps the starting design.
    /// </summary>
    public void StartUp()
    {
        foreach (string? path in new[] { AppSettings.Load().LastFile, LegacyFile })
        {
            if (path is not null && File.Exists(path))
            {
                try
                {
                    OpenDesign(path);
                    return;
                }
                catch (Exception e) when (IsFileError(e))
                {
                    // Unreadable: try the next one, and fall back to the starting design.
                }
            }
        }
    }

    public void NewDesign()
    {
        Load(new DesignDocument());
        History.Reset();
        FilePath = null;
    }

    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="JsonException">The file isn't a design.</exception>
    public void OpenDesign(string path)
    {
        string json = File.ReadAllText(path);

        // nodes.json used to hold just the node list (serialized as {"$type": ..., "$values": [...]}).
        DesignDocument document = (JObject.Parse(json).ContainsKey("$values")
            ? new DesignDocument { Nodes = JsonConvert.DeserializeObject<AvaloniaList<Node>>(json, settings) ?? [] }
            : JsonConvert.DeserializeObject<DesignDocument>(json, settings))
            ?? throw new JsonSerializationException("The file holds no design.");

        Load(document);
        History.Reset();
        FilePath = Path.GetFullPath(path);
        Remember(FilePath);
    }

    /// <exception cref="IOException">The file can't be written.</exception>
    public void SaveDesign(string path)
    {
        History.Commit();
        File.WriteAllText(path, CaptureDesign());
        History.MarkSaved();
        FilePath = Path.GetFullPath(path);
        Remember(FilePath);
    }

    /// <summary>
    /// Errors that mean "this file can't be used", as opposed to bugs.
    /// </summary>
    public static bool IsFileError(Exception e)
    {
        return e is IOException or UnauthorizedAccessException or JsonException;
    }

    private static void Remember(string path)
    {
        AppSettings settings = AppSettings.Load();
        settings.LastFile = path;
        settings.Save();
    }
    #endregion
}
