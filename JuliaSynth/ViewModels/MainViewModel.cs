using Avalonia;
using Avalonia.Collections;
using JuliaSynth.Converters;
using JuliaSynth.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JuliaSynth.ViewModels;

public partial class MainViewModel : ReactiveObject
{
    private Vector _scrollViewerOffset = Vector.Zero;
    private double _selectedZoomLevel = 1.0;

    public ReactiveCommand<RxVoid, RxVoid> ZoomIn { get; }

    public ReactiveCommand<RxVoid, RxVoid> ZoomOut { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetZoom { get; }

    // The zoom shown, 100% being nodes at three quarters of their size (see <see cref="Scale"/>). Capped at 400% (3x their
    // size): a node is 180x120, so beyond that a single node no longer fits on screen.
    public AvaloniaList<double> ZoomLevels { get; } = new([
        0.25, 0.33, 0.50, 0.67, 0.75, 0.90, 1.00, 1.10, 1.25, 1.50, 1.75, 2.00, 2.50, 3.00, 4.00
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
            DesignChanged();
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
    /// Wires <paramref name="source"/>'s value (or its <paramref name="output"/> of that name, for a node with more
    /// than its value) to <paramref name="parameter"/> of <paramref name="target"/>, replacing the wire already on that
    /// parameter (on one taking many wires, the one from the same output).
    /// </summary>
    /// <returns>Why the connection isn't allowed, or null when it was made.</returns>
    public static string? Connect(Node source, Node target, Parameter parameter, string? output = null)
    {
        List<Connection> replaced = Connections.Where(c => c.ParameterID == parameter.ID && (!parameter.TakesMany || (c.SourceID == source.ID && c.Output == output))).ToList();
        string? error = Wiring.Validate(Nodes, Connections.Except(replaced), source, target, parameter, output);
        if (error is not null)
        {
            return error;
        }

        Connections.RemoveAll(replaced);
        Connections.Add(new Connection(source.ID, target.ID, parameter.ID, output));
        target.Wired(Connections);

        // Folders go together file by file.
        if (target is ZipNode or MergeNode && source is FilesNode files)
        {
            files.EachFile = true;
        }

        return null;
    }

    /// <summary>
    /// The wire ending at <paramref name="parameter"/>; on one taking many wires, the last made.
    /// </summary>
    public static Connection? ConnectionTo(Parameter parameter)
    {
        return Connections.LastOrDefault(c => c.ParameterID == parameter.ID);
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
        DesignChanged();
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
        DesignChanged();
    }

    /// <summary>
    /// Tells every input whether a wire feeds it, e.g. so a builder hides the value typed in for it.
    /// </summary>
    private static void MarkWiredInputs()
    {
        HashSet<string> wired = Connections.Select(c => c.ParameterID).ToHashSet();
        foreach (Parameter parameter in Nodes.SelectMany(n => n.Ports))
        {
            parameter.IsWired = wired.Contains(parameter.ID);
        }
    }

    private static void DesignPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Node.IsSelected))
        {
            // A node selected: the wire no longer is.
            if (sender is Node { IsSelected: true })
            {
                _selectedConnection = null;
            }

            SelectionChanged?.Invoke();
        }

        // Selection, wiring state and run results aren't part of the design.
        if (e.PropertyName is string name
            && ((sender is Node node && node.IsViewState(name)) || (sender is Parameter && name == nameof(Parameter.IsWired))))
        {
            return;
        }

        // Moving or recolouring a node changes the design, but not what it computes: keep the results.
        if (e.PropertyName is nameof(Node.Row) or nameof(Node.Column) or nameof(Node.Color))
        {
            History.ScheduleCommit();
            return;
        }

        DesignChanged();
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
        _loading = true;
        try
        {
            document.ReplaceFileNameNodes();
            Nodes.Clear();
            Connections.Clear();
            Nodes.AddRange(document.Nodes);
            Connections.AddRange(document.Connections);
            PruneConnections();
        }
        finally
        {
            _loading = false;
        }

        DesignLoaded?.Invoke();
    }
    #endregion

