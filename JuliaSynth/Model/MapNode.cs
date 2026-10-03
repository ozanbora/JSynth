using Newtonsoft.Json;
using System.Collections.Generic;

namespace JuliaSynth.Model
{
    /// <summary>
    /// Applies a function to every value of a collection, giving a vector of the results: a vector's
    /// elements, a range's numbers, or a <see cref="FilesNode"/>'s files, each read only when its turn comes.
    /// </summary>
    public class MapNode : Node
    {
        /// <summary>
        /// Julia code for a run: map, but an error says which value (or file) it came from.
        /// </summary>
        public static string Prelude { get; } = """
            function _jsynth_map(f, items)
                if items isa JuliaSynthFiles
                    return map(items.paths) do path
                        try
                            f(items.read(path))
                        catch e
                            error(basename(path), ": ", sprint(showerror, e))
                        end
                    end
                end
                map(enumerate(items)) do (i, x)
                    try
                        f(x)
                    catch e
                        error("value ", i, ": ", sprint(showerror, e))
                    end
                end
            end
            """.ReplaceLineEndings("\n");

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
