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
        /// Returns why <paramref name="source"/> can't be wired to <paramref name="parameter"/>
        /// of <paramref name="target"/>, or null when it can.
        /// </summary>
        public static string? Validate(IEnumerable<Connection> connections, Node source, Node target, Parameter parameter)
        {
            if (!AreTypesCompatible(source.OutputType, parameter.Type))
            {
                return $"{source.OutputType} can't be passed to {parameter.Signature}.";
            }

            // The wire would make the target (indirectly) depend on itself.
            if (source == target || DependsOn(connections, source.ID, target.ID))
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
                && nodes.Any(n => n.ID == connection.TargetID && n.Inputs.Any(p => p.ID == connection.ParameterID));
        }
    }
}