    #region Editing
    /// <summary>
    /// Adds a new node of <paramref name="kind"/> (Float, Integer, UnsignedInteger, VectorNode,
    /// MatrixNode, SparseInput, ArrayBuilder, ArrayFile, DisplayNode, SaveFile, PlotNode or Function)
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
            nameof(DisplayNode) => new DisplayNode(),
            nameof(SaveFile) => new SaveFile(),
            nameof(PlotNode) => new PlotNode(),
            nameof(FilesNode) => new FilesNode(),
            nameof(MapNode) => new MapNode(),
            nameof(WatchNode) => new WatchNode(),
            nameof(WindowNode) => new WindowNode(),
            nameof(WhenNode) => new WhenNode(),
            nameof(ZipNode) => new ZipNode(),
            nameof(MergeNode) => new MergeNode(),
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

    private static Connection? _selectedConnection;

    /// <summary>
    /// The wire selected, drawn standing out from the others; selecting it unselects the nodes, and selecting a node
    /// unselects it.
    /// </summary>
    public static Connection? SelectedConnection
    {
        get => _selectedConnection is Connection selected && Connections.Contains(selected) ? selected : null;
        set
        {
            if (value is not null)
            {
                SelectOnly(null);
            }

            _selectedConnection = value;
            SelectionChanged?.Invoke();
        }
    }

    public static void SelectOnly(Node? node)
    {
        if (_selectedConnection is not null)
        {
            _selectedConnection = null;
            SelectionChanged?.Invoke();
        }

        foreach (Node other in Nodes)
        {
            other.IsSelected = other == node;
        }

        if (node is not null)
        {
            node.IsSelected = true;
        }
    }

