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
        Connections.CollectionChanged += (_, _) =>
        {
            MarkWiredInputs();
            History.ScheduleCommit();
        };
        AddStartingDesign();
        History.Reset();
    }

    /// <summary>
    /// Replaces the design with the starting design, as an unsaved new one. Tests start from here.
    /// </summary>
    internal static void LoadStartingDesign()
    {
        Load(new DesignDocument());
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
    public static string? Connect(Node source, Node target, Parameter parameter)
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
        MarkWiredInputs();
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

        if (node.Inputs is INotifyCollectionChanged inputs)
        {
            if (track)
            {
                inputs.CollectionChanged += ParametersChanged;
            }
            else
            {
                inputs.CollectionChanged -= ParametersChanged;
            }
        }

        foreach (Parameter parameter in node.Inputs)
        {
            Track(parameter, track);
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
        MarkWiredInputs();
        History.ScheduleCommit();
    }

    /// <summary>
    /// Tells every input whether a wire feeds it, e.g. so a builder hides the value typed in for it.
    /// </summary>
    private static void MarkWiredInputs()
    {
        HashSet<string> wired = Connections.Select(c => c.ParameterID).ToHashSet();
        foreach (Parameter parameter in Nodes.SelectMany(n => n.Inputs))
        {
            parameter.IsWired = wired.Contains(parameter.ID);
        }
    }

    private static void DesignPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Selection and wiring state aren't part of the design.
        if (e.PropertyName is not (nameof(Node.IsSelected) or nameof(Parameter.IsWired)))
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
    /// Adds a new node of <paramref name="kind"/> (Float, Integer, UnsignedInteger, VectorNode,
    /// MatrixNode, SparseInput, ArrayBuilder, ArrayFile or Function)
    /// at the first free spot at or below the given cell, and selects it.
    /// </summary>
    public static Node AddNode(string kind, int row, int column)
    {
        Node node = kind switch
        {
            nameof(Float) => new Float(),
            nameof(Integer) => new Integer(),
            nameof(UnsignedInteger) => new UnsignedInteger(),
            nameof(VectorNode) => new VectorNode(),
            nameof(MatrixNode) => new MatrixNode(),
            nameof(ArrayBuilder) => new ArrayBuilder(),
            nameof(SparseInput) => new SparseInput(),
            nameof(ArrayFile) => new ArrayFile(),
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
        return FreeFunctionName(Nodes.OfType<Function>().Select(f => f.Name).ToHashSet());
    }

    private static string FreeFunctionName(ISet<string> used)
    {
        return "fghkpqrstuvw".Select(c => c.ToString()).FirstOrDefault(n => !used.Contains(n))
            ?? Enumerable.Range(1, int.MaxValue).Select(i => $"f{i}").First(n => !used.Contains(n));
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

    #region Clipboard
    // Nodes copied as design-file text; kept in the app, not on the system clipboard.
    private static string? _clipboard;

    // How many times the clipboard was pasted: each paste lands a little further away.
    private static int _pasteCount;

    // Pasted copies are offset from the originals by this many cells, per paste.
    private const int PasteOffset = 3;

    public static bool HasClipboard => _clipboard is not null;

    /// <summary>
    /// Copies the selected nodes and the wires between them. False when nothing is selected.
    /// </summary>
    public static bool CopySelection()
    {
        if (CaptureSelection() is not string copy)
        {
            return false;
        }

        _clipboard = copy;
        _pasteCount = 0;
        return true;
    }

    public static void CutSelection()
    {
        if (CopySelection())
        {
            DeleteSelectedNodes();

            // The first paste after a cut puts the nodes back where they were.
            _pasteCount = -1;
        }
    }

    public static void PasteClipboard()
    {
        if (_clipboard is not null)
        {
            _pasteCount++;
            PasteCopy(_clipboard, _pasteCount * PasteOffset);
        }
    }

    /// <summary>
    /// Copies and pastes the selection in one go, leaving the clipboard as it was.
    /// </summary>
    public static void DuplicateSelection()
    {
        if (CaptureSelection() is string copy)
        {
            PasteCopy(copy, PasteOffset);
        }
    }

    private static string? CaptureSelection()
    {
        List<Node> selected = Nodes.Where(n => n.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return null;
        }

        // Only wires within the selection come along; the copies can't keep the others.
        HashSet<string> ids = selected.Select(n => n.ID).ToHashSet();
        List<Connection> wires = Connections.Where(c => ids.Contains(c.SourceID) && ids.Contains(c.TargetID)).ToList();

        return JsonConvert.SerializeObject(new DesignDocument { Nodes = new(selected), Connections = new(wires) }, settings);
    }

    /// <summary>
    /// Adds the copied nodes as new ones (new identities, clashing function names changed), moved by
    /// <paramref name="offset"/> cells, with their wires; the pasted nodes become the selection.
    /// </summary>
    private static void PasteCopy(string copy, int offset)
    {
        if (JsonConvert.DeserializeObject<DesignDocument>(copy, settings) is not DesignDocument pasted || pasted.Nodes.Count == 0)
        {
            return;
        }

        Dictionary<string, string> newIDs = [];
        HashSet<string> functionNames = Nodes.OfType<Function>().Select(f => f.Name).ToHashSet();
        foreach (Node node in pasted.Nodes)
        {
            string oldID = node.ID;
            node.RenewID();
            newIDs[oldID] = node.ID;

            foreach (Parameter parameter in node.AllInputs)
            {
                string oldParameterID = parameter.ID;
                parameter.RenewID();
                newIDs[oldParameterID] = parameter.ID;
            }

            if (node is Function function)
            {
                // Two Julia functions with one name would clash.
                if (functionNames.Contains(function.Name))
                {
                    function.Name = FreeFunctionName(functionNames);
                }

                _ = functionNames.Add(function.Name);
            }
        }

        // Move the group as one, as far as the canvas edges allow.
        int rows = Math.Min(offset, Constants.ROWS - 1 - pasted.Nodes.Max(n => n.Row));
        int columns = Math.Min(offset, Constants.COLUMNS - 1 - pasted.Nodes.Max(n => n.Column));
        foreach (Node node in pasted.Nodes)
        {
            node.Row += rows;
            node.Column += columns;
        }

        SelectOnly(null);
        Nodes.AddRange(pasted.Nodes);
        foreach (Node node in pasted.Nodes)
        {
            node.IsSelected = true;
        }

        Connections.AddRange(pasted.Connections.Select(c => new Connection(newIDs[c.SourceID], newIDs[c.TargetID], newIDs[c.ParameterID])));
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
        Copy = ReactiveCommand.Create(() => { _ = CopySelection(); });
        Cut = ReactiveCommand.Create(CutSelection);
        Paste = ReactiveCommand.Create(PasteClipboard);
        Duplicate = ReactiveCommand.Create(DuplicateSelection);

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

    public ReactiveCommand<Unit, Unit> Copy { get; }

    public ReactiveCommand<Unit, Unit> Cut { get; }

    public ReactiveCommand<Unit, Unit> Paste { get; }

    public ReactiveCommand<Unit, Unit> Duplicate { get; }

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
