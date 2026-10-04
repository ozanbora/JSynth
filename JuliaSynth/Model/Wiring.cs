using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// Rules for wiring node outputs to function parameters.
    /// </summary>
    public static class Wiring
    {
        /// <summary>
        /// Julia only dispatches to a typed parameter with a value of exactly that type;
        /// <c>Any</c> on either side is left to be checked at run time. The same goes for element
        /// types: a plain <c>Vector</c> (a vector of Any) takes any vector, but never a matrix. An
        /// <c>AbstractVector</c> takes dense and sparse vectors alike; a <c>Vector</c> only dense ones.
        /// </summary>
        public static bool AreTypesCompatible(string outputType, string parameterType)
        {
            if (outputType == Function.AnyType || parameterType == Function.AnyType)
            {
                return true;
            }

            (string outputShape, string outputElement) = JuliaTypes.Split(outputType);
            (string parameterShape, string parameterElement) = JuliaTypes.Split(parameterType);
            return JuliaTypes.FitsShape(outputShape, parameterShape)
                && (outputElement == parameterElement || outputElement == Function.AnyType || parameterElement == Function.AnyType);
        }

        /// <summary>
        /// Whether what <paramref name="source"/> gives fits <paramref name="parameter"/>: its value's type,
        /// for a function port the function itself, and for a next port a value of the input's own type.
        /// </summary>
        public static bool Fits(Node source, Node target, Parameter parameter)
        {
            return parameter == target.Next ? source != target && AreTypesCompatible(source.OutputType, target.OutputType)
                : parameter.IsFunctionPort ? source is Function function && function.Parameters.Count == 1
                : AreTypesCompatible(source.OutputType, parameter.Type);
        }

        /// <summary>
        /// The wires into next ports (see <see cref="Node.Next"/>): they carry a value to the next file, so
        /// they make no loop, and nothing waits for them.
        /// </summary>
        public static bool IsNextWire(IEnumerable<Node> nodes, Connection connection)
        {
            return nodes.Any(n => n.ID == connection.TargetID && n.Next?.ID == connection.ParameterID);
        }

        /// <summary>
        /// Returns why <paramref name="source"/> can't be wired to <paramref name="parameter"/>
        /// of <paramref name="target"/>, or null when it can.
        /// </summary>
        public static string? Validate(IEnumerable<Node> nodes, IEnumerable<Connection> connections, Node source, Node target, Parameter parameter)
        {
            if (!source.HasOutput)
            {
                return "This node has no output to wire from.";
            }

            if (parameter == target.Next)
            {
                return source == target ? "An input already keeps its value from file to file: wire the value it takes next."
                    : !Fits(source, target, parameter) ? $"{source.OutputType} can't replace a {target.OutputType}: the next value takes its place."
                    : null;
            }

            // A function port takes the function itself, to apply to each value.
            if (!Fits(source, target, parameter))
            {
                return !parameter.IsFunctionPort ? $"{source.OutputType} can't be passed to {parameter.Signature}."
                    : source is Function function ? $"{function.Name} takes {function.Parameters.Count} values; it must take one, to be applied to each value."
                    : "Only a function can be wired here: the one to apply to each value.";
            }

            // The wire would make the target (indirectly) depend on itself.
            List<Node> all = nodes.ToList();
            if (source == target || DependsOn(connections.Where(c => !IsNextWire(all, c)), source.ID, target.ID))
            {
                return "This connection would create a loop.";
            }

            return null;
        }

        /// <summary>
        /// Whether the node <paramref name="nodeID"/> receives, directly or indirectly, the output
        /// of <paramref name="dependencyID"/>.
        /// </summary>
        public static bool DependsOn(IEnumerable<Connection> connections, string nodeID, string dependencyID)
        {
            ILookup<string, string> inputs = connections.ToLookup(c => c.TargetID, c => c.SourceID);
            HashSet<string> visited = [];
            Stack<string> pending = new([nodeID]);

            while (pending.Count > 0)
            {
                string id = pending.Pop();
                if (!visited.Add(id))
                {
                    continue;
                }

                foreach (string input in inputs[id])
                {
                    if (input == dependencyID)
                    {
                        return true;
                    }

                    pending.Push(input);
                }
            }

            return false;
        }

        /// <summary>
        /// Whether both ends of <paramref name="connection"/> still exist.
        /// </summary>
        public static bool IsAttached(IEnumerable<Node> nodes, Connection connection)
        {
            return nodes.Any(n => n.ID == connection.SourceID)
                && nodes.Any(n => n.ID == connection.TargetID && n.Ports.Any(p => p.ID == connection.ParameterID));
        }
    }
}