    /// <summary>
    /// Deletes the selected nodes, or the selected wire.
    /// </summary>
    public static void DeleteSelectedNodes()
    {
        if (SelectedConnection is Connection wire)
        {
            SelectedConnection = null;
            Disconnect(wire);
        }

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

            foreach (Parameter parameter in node.Next is null ? node.AllInputs : node.AllInputs.Append(node.Next))
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

        Connections.AddRange(pasted.Connections.Select(c => new Connection(newIDs[c.SourceID], newIDs[c.TargetID], newIDs[c.ParameterID], c.Output)));
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

    public double MaxZoom { get; } = 4.00;

    public double SelectedZoomLevel
    {
        get => _selectedZoomLevel;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _selectedZoomLevel, value);
            this.RaisePropertyChanged(nameof(Scale));
            this.RaisePropertyChanged(nameof(ShowShadows));
        }
    }

    /// <summary>
    /// How much 100% shrinks the nodes: at their full size they looked too big.
    /// </summary>
    public const double BaseScale = 0.75;

    /// <summary>
    /// The scale the canvas is drawn at: the zoom shown, of <see cref="BaseScale"/>.
    /// </summary>
    public double Scale => SelectedZoomLevel * BaseScale;

    /// <summary>
    /// Shadows are scaled with the zoom, and blurring them gets very expensive when zoomed in.
    /// </summary>
    public bool ShowShadows => Scale <= MaxShadowZoom;

    private const double MaxShadowZoom = 2.0;


    public Vector ScrollViewerOffset
    {
        get => _scrollViewerOffset;
        set => this.RaiseAndSetIfChanged(ref _scrollViewerOffset, value);
    }

    public MainViewModel()
    {
        ZoomIn = ReactiveCommand.Create(() => StepZoom(1));
        ZoomOut = ReactiveCommand.Create(() => StepZoom(-1));
        ResetZoom = ReactiveCommand.Create(() => { SelectedZoomLevel = 1.0; });

        // Editing the design waits while text is being typed: Delete, Ctrl+Z and the like are the text's then.
        IObservable<bool> notTyping = this.WhenAnyValue(x => x.IsTypingText, typing => !typing);
        Undo = ReactiveCommand.Create(History.Undo, this.WhenAnyValue(x => x.CanUndo, x => x.IsTypingText, (can, typing) => can && !typing));
        Redo = ReactiveCommand.Create(History.Redo, this.WhenAnyValue(x => x.CanRedo, x => x.IsTypingText, (can, typing) => can && !typing));
        DeleteSelected = ReactiveCommand.Create(DeleteSelectedNodes, notTyping);
        SelectAll = ReactiveCommand.Create(() => { foreach (Node node in Nodes) { node.IsSelected = true; } }, notTyping);
        ArrangeNodes = ReactiveCommand.Create(() => Layout.Arrange(Nodes), notTyping);
        // Escape closes the start page first.
        ClearSelection = ReactiveCommand.Create(() => { if (IsStartPageOpen) { IsStartPageOpen = false; } else { SelectOnly(null); } }, notTyping);
        Copy = ReactiveCommand.Create(() => { _ = CopySelection(); }, notTyping);
        Cut = ReactiveCommand.Create(CutSelection, notTyping);
        Paste = ReactiveCommand.Create(PasteClipboard, notTyping);
        Duplicate = ReactiveCommand.Create(DuplicateSelection, notTyping);
        CopyAsJulia = ReactiveCommand.CreateFromTask(async () =>
        {
            if (SetClipboardText is not null)
            {
                await SetClipboardText(JuliaOfSelection());
                Log("Copied as Julia: paste it in a REPL or a script.", OutputKind.Info);
            }
        }, notTyping);
        CreateRunCommands();
        CreateInspectorCommands();
        CreateStartCommands();
        CreatePackageCommands();

        History.Changed += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(IsDirty));
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(CanUndo));
            this.RaisePropertyChanged(nameof(CanRedo));
        };
    }

    private bool _isTypingText;

    /// <summary>
    /// Whether the keyboard is in a text field (a code editor, a name): set by the window. Commands
    /// editing the design wait meanwhile, so their keys reach the text.
    /// </summary>
    public bool IsTypingText
    {
        get => _isTypingText;
        set => this.RaiseAndSetIfChanged(ref _isTypingText, value);
    }

    /// <summary>
    /// Whether there is something to undo; enables the Undo command.
    /// </summary>
    public bool CanUndo => History.CanUndo;

    /// <summary>
    /// Whether there is something to redo; enables the Redo command.
    /// </summary>
    public bool CanRedo => History.CanRedo;

    public ReactiveCommand<RxVoid, RxVoid> Undo { get; }

    public ReactiveCommand<RxVoid, RxVoid> Redo { get; }

    public ReactiveCommand<RxVoid, RxVoid> DeleteSelected { get; }

    public ReactiveCommand<RxVoid, RxVoid> SelectAll { get; }

    /// <summary>
    /// Arranges the design's nodes: drawn together, apart from each other, in the order they are (see <see cref="Layout.Arrange"/>).
    /// </summary>
    public ReactiveCommand<RxVoid, RxVoid> ArrangeNodes { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearSelection { get; }

    public ReactiveCommand<RxVoid, RxVoid> Copy { get; }

    /// <summary>
    /// Puts <see cref="JuliaOfSelection"/> on the system clipboard.
    /// </summary>
    public ReactiveCommand<RxVoid, RxVoid> CopyAsJulia { get; }

    /// <summary>
    /// Puts text on the system clipboard. Set by the window; without it (in tests), nothing is copied.
    /// </summary>
    public Func<string, Task>? SetClipboardText { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> Cut { get; }

    public ReactiveCommand<RxVoid, RxVoid> Paste { get; }

    public ReactiveCommand<RxVoid, RxVoid> Duplicate { get; }

    /// <summary>
    /// Moves the view with the pointer: dragging the canvas by <paramref name="offset"/> (a drag on the empty canvas).
    /// </summary>
    public void PanBy(Vector offset)
    {
        ScrollViewerOffset -= offset;
    }

    /// <summary>
    /// Zooms one level in (wheel up) or out (wheel down): Ctrl + wheel.
    /// </summary>
    public void ZoomByWheel(Vector delta)
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

        /// <summary>
        /// A design saved before folders had a file name and path output had a File name node instead: its wires now
        /// start at the folder going through its files one at a time (a watched one first), and the node goes.
        /// </summary>
        public void ReplaceFileNameNodes()
        {
            FilesNode? folder = Nodes.OfType<WatchNode>().FirstOrDefault() ?? Nodes.OfType<FilesNode>().FirstOrDefault(f => f.EachFile);
            foreach (FileNameNode name in Nodes.OfType<FileNameNode>().ToList())
            {
                foreach (Connection wire in Connections.Where(c => c.SourceID == name.ID).ToList())
                {
                    _ = Connections.Remove(wire);
                    if (folder is not null)
                    {
                        Connections.Add(new Connection(folder.ID, wire.TargetID, wire.ParameterID, name.WholePath ? FilesNode.PathOutput : FilesNode.NameOutput));
                    }
                }

                _ = Nodes.Remove(name);
            }
        }
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
    /// Opens the design open when JuliaSynth last closed (see <see cref="RememberOpenDesign"/>), else a
    /// nodes.json from before design files, else keeps the starting design and shows the start page.
    /// </summary>
    public void StartUp()
    {
        _ = FindJuliaAtStartAsync();
        DeleteOldPlotFiles();
        StartAutoSave();

        AppSettings settings = AppSettings.Load();
        if (settings.LastUntitled && settings.LastFile is string untitled && File.Exists(untitled))
        {
            try
            {
                OpenUntitled(untitled);
                return;
            }
            catch (Exception e) when (IsFileError(e))
            {
                // Unreadable: fall back to the starting design.
            }
        }

        foreach (string? path in new[] { settings.LastUntitled ? null : settings.LastFile, LegacyFile })
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

        IsStartPageOpen = true;
    }

    /// <summary>
    /// Where a design never saved is kept between runs of JuliaSynth: next to the settings.
    /// </summary>
    internal static string UntitledFile => Path.Combine(Path.GetDirectoryName(AppSettings.SettingsPath)!, "untitled" + FileExtension);

    /// <summary>
    /// As JuliaSynth closes: remembers the design open, for <see cref="StartUp"/> to open again. A design
    /// file is reopened (as last saved); a design never saved (an example, say) is kept to reopen, unless
    /// its changes were just discarded or it is empty, which leave the start page to show.
    /// </summary>
    public void RememberOpenDesign()
    {
        if (FilePath is null && !IsDirty && Nodes.Count > 0)
        {
            try
            {
                KeepUntitled();
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Not kept: the start page shows instead.
            }
        }

        AppSettings settings = AppSettings.Load();
        settings.LastUntitled = false;
        settings.LastFile = FilePath;
        settings.Save();
    }

    /// <summary>
    /// The design kept by <see cref="RememberOpenDesign"/>, open again as never saved.
    /// </summary>
    private void OpenUntitled(string path)
    {
        ReadDesign(path);
        FilePath = null;
        DesignOpened();
    }

    public void NewDesign()
    {
        IsStartPageOpen = false;
        Load(new DesignDocument());
        History.Reset();
        FilePath = null;
        DesignOpened();
    }

    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="JsonException">The file isn't a design.</exception>
    public void OpenDesign(string path)
    {
        ReadDesign(path);
        FilePath = Path.GetFullPath(path);
        Remember(FilePath);
        DesignOpened();
    }

    /// <summary>
    /// Replaces the design with the one in <paramref name="path"/>, with no history to undo.
    /// </summary>
    private void ReadDesign(string path)
    {
        string json = File.ReadAllText(path);
        IsStartPageOpen = false;

        // nodes.json used to hold just the node list (serialized as {"$type": ..., "$values": [...]}).
        DesignDocument document = (JObject.Parse(json).ContainsKey("$values")
            ? new DesignDocument { Nodes = JsonConvert.DeserializeObject<AvaloniaList<Node>>(json, settings) ?? [] }
            : JsonConvert.DeserializeObject<DesignDocument>(json, settings))
            ?? throw new JsonSerializationException("The file holds no design.");

        Load(document);
        History.Reset();
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
        settings.LastUntitled = false;
        settings.Save();
    }
    #endregion
}
