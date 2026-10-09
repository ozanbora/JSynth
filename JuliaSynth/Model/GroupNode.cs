using Avalonia.Collections;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuliaSynth.Model
{
    /// <summary>
    /// An input of a group: its port, and the inputs inside it the value goes into.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class GroupInput
    {
        [JsonProperty]
        public Parameter Port { get; init; } = new("x", Function.AnyType);

        /// <summary>
        /// The <see cref="Parameter.ID"/>s of the inputs inside the group it goes into.
        /// </summary>
        [JsonProperty]
        public List<string> Into { get; init; } = [];
    }

    /// <summary>
    /// An output of a group: its name, and the node inside and the output of it whose value it gives.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class GroupOutput
    {
        [JsonProperty]
        public string Name { get; init; } = string.Empty;

        [JsonProperty]
        public string NodeID { get; init; } = string.Empty;

        /// <summary>
        /// Which output of that node: null for its value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Output { get; init; }
    }

    /// <summary>
    /// A row of a group's outputs after its value, as the node shows it: "estimate: Float64".
    /// </summary>
    public sealed record GroupExitRow(string Name, string Caption);

    /// <summary>
    /// Nodes grouped into one: the wires that came in are its inputs, those that went out its outputs (the first its
    /// value, the others by name). What it works out is what its nodes do: a script has them as if they weren't grouped
    /// (see <see cref="Flatten"/>). Opened, its nodes come back out on the canvas.
    /// </summary>
    public class GroupNode : Node
    {
        private string _name = "group";

        public GroupNode()
        {
            Color = NodePalette.Group;
        }

        [JsonProperty]
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, string.IsNullOrWhiteSpace(value) ? _name : value.Trim());
        }

        /// <summary>
        /// The nodes inside, placed relative to the group: at their place on the canvas less the group's.
        /// </summary>
        [JsonProperty]
        public AvaloniaList<Node> Members { get; init; } = [];

        /// <summary>
        /// The wires between the nodes inside.
        /// </summary>
        [JsonProperty]
        public AvaloniaList<Connection> Wires { get; init; } = [];

        [JsonProperty]
        public List<GroupInput> Entries { get; init; } = [];

        [JsonProperty]
        public List<GroupOutput> Exits { get; init; } = [];

        private IReadOnlyList<Parameter>? _ports;

        public override IReadOnlyList<Parameter> Inputs => _ports ??= [.. Entries.Select(e => e.Port)];

        /// <summary>
        /// The outputs after its value, on rows of their own.
        /// </summary>
        public IReadOnlyList<GroupOutput> OtherExits => [.. Exits.Skip(1)];

        public IReadOnlyList<GroupExitRow> OtherExitRows => [.. OtherExits.Select(e => new GroupExitRow(e.Name, $"{e.Name}: {OutputTypeOf(e.Name)}"))];

        public override int ConnectorRows => Math.Max(Inputs.Count, Exits.Count - 1);

        public override bool HasOutput => Exits.Count > 0;

        public override string OutputType => OutputTypeOf(null);

        public override string OutputTypeOf(string? output) =>
            ExitOf(output) is GroupOutput exit && Members.FirstOrDefault(n => n.ID == exit.NodeID) is Node inside
                ? inside.OutputTypeOf(exit.Output)
                : Function.AnyType;

        /// <summary>
        /// The output given by <paramref name="output"/>: null for the group's value, the first.
        /// </summary>
        public GroupOutput? ExitOf(string? output) => output is null ? Exits.FirstOrDefault() : Exits.Skip(1).FirstOrDefault(e => e.Name == output);

        /// <summary>
        /// "3 nodes", under its name.
        /// </summary>
        public string Summary => $"{Members.Count} {(Members.Count == 1 ? "node" : "nodes")}";

        /// <summary>
        /// Gives the nodes inside, their inputs and the wires between them identities of their own, for a pasted copy.
        /// </summary>
        internal void RenewInside()
        {
            Dictionary<string, string> renewed = [];
            foreach (Node node in Members)
            {
                string old = node.ID;
                node.RenewID();
                renewed[old] = node.ID;
                foreach (Parameter parameter in node.Next is null ? node.AllInputs : node.AllInputs.Append(node.Next))
                {
                    string oldParameter = parameter.ID;
                    parameter.RenewID();
                    renewed[oldParameter] = parameter.ID;
                }

                (node as GroupNode)?.RenewInside();
            }

            List<Connection> wires = [.. Wires.Select(c => new Connection(renewed[c.SourceID], renewed[c.TargetID], renewed[c.ParameterID], c.Output) { Breakpoint = c.Breakpoint })];
            Wires.Clear();
            Wires.AddRange(wires);
            foreach (GroupInput entry in Entries)
            {
                List<string> into = [.. entry.Into.Select(id => renewed.GetValueOrDefault(id, id))];
                entry.Into.Clear();
                entry.Into.AddRange(into);
            }

            List<GroupOutput> exits = [.. Exits.Select(e => new GroupOutput { Name = e.Name, NodeID = renewed.GetValueOrDefault(e.NodeID, e.NodeID), Output = e.Output })];
            Exits.Clear();
            Exits.AddRange(exits);
        }

        /// <summary>
        /// The nodes and the wires of a design with every group (in groups too) taken apart: its nodes in its place, and
        /// the wires into and out of it to and from them. What a script is written from.
        /// </summary>
        public static (List<Node> Nodes, List<Connection> Connections) Flatten(IEnumerable<Node> nodes, IEnumerable<Connection> connections)
        {
            List<Node> flat = [];
            List<Connection> wires = [.. connections];
            Dictionary<string, GroupNode> groups = [];

            void Open(IEnumerable<Node> level)
            {
                foreach (Node node in level)
                {
                    if (node is GroupNode group)
                    {
                        groups[group.ID] = group;
                        wires.AddRange(group.Wires);
                        Open(group.Members);
                    }
                    else
                    {
                        flat.Add(node);
                    }
                }
            }

            Open(nodes);
            if (groups.Count == 0)
            {
                return (flat, wires);
            }

            // Into a group: into the inputs inside it; out of one: out of the node inside. Groups in groups: again.
            Dictionary<string, string> nodeOfInput = groups.Values
                .SelectMany(g => g.Members.SelectMany(n => (n.Next is null ? n.Inputs : n.Inputs.Append(n.Next)).Select(p => (p.ID, n.ID))))
                .GroupBy(p => p.Item1)
                .ToDictionary(g => g.Key, g => g.First().Item2);
            bool changed = true;
            while (changed)
            {
                changed = false;
                List<Connection> next = [];
                foreach (Connection wire in wires)
                {
                    if (groups.TryGetValue(wire.TargetID, out GroupNode? into) && into.Entries.FirstOrDefault(e => e.Port.ID == wire.ParameterID) is GroupInput entry)
                    {
                        next.AddRange(entry.Into.Where(nodeOfInput.ContainsKey).Select(id => new Connection(wire.SourceID, nodeOfInput[id], id, wire.Output) { Breakpoint = wire.Breakpoint }));
                        changed = true;
                    }
                    else if (groups.TryGetValue(wire.SourceID, out GroupNode? from) && from.ExitOf(wire.Output) is GroupOutput exit)
                    {
                        next.Add(new Connection(exit.NodeID, wire.TargetID, wire.ParameterID, exit.Output) { Breakpoint = wire.Breakpoint });
                        changed = true;
                    }
                    else if (!groups.ContainsKey(wire.TargetID) && !groups.ContainsKey(wire.SourceID))
                    {
                        next.Add(wire);
                    }
                }

                wires = next;
            }

            return (flat, wires);
        }

        public override bool IsViewState(string property)
        {
            return base.IsViewState(property) || property is nameof(Summary) or nameof(OtherExits) or nameof(OtherExitRows);
        }
    }
}
