using Newtonsoft.Json;
using ReactiveUI;
using System.Collections.Generic;

namespace JuliaSynth.Model
{
    /// <summary>
    /// Goes through a collection carrying a state along: each value updates the state with a function
    /// <c>(state, value) -> new state</c>, as Julia's <c>foldl</c>. Gives the final state, or every state
    /// (<c>accumulate</c>). A running average over the frames of a folder makes a clutter map, for one.
    /// </summary>
    public class RunningNode : Node
    {
        /// <summary>
        /// Julia code for a run: foldl or accumulate, but an error says which value (or file) it came from.
        /// </summary>
        public static string Prelude { get; } = """
            struct _JSynthFirst end
            function _jsynth_running(f, items, keep; init = _JSynthFirst())
                files = items isa JuliaSynthFiles
                state = init
                started = !(init isa _JSynthFirst)
                states = Any[]
                for (i, x) in enumerate(files ? items.paths : items)
                    try
                        value = files ? items.read(x) : x
                        state = started ? f(state, value) : value
                        started = true
                    catch e
                        error(files ? basename(x) : "value $i", ": ", sprint(showerror, e))
                    end
                    # A copy, in case f changes the state it is given rather than making a new one.
                    keep && push!(states, state isa AbstractArray ? copy(state) : state)
                end
                started || error("There are no values to go through, and no start.")
                keep ? identity.(states) : state
            end
            """.ReplaceLineEndings("\n");

        private bool _keepEach;

        /// <summary>
        /// The values to go through.
        /// </summary>
        [JsonProperty]
        public Parameter Items { get; private set; } = new("items", Function.AnyType);

        /// <summary>
        /// The state before the first value; unwired, the first value is the starting state.
        /// </summary>
        [JsonProperty]
        public Parameter Start { get; private set; } = new("start", Function.AnyType);

        /// <summary>
        /// The function updating the state: a Function node of two parameters, the state and a value.
        /// </summary>
        [JsonProperty]
        public Parameter Update { get; private set; } = new("f", Parameter.FunctionType);

        /// <summary>
        /// Gives the state after every value, as a vector, rather than only the final one.
        /// </summary>
        [JsonProperty]
        public bool KeepEach
        {
            get => _keepEach;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _keepEach, value);
                this.RaisePropertyChanged(nameof(OutputType));
                this.RaisePropertyChanged(nameof(KeepLabel));
            }
        }

        /// <summary>
        /// What the node gives, in a word: "last" state or "all" of them.
        /// </summary>
        public string KeepLabel => KeepEach ? "all" : "last";

        public override IReadOnlyList<Parameter> Inputs => [Items, Start, Update];

        public override string OutputType => KeepEach ? JuliaTypes.Vector : Function.AnyType;

        public RunningNode()
        {
            Color = NodePalette.Running;
        }

        /// <summary>
        /// Why <paramref name="function"/> can't update the state, or null when it can.
        /// </summary>
        public static string? Problem(Function function)
        {
            return function.Error is string error ? $"{function.Name} has an error: {error}"
                : function.Parameters.Count != 2 ? $"{function.Name} takes {function.Parameters.Count} values; Running updates the state with a function of two: the state, then a value."
                : null;
        }

        /// <summary>
        /// Julia code updating the state, under <paramref name="id"/>, with <paramref name="value"/>, the value of
        /// a stream's file: the state is carried from file to file (see <see cref="WatchNode"/>).
        /// </summary>
        public string JuliaTick(string id, string function, string value, string? start)
        {
            string init = start is null ? string.Empty : $"; init = {start}";
            return $"_jsynth_tick({id}, {function}, {value}, {(KeepEach ? "true" : "false")}{init})";
        }

        /// <summary>
        /// Julia code going through <paramref name="items"/> with <paramref name="function"/>, from
        /// <paramref name="start"/> or else the first value: for a run (errors say which value), or plain.
        /// </summary>
        public string Julia(string function, string items, string? start, bool instrumented)
        {
            string init = start is null ? string.Empty : $"; init = {start}";
            return instrumented ? $"_jsynth_running({function}, {items}, {(KeepEach ? "true" : "false")}{init})"
                : KeepEach ? $"collect(Iterators.accumulate({function}, {items}{init}))"
                : $"foldl({function}, {items}{init})";
        }
    }
}
