using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace JuliaSynth.Model
{
    /// <summary>
    /// The last values given to it, oldest first, as a vector: going through files one at a time, the
    /// values of the last few files, e.g. the last 6 strengths of a vibration, to smooth them.
    /// </summary>
    public class WindowNode : Node
    {
        /// <summary>
        /// Julia code keeping the last values, for runs and exported scripts alike.
        /// </summary>
        public static string Prelude { get; } = """
            # The last n values given to a JuliaSynth Window node, oldest first: x added to them.
            function _jsynth_window!(values, x, n)
                push!(values, x isa AbstractArray ? copy(x) : x)
                length(values) > n && deleteat!(values, 1:length(values) - n)
                identity.(values)
            end
            """.ReplaceLineEndings("\n");

        private long _size = 10;

        [JsonProperty]
        public Parameter Value { get; private set; } = new("value", Function.AnyType);

        /// <summary>
        /// How many of the last values it keeps, from 1 to a million.
        /// </summary>
        [JsonProperty]
        public long Size
        {
            get => _size;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _size, Math.Clamp(value, 1L, 1_000_000L));
                this.RaisePropertyChanged(nameof(SizeText));
            }
        }

        /// <summary>
        /// On the node: "last 6".
        /// </summary>
        public string SizeText => $"last {Size.ToString(CultureInfo.InvariantCulture)}";

        public override IReadOnlyList<Parameter> Inputs => [Value];

        public override string OutputType => JuliaTypes.Vector;

        public WindowNode()
        {
            Color = NodePalette.Window;
        }

        /// <summary>
        /// Julia code giving the last values with <paramref name="value"/> added to those kept in <paramref name="values"/>.
        /// </summary>
        public string Julia(string values, string value) => $"_jsynth_window!({values}, {value}, {Size.ToString(CultureInfo.InvariantCulture)})";
    }

    /// <summary>
    /// Passes its value on only when the condition holds. When it doesn't, the nodes after it aren't worked
    /// out: going through files one at a time, they keep the values they had, e.g. an alarm's first time.
    /// </summary>
    public class WhenNode : Node
    {
        [JsonProperty]
        public Parameter Value { get; private set; } = new("value", Function.AnyType);

        /// <summary>
        /// Whether the value passes: true or false.
        /// </summary>
        [JsonProperty]
        public Parameter Condition { get; private set; } = new("condition", "Bool");

        public override IReadOnlyList<Parameter> Inputs => [Value, Condition];

        public WhenNode()
        {
            Color = NodePalette.When;
        }
    }

    /// <summary>
    /// The node giving the name, or the path, of the file a loop was at, in designs saved before folders had outputs
    /// for them (see <see cref="FilesNode.NameOutput"/>): read only to be replaced by them, as a design is opened (see
    /// MainViewModel.DesignDocument.ReplaceFileNameNodes).
    /// </summary>
    public class FileNameNode : Node
    {
        /// <summary>
        /// Whether it gave the file's whole path rather than its name.
        /// </summary>
        [JsonProperty]
        public bool WholePath { get; set; }

        public override string OutputType => "String";
    }

    /// <summary>
    /// Several streams at once, in step: folders going through their files one at a time (Files nodes so set, or Watch
    /// folders) wired in, a port each, their files matched by name, e.g. the same minute of several stations. For each
    /// name every folder has, it gives their files' contents as a vector, in the order of its ports; a name not in every
    /// folder is passed over, and a watched file waits until every folder has it. Wiring its last port adds a free one.
    /// </summary>
    public class ZipNode : Node
    {
        /// <summary>
        /// The ports the folders are wired to, in the order of the vector.
        /// </summary>
        [JsonProperty]
        public ObservableCollection<Parameter> Streams { get; } = [];

        public override IReadOnlyList<Parameter> Inputs => Streams;

        public override string OutputType => JuliaTypes.Vector;

        public ZipNode()
        {
            Color = NodePalette.Zip;
            Streams.Add(NewStream(1));
            Streams.Add(NewStream(2));
        }

        /// <summary>
        /// A saved node's ports replace those it starts with.
        /// </summary>
        [OnDeserializing]
        private void Deserializing(StreamingContext context)
        {
            Streams.Clear();
        }

        /// <summary>
        /// A wire was made into it: once its last port is wired, there is a free one after it for the next folder.
        /// </summary>
        public override void Wired(IEnumerable<Connection> connections)
        {
            if (connections.Any(c => c.ParameterID == Streams[^1].ID))
            {
                Streams.Add(NewStream(Streams.Count + 1));
            }
        }

        private static Parameter NewStream(int number) => new($"stream {number.ToString(CultureInfo.InvariantCulture)}", Function.AnyType);
    }

    /// <summary>
    /// Several streams merged into one: the files of the folders wired in (all to its one port), one at a time as they
    /// come (in name order, so by time when named by it; for the same name, the folder higher on the canvas first),
    /// whichever folder each is in. It gives the file just come, and on its second output the name of the folder it is
    /// in. Each folder's own value is its latest file, so a node wired from a folder works with the latest it has, e.g.
    /// a storm's latest position for each tide gauge reading. Before a folder's first file, the nodes after it wait.
    /// </summary>
    public class MergeNode : Node
    {
        /// <summary>
        /// The port all the folders are wired to.
        /// </summary>
        [JsonProperty]
        public Parameter Folders { get; private set; } = new("folders", Function.AnyType) { TakesMany = true };

        public override IReadOnlyList<Parameter> Inputs => [Folders];

        /// <summary>
        /// Its second output: the name of the folder the file just come is in, e.g. a tide gauge's.
        /// </summary>
        public const string FolderOutput = "folder";

        public override string OutputTypeOf(string? output) => output == FolderOutput ? "String" : OutputType;

        public MergeNode()
        {
            Color = NodePalette.Merge;
        }
    }
}
