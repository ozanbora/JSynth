using Newtonsoft.Json;
using ReactiveUI;
using System.Collections.Generic;

namespace JSynth.Model
{
    /// <summary>
    /// Applies a function to every value of a collection, giving a vector of the results: a vector's
    /// elements, a range's numbers, or a <see cref="FilesNode"/>'s files, each read only when its turn comes.
    /// </summary>
    public class MapNode : Node
    {
        /// <summary>
        /// Julia code for a run: map, but an error says which value (or file) it came from. In parallel, the values are
        /// shared out among Julia's threads; the first failure, in order, is the one said.
        /// </summary>
        public static string Prelude { get; } = """
            function _jsynth_map(f, items; parallel = false)
                jobs = items isa JSynthFiles ? [(basename(path), () -> f(items.read(path))) for path in items.paths] :
                    [(string("value ", i), () -> f(x)) for (i, x) in enumerate(items)]
                work((label, job)) = try
                    job()
                catch e
                    error(label, ": ", sprint(showerror, e))
                end
                parallel || return map(work, jobs)
                tasks = [Threads.@spawn work(j) for j in jobs]
                try
                    fetch.(tasks)
                catch e
                    e isa TaskFailedException ? throw(e.task.exception) : rethrow()
                end
            end
            """.ReplaceLineEndings("\n");

        /// <summary>
        /// Julia code for a Map in parallel, for runs and exported scripts alike: f applied to each value on as many
        /// threads as Julia has (started with <c>--threads=auto</c>), the results in order; files are each read on
        /// the thread working on them.
        /// </summary>
        public static string ParallelPrelude { get; } = """
            # f applied to each value on Julia's threads (julia --threads=auto), the results in order; made by a JSynth Map node.
            function _jsynth_tmap(f, items)
                jobs = items isa JSynthFiles ? [() -> f(items.read(path)) for path in items.paths] : [() -> f(x) for x in items]
                fetch.([Threads.@spawn job() for job in jobs])
            end
            """.ReplaceLineEndings("\n");

        private bool _inParallel;

        /// <summary>
        /// Whether the values are worked through on all of Julia's threads at once, rather than one after the
        /// other: for many files, or slow work on each value, when the work on one doesn't depend on another's.
        /// </summary>
        [JsonProperty]
        public bool InParallel
        {
            get => _inParallel;
            set
            {
                _ = this.RaiseAndSetIfChanged(ref _inParallel, value);
                this.RaisePropertyChanged(nameof(Label));
            }
        }

        /// <summary>
        /// On the node: "map", or "map ∥" in parallel.
        /// </summary>
        public string Label => InParallel ? "map ∥" : "map";

        /// <summary>
        /// The values to go through.
        /// </summary>
        [JsonProperty]
        public Parameter Items { get; private set; } = new("items", Function.AnyType);

        /// <summary>
        /// The function applied to each value: a Function node of one parameter.
        /// </summary>
        [JsonProperty]
        public Parameter Apply { get; private set; } = new("f", Parameter.FunctionType);

        public override IReadOnlyList<Parameter> Inputs => [Items, Apply];

        public override string OutputType => JuliaTypes.Vector;

        public MapNode()
        {
            Color = NodePalette.Map;
        }

        /// <summary>
        /// Why <paramref name="function"/> can't be applied to each value, or null when it can.
        /// </summary>
        public static string? Problem(Function function)
        {
            return function.Error is string error ? $"{function.Name} has an error: {error}"
                : function.Parameters.Count != 1 ? $"{function.Name} takes {function.Parameters.Count} values; Map applies a function of one value."
                : null;
        }
    }
}
