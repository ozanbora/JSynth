using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace JSynth.Model
{
    /// <summary>
    /// A design as one Julia script, the variable holding each node's value, and the packages it loads.
    /// </summary>
    public sealed record JuliaScript(string Text, IReadOnlyDictionary<string, string> Variables, IReadOnlyList<string> Packages)
    {
        /// <summary>
        /// Whether it runs as a stream (the design watches a folder, or has a clock): until stopped, rather than once.
        /// </summary>
        public bool Streams => Watched is not null || Clock is not null;

        /// <summary>
        /// The watched folder a stream goes through, when it <see cref="Streams"/> from one.
        /// </summary>
        public WatchNode? Watched { get; init; }

        /// <summary>
        /// The clock or button a stream ticks with, when it <see cref="Streams"/> from one.
        /// </summary>
        public TickNode? Clock { get; init; }

        /// <summary>
        /// The ids of the nodes whose values change from file to file, when the design goes through files one
        /// at a time (watched, or a Files node so set): worked out again for each, or taking a next value. Empty otherwise.
        /// </summary>
        public IReadOnlySet<string> Looped { get; init; } = new HashSet<string>();
    }

    /// <summary>
    /// Turns a design into a Julia script: the packages it needs, the function definitions, then every
    /// node's value in wiring order, each in a variable (x1, v1, f_out, ...).
    /// </summary>
    /// <remarks>
    /// A script for running is instrumented: it reports every value (see <see cref="Runtime.Reports"/>), and a
    /// failure only stops the values depending on it. An exported script is plain Julia.
    /// </remarks>
    public static partial class ScriptGenerator
    {
        private const string Indent = "    ";

        /// <param name="plotFolder">Where plots write their numbers for the app to read; JSynth's temporary folder by default.</param>
        /// <param name="stopFile">For a run of a stream: the file whose appearing ends it.</param>
        public static JuliaScript Generate(IEnumerable<Node> designNodes, IEnumerable<Connection> designConnections, bool instrumented, string? plotFolder = null, string? stopFile = null)
        {
            // Groups are their nodes.
            (List<Node> nodes, List<Connection> connections) = GroupNode.Flatten(designNodes, designConnections);
            return new Writer(nodes, connections, instrumented, plotFolder ?? Path.Combine(Path.GetTempPath(), "JSynth"), stopFile).Write();
        }

        /// <summary>
        /// The variable holding what <paramref name="wire"/> carries: its source's value, or another of its outputs
        /// (<c>merged1_folder</c>).
        /// </summary>
        private static string ValueOf(Dictionary<string, string> variables, Connection wire) =>
            wire.Output is null ? variables[wire.SourceID] : $"{variables[wire.SourceID]}_{wire.Output}";

        /// <summary>
        /// The folders wired into <paramref name="node"/> (a Zip node's in the order of its ports, a Merge node's top to
        /// bottom on the canvas), or why they can't be gone through together.
        /// </summary>
        private static (List<FilesNode> Streams, string? Problem) FolderStreams(Node node, List<Connection> connections, Dictionary<string, Node> byID)
        {
            List<Node> wired = node is ZipNode zip
                ? [.. zip.Streams.Select(p => connections.FirstOrDefault(c => c.ParameterID == p.ID)).OfType<Connection>().Select(c => byID[c.SourceID])]
                : [.. connections.Where(c => c.TargetID == node.ID).Select(c => byID[c.SourceID]).OrderBy(n => n.Row).ThenBy(n => n.Column)];
            List<FilesNode> streams = wired.OfType<FilesNode>().ToList();
            string how = node is ZipNode ? "in step" : "together";
            string? problem = wired.Count < 2 ? $"Wire in two folders or more, to go through their files {how}."
                : streams.Count < wired.Count ? $"Only folders go {how}: wire in Files nodes or Watch folders."
                : streams.FirstOrDefault(f => !f.EachFile) is FilesNode all ? $"Set {all.FolderName} to go through its files one at a time."
                : streams.Any(f => f.Error is not null) ? "A folder wired in has a problem: see its node."
                : streams.Distinct().Count() < streams.Count ? "A folder is wired in twice."
                : null;
            return (streams, problem);
        }

        /// <summary>
        /// The ids of <paramref name="root"/> and every node its value reaches, through value wires (a
        /// function wired into a function port is applied, not given a value), not past <paramref name="stop"/>.
        /// </summary>
        private static HashSet<string> Downstream(Node root, List<Connection> connections, Dictionary<string, Node> byID, string? stop = null)
        {
            HashSet<string> reached = [root.ID];
            Queue<string> pending = new([root.ID]);
            while (pending.TryDequeue(out string? id))
            {
                foreach (Connection wire in connections.Where(c => c.SourceID == id))
                {
                    bool value = byID[wire.TargetID].Inputs.Any(p => p.ID == wire.ParameterID && !p.IsFunctionPort);
                    if (value && wire.TargetID != stop && reached.Add(wire.TargetID))
                    {
                        pending.Enqueue(wire.TargetID);
                    }
                }
            }

            return reached;
        }

        /// <summary>
        /// Julia code computing <paramref name="node"/>'s value from its wired inputs, or why it can't be computed.
        /// </summary>
        private static (string? Expression, string? Problem) Expression(Node node, List<Connection> inputs, Dictionary<string, string> variables, Dictionary<string, Node> byID, bool instrumented, string plotFolder, LoopPlan? loop)
        {
            string? Input(Parameter parameter) => inputs.FirstOrDefault(c => c.ParameterID == parameter.ID) is Connection c ? ValueOf(variables, c) : null;

            // A folder has a file name and path only while it goes through its files one at a time.
            if (inputs.Any(c => c.Output is FilesNode.NameOutput or FilesNode.PathOutput && loop?.ReadsFrame(byID[c.SourceID]) != true))
            {
                return (null, "There's a file name only while files are gone through one at a time.");
            }

            switch (node)
            {
                case Float x:
                    return ($"{x.OutputType}({FloatLiteral(x.Value)})", null);
                case Integer z:
                    return ($"{z.OutputType}({z.Value.ToString(CultureInfo.InvariantCulture)})", null);
                case UnsignedInteger n:
                    return ($"{n.OutputType}({n.Value.ToString(CultureInfo.InvariantCulture)})", null);
                case ArrayInput array:
                    return array.Error is null ? (array.Julia, null) : (null, array.Error);
                case SparseInput sparse:
                    return sparse.Error is null ? (sparse.Julia, null) : (null, sparse.Error);
                case ArrayFile file:
                    return file.Error is null ? (file.Julia, null) : (null, file.Error);
                case FilesNode files when loop?.ReadsFrame(files) == true:
                    // The file the loop is at (zipped, its own of that name), read as each of the folder's files is.
                    return (files.FrameJulia(loop!.PathOf(files)), null);
                case TickNode ticks:
                    // The tick's number; a design has one stream.
                    return loop?.Root == ticks ? ("_n", null)
                        : (null, "A design has one stream: a clock, a button or a watched folder. This one isn't it.");
                case Node together when together is ZipNode or MergeNode:
                {
                    (List<FilesNode> streams, string? problem) = FolderStreams(together, inputs, byID);
                    return problem is not null ? (null, problem)
                        : loop?.Streams != together ? (null, $"Only one set of folders is gone through {(together is ZipNode ? "in step" : "together")} in a design: this one isn't.")
                        : together is ZipNode ? ($"collect(({string.Join(", ", streams.Select(s => variables[s.ID]))},))", null)

                        // Merged: the file of the folder the loop is at.
                        : (string.Concat(streams.Take(streams.Count - 1).Select((s, i) => $"_stream == {i + 1} ? {variables[s.ID]} : ")) + variables[streams[^1].ID], null);
                }
                case FilesNode files when files.EachFile:
                    return (null, files.Error ?? (files is WatchNode
                        ? "Only one folder is gone through file by file in a design: this one isn't."
                        : "Only one folder is gone through file by file in a design, and a watched one comes first: this one isn't."));
                case FilesNode files:
                    return files.Error is null ? (files.Julia, null) : (null, files.Error);
                case WindowNode window:
                    return Input(window.Value) is string kept
                        ? (window.Julia($"{variables[window.ID]}_values", kept), null)
                        : (null, "Wire in the values to keep.");
                case WhenNode only:
                    return Input(only.Value) is string passed && Input(only.Condition) is not null
                        ? (passed, null)
                        : (null, "Wire in a value, and the condition on which it passes.");
                case MapNode map:
                {
                    if (Input(map.Items) is not string items)
                    {
                        return (null, "Wire values in to go through them.");
                    }

                    // The function itself, by name: Map calls it, once per value.
                    if (inputs.FirstOrDefault(c => c.ParameterID == map.Apply.ID) is not Connection wire || byID[wire.SourceID] is not Function function)
                    {
                        return (null, "Wire in the function to apply to each value.");
                    }

                    return MapNode.Problem(function) is string problem
                        ? (null, problem)
                        : (instrumented ? $"_jsynth_map({function.Name}, {items}{(map.InParallel ? "; parallel = true" : string.Empty)})"
                            : $"{(map.InParallel ? "_jsynth_tmap" : "map")}({function.Name}, {items})", null);
                }
                case ArrayBuilder builder:
                {
                    if (builder.Error is not null)
                    {
                        return (null, builder.Error);
                    }

                    // The builder's code names its wired arguments (m, n, ...); bind them to the inputs.
                    List<string> bindings = builder.Parameters.Where(p => Input(p) is not null).Select(p => $"{p.Name} = {Input(p)}").ToList();
                    return (bindings.Count == 0 ? builder.Julia : $"let {string.Join(", ", bindings)}\n{Indent}{builder.Julia}\nend", null);
                }

                case Function function:
                {
                    if (function.Error is not null)
                    {
                        return (null, function.Error);
                    }

                    if (function.Parameters.FirstOrDefault(p => Input(p) is null) is Parameter unwired)
                    {
                        return (null, $"Parameter {unwired.Name} isn't wired.");
                    }

                    return ($"{function.Name}({string.Join(", ", function.Parameters.Select(Input))})", null);
                }

                case PlotNode plot:
                    return Input(plot.Y) is string y
                        // In a loop, a file of its own each time it is drawn: the app may still be reading the last.
                        ? (loop is not null
                            ? plot.JuliaAt($"joinpath({ArrayFile.PathLiteral(plotFolder)}, \"{plot.ID}-$(_n).jsplot\")", y, Input(plot.X))
                            : plot.Julia(Path.Combine(plotFolder, $"{plot.ID}.jsplot"), y, Input(plot.X)), null)
                        : (null, "Wire values in to plot them.");

                case OutputNode output:
                {
                    if (output.Problem is string problem)
                    {
                        return (null, problem);
                    }

                    return Input(output.Input) is string value
                        ? (output is SaveFile { SavesPerFile: true } rows ? rows.JuliaPerFile(value, named: loop is not null) : output.Julia(value), null)
                        : (null, output is DisplayNode ? "Wire a value in to show it." : "Wire a value in to save it.");
                }

                default:
                    return (null, "This node can't be computed.");
            }
        }

        /// <summary>
        /// How to draw a plot in Julia, as a comment: JSynth draws its plots itself.
        /// </summary>
        private static string PlotHint(PlotNode plot, List<Connection> inputs, Dictionary<string, string> variables)
        {
            string Wired(Parameter parameter) => ValueOf(variables, inputs.First(c => c.ParameterID == parameter.ID));

            string y = Wired(plot.Y);
            string call = plot.X.IsWired ? $"plot({Wired(plot.X)}, {y})" : $"plot({y})";
            return $"# Plotted by JSynth. With Plots.jl: using Plots; {call}";
        }

        /// <summary>
        /// A function's definition for running: evaluated on its own, after checking its packages loaded.
        /// </summary>
        private static string Definition(Function function)
        {
            string id = StringLiteral(function.ID);
            string define = Guarded(function.ID, $"include_string(@__MODULE__, {StringLiteral(function.Code)}, {StringLiteral(function.Name)})", report: false);
            if (function.RequiredPackages.Count == 0)
            {
                return define;
            }

            // Skipping the definition skips the calls too, with the reason on the node.
            string needed = string.Join(", ", function.RequiredPackages.Select(StringLiteral));
            return $"if (_why = _jsynth_package_problem({needed})) !== nothing\n{Indent}_jsynth_skip({id}, _why)\nelse\n{Indented(define)}end\n";
        }

        /// <summary>
        /// Every package <paramref name="node"/> can't do without: those loaded for it and, for a
        /// function, those its own code loads.
        /// </summary>
        private static IEnumerable<string> NeededPackages(Node node)
        {
            IEnumerable<string> packages = PackagesOf(node).Concat(ImportsOf(node));
            return node is Function function ? packages.Concat(function.RequiredPackages).Distinct() : packages;
        }

        /// <summary>
        /// The packages loaded with <c>using</c> for <paramref name="node"/>, before any function is defined.
        /// </summary>
        private static IEnumerable<string> PackagesOf(Node node)
        {
            IEnumerable<string> packages = node switch
            {
                ArrayBuilder builder => builder.Package is string package ? [package] : [],
                ArrayFile file => file.Package.Split(',', StringSplitOptions.TrimEntries),
                FilesNode files => files.Package.Split(',', StringSplitOptions.TrimEntries),
                SparseInput => ["SparseArrays"],
                OutputNode output => output.Packages,

                // A signature naming a sparse type needs SparseArrays to be defined at all.
                Function function => function.Parameters.Select(p => p.Shape).Append(function.ReturnShape).Any(JuliaTypes.IsSparse) ? ["SparseArrays"] : [],
                _ => [],
            };
            return packages;
        }

        /// <summary>
        /// The packages loaded with <c>import</c> for <paramref name="node"/>: used by qualified names only.
        /// </summary>
        private static IEnumerable<string> ImportsOf(Node node)
        {
            return node switch
            {
                ArrayFile file => FileFormats.Imports(file.Format),
                FilesNode files => FileFormats.Imports(files.Format),
                SaveFile { Format: FileFormats.Png } => ["ColorTypes"],
                _ => [],
            };
        }

        /// <summary>
        /// Loads each package on its own, so one that isn't installed only affects the nodes needing it:
        /// <paramref name="usings"/> with <c>using</c>, <paramref name="imports"/> with <c>import</c>
        /// (their functions say with <c>using</c> or <c>import</c> what to take from them).
        /// </summary>
        private static string LoadPackages(IEnumerable<string> usings, IEnumerable<string> imports)
        {
            IEnumerable<string> loads = usings.Select(p => $"({StringLiteral(p)}, \"using\")")
                .Concat(imports.Select(p => $"({StringLiteral(p)}, \"import\")"));

            return $$"""
                const _jsynth_missing = Dict{String, String}()
                for (_p, _how) in ({{string.Join(", ", loads)}},)
                    try
                        Core.eval(@__MODULE__, Meta.parse("$_how $_p"))
                    catch e
                        _jsynth_missing[_p] = sprint(showerror, e)
                    end
                end
                function _jsynth_package_problem(names...)
                    for n in names
                        haskey(_jsynth_missing, n) && return "Package $n can't be loaded. Install it with: import Pkg; Pkg.add(\"$n\")"
                    end
                    nothing
                end

                """;
        }

        /// <summary>
        /// <paramref name="code"/> in a try block reporting an error on the node, and the value when
        /// <paramref name="report"/> is set.
        /// </summary>
        private static string Guarded(string id, string code, bool report, string? variable = null)
        {
            StringBuilder block = new();
            _ = block.AppendLine("try").Append(Indented(code));
            if (report)
            {
                _ = block.AppendLine($"{Indent}_jsynth_ok({StringLiteral(id)}, {variable})");
            }

            return block.AppendLine("catch e").AppendLine($"{Indent}_jsynth_error({StringLiteral(id)}, e)").AppendLine("end").ToString();
        }

        private static string Indented(string code)
        {
            return string.Concat(code.TrimEnd('\n').Split('\n').Select(line => line.Length == 0 ? "\n" : $"{Indent}{line}\n"));
        }

        /// <summary>
        /// Every node's variable: x1, z1, v1, A1, ... by kind, f_out for a function f.
        /// </summary>
        internal static Dictionary<string, string> NameVariables(List<Node> nodes)
        {
            HashSet<string> taken = nodes.OfType<Function>().Select(f => f.Name).ToHashSet();
            Dictionary<string, string> variables = [];

            // Inputs given a name first: it is their variable (as near as Julia allows), before x1, v1, … are handed out.
            foreach (Node node in nodes)
            {
                if (VariableFor(node.PanelName) is string named)
                {
                    string name = taken.Contains(named) ? Enumerable.Range(2, int.MaxValue - 2).Select(i => $"{named}{i}").First(n => !taken.Contains(n)) : named;
                    _ = taken.Add(name);
                    variables[node.ID] = name;
                }
            }

            foreach (Node node in nodes.Where(n => !variables.ContainsKey(n.ID)))
            {
                string stem = node switch
                {
                    Float => "x",
                    Integer => "z",
                    UnsignedInteger => "n",
                    VectorNode => "v",
                    MatrixNode => "A",
                    SparseInput => "S",
                    ArrayBuilder => "B",
                    ArrayFile => "D",
                    WindowNode => "window",
                    WhenNode => "passed",
                    ZipNode => "zipped",
                    MergeNode => "merged",
                    FilesNode { EachFile: true } => "frame",
                    FilesNode => "files",
                    MapNode => "mapped",
                    DisplayNode => "shown",
                    PlotNode => "plot",
                    SaveFile => "saved",
                    Function f => $"{f.Name}_out",
                    _ => "value",
                };

                string name = node is Function && !taken.Contains(stem) ? stem : Enumerable.Range(1, int.MaxValue).Select(i => $"{stem}{i}").First(n => !taken.Contains(n));
                _ = taken.Add(name);
                variables[node.ID] = name;
            }

            return variables;
        }

        private static readonly HashSet<string> Keywords = ["baremodule", "begin", "break", "catch", "const", "continue", "do",
            "else", "elseif", "end", "export", "false", "finally", "for", "function", "global", "if", "import", "let", "local",
            "macro", "module", "public", "quote", "return", "struct", "true", "try", "using", "while", "abstract", "mutable",
            "primitive", "type", "where", "in", "isa", "outer"];

        /// <summary>
        /// The variable for a node named <paramref name="name"/>: its letters, digits and underscores, other characters
        /// one underscore ("quake at × the background": quake_at_the_background). A name Julia has already or a keyword
        /// gets an underscore after it (length_), and one starting with a digit before it. Null for no name.
        /// </summary>
        internal static string? VariableFor(string name)
        {
            string variable = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"[^\p{L}\p{Nd}_]+", "_").Trim('_');
            if (variable.Length == 0)
            {
                return null;
            }

            if (char.IsDigit(variable[0]))
            {
                variable = "_" + variable;
            }

            return Keywords.Contains(variable) || JuliaNames.Taken.Contains(variable) ? variable + "_" : variable;
        }

        /// <summary>
        /// The nodes with every node after the ones wired into it; otherwise in design order.
        /// </summary>
        private static List<Node> WiringOrder(List<Node> nodes, List<Connection> connections)
        {
            List<Node> order = [];
            HashSet<string> done = [];
            ILookup<string, string> inputs = connections.ToLookup(c => c.TargetID, c => c.SourceID);

            while (order.Count < nodes.Count)
            {
                // The wiring rules keep out loops; should one get in anyway, take the rest as they are.
                Node next = nodes.FirstOrDefault(n => !done.Contains(n.ID) && inputs[n.ID].All(done.Contains))
                    ?? nodes.First(n => !done.Contains(n.ID));
                order.Add(next);
                _ = done.Add(next.ID);
            }

            return order;
        }

        private static string FloatLiteral(double value)
        {
            return double.IsNaN(value) ? "NaN"
                : double.IsPositiveInfinity(value) ? "Inf"
                : double.IsNegativeInfinity(value) ? "-Inf"
                : value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A Julia string literal holding <paramref name="text"/> exactly.
        /// </summary>
        public static string StringLiteral(string text)
        {
            StringBuilder literal = new("\"");
            foreach (char c in text)
            {
                _ = literal.Append(c switch
                {
                    '\\' => "\\\\",
                    '"' => "\\\"",
                    '$' => "\\$",
                    '\n' => "\\n",
                    '\r' => string.Empty,
                    '\t' => "\\t",
                    _ => c.ToString(),
                });
            }

            return literal.Append('"').ToString();
        }
    }
}
