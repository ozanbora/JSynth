using Avalonia.Collections;
using JuliaSynth.Model;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace JuliaSynth.ViewModels;

/// <summary>
/// Groups: selected nodes made one, the wires coming in its inputs and those going out its outputs; opened, its nodes
/// come back out on the canvas, in a frame named after it, to group again.
/// </summary>
public partial class MainViewModel
{
    /// <summary>
    /// Ctrl+G: the selected nodes, or the nodes in the selected frame, as one.
    /// </summary>
    public ICommand GroupNodes { get; private set; } = null!;

    /// <summary>
    /// Ctrl+Shift+G: the selected groups' nodes back on the canvas.
    /// </summary>
    public ICommand OpenGroups { get; private set; } = null!;

    private void CreateGroupCommands()
    {
        IObservable<bool> notTyping = this.WhenAnyValue(x => x.IsTypingText, typing => !typing);
        GroupNodes = ReactiveCommand.Create(() => { _ = GroupSelection(); }, notTyping);
        OpenGroups = ReactiveCommand.Create(() =>
        {
            foreach (GroupNode group in Nodes.OfType<GroupNode>().Where(g => g.IsSelected).ToList())
            {
                OpenGroup(group);
            }
        }, notTyping);
    }

    /// <summary>
    /// The design's nodes and those in its groups, however deep.
    /// </summary>
    internal static IEnumerable<Node> AllNodes(IEnumerable<Node> nodes) =>
        nodes.SelectMany(n => n is GroupNode group ? AllNodes(group.Members).Prepend(n) : [n]);

    /// <summary>
    /// Groups the selected nodes (but frames), or, a frame alone selected, the nodes in it, named after it: the wires
    /// from outside into them become its inputs, one for each value coming in, and those going out its outputs, one for
    /// each value going out, the highest on the canvas its value. Null when nothing is selected.
    /// </summary>
    public static GroupNode? GroupSelection()
    {
        List<Node> chosen = [.. Nodes.Where(n => n.IsSelected && n is not FrameNode)];
        FrameNode? frame = null;
        if (chosen.Count == 0 && Nodes.OfType<FrameNode>().Where(f => f.IsSelected).ToList() is [FrameNode selected])
        {
            frame = selected;
            chosen = [.. selected.Grouped(Nodes)];
        }

        if (chosen.Count == 0)
        {
            return null;
        }

        HashSet<string> taken = [.. AllNodes(Nodes).OfType<GroupNode>().Select(g => g.Name)];
        GroupNode group = Group(Nodes, Connections, chosen, Unique(frame is { Title.Length: > 0 } ? Name(frame.Title) : "group", taken, first: frame is not null));
        if (frame is not null)
        {
            _ = Nodes.Remove(frame);
        }

        SelectOnly(group);
        return group;
    }

    // Julia-like: letters, digits and underscores.
    private static string Name(string text)
    {
        string name = new([.. text.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_')]);
        return name.Length == 0 || char.IsDigit(name[0]) ? $"group_{name}" : name;
    }

    /// <summary>
    /// Makes <paramref name="chosen"/>, nodes of the design given, one group named <paramref name="name"/> in their
    /// place, wired as they were: see <see cref="GroupSelection"/>.
    /// </summary>
    internal static GroupNode Group(AvaloniaList<Node> nodes, AvaloniaList<Connection> connections, List<Node> chosen, string name)
    {
        HashSet<string> ids = [.. chosen.Select(n => n.ID)];
        List<Connection> inside = [.. connections.Where(c => ids.Contains(c.SourceID) && ids.Contains(c.TargetID))];
        List<Connection> incoming = [.. connections.Where(c => !ids.Contains(c.SourceID) && ids.Contains(c.TargetID))];
        List<Connection> outgoing = [.. connections.Where(c => ids.Contains(c.SourceID) && !ids.Contains(c.TargetID))];

        GroupNode group = new() { Name = name };

        // In the middle of the nodes.
        List<(double Top, double Left, double Bottom, double Right)> footprints = [.. chosen.Select(Layout.Footprint)];
        group.Row = (int)Math.Round((footprints.Min(f => f.Top) + footprints.Max(f => f.Bottom)) / 2);
        group.Column = (int)Math.Round((footprints.Min(f => f.Left) + footprints.Max(f => f.Right)) / 2);

        // An input for each value coming in, named after the input it goes into (the first, when several).
        HashSet<string> portNames = [];
        Dictionary<(string, string?), GroupInput> entryOf = [];
        foreach (IGrouping<(string SourceID, string? Output), Connection> source in incoming.GroupBy(c => (c.SourceID, c.Output)))
        {
            List<Parameter> into = [.. source.Select(c => chosen.Single(n => n.ID == c.TargetID).Ports.Single(p => p.ID == c.ParameterID))];
            string type = into.Select(p => p.Type).Distinct().Count() == 1 && !into[0].IsFunctionPort ? into[0].Type : Function.AnyType;
            GroupInput entry = new() { Port = new Parameter(Unique(into[0].Name, portNames), type), Into = [.. into.Select(p => p.ID)] };
            group.Entries.Add(entry);
            entryOf[source.Key] = entry;
        }

        // An output for each value going out, named after the node it comes from; the highest on the canvas, its value.
        HashSet<string> exitNames = [];
        Dictionary<(string, string?), (GroupOutput Exit, bool First)> exitOf = [];
        foreach (IGrouping<(string SourceID, string? Output), Connection> source in outgoing.GroupBy(c => (c.SourceID, c.Output))
            .OrderBy(g => chosen.Single(n => n.ID == g.Key.SourceID).Row).ThenBy(g => chosen.Single(n => n.ID == g.Key.SourceID).Column))
        {
            Node node = chosen.Single(n => n.ID == source.Key.SourceID);
            string shown = node is Function f ? f.Name : Name(node.GetType().Name.Replace("Node", string.Empty, StringComparison.Ordinal));
            GroupOutput exit = new() { Name = Unique(source.Key.Output is null ? shown : $"{shown}_{source.Key.Output}", exitNames), NodeID = node.ID, Output = source.Key.Output };
            exitOf[source.Key] = (exit, group.Exits.Count == 0);
            group.Exits.Add(exit);
        }

        // Inside, placed relative to the group.
        foreach (Node node in chosen)
        {
            (node.IsSelected, node.Row, node.Column) = (false, node.Row - group.Row, node.Column - group.Column);
        }

        group.Members.AddRange(chosen);
        group.Wires.AddRange(inside);

        // On the canvas: the group, wired as its nodes were.
        connections.RemoveAll([.. inside, .. incoming, .. outgoing]);
        nodes.RemoveAll(chosen);
        nodes.Add(group);
        connections.AddRange(incoming.GroupBy(c => (c.SourceID, c.Output))
            .Select(s => new Connection(s.Key.SourceID, group.ID, entryOf[s.Key].Port.ID, s.Key.Output) { Breakpoint = s.First().Breakpoint }));
        connections.AddRange(outgoing.Select(c => exitOf[(c.SourceID, c.Output)] is var (exit, first)
            ? new Connection(group.ID, c.TargetID, c.ParameterID, first ? null : exit.Name) { Breakpoint = c.Breakpoint }
            : c));
        return group;
    }

    /// <summary>
    /// <paramref name="name"/>, or with a number after it when taken (<paramref name="first"/>: the name itself is
    /// fine, else from 1: group1, group2), and now taken.
    /// </summary>
    private static string Unique(string name, ISet<string> taken, bool first = true)
    {
        string unique = first && !taken.Contains(name) ? name : Enumerable.Range(first ? 2 : 1, int.MaxValue - 2).Select(i => $"{name}{i}").First(n => !taken.Contains(n));
        _ = taken.Add(unique);
        return unique;
    }

    /// <summary>
    /// Puts <paramref name="group"/>'s nodes back on the canvas where it is, wired as before it was made, in a frame
    /// named after it (selecting the frame and grouping makes it again).
    /// </summary>
    public static void OpenGroup(GroupNode group)
    {
        List<Connection> into = [.. Connections.Where(c => c.TargetID == group.ID)];
        List<Connection> outOf = [.. Connections.Where(c => c.SourceID == group.ID)];
        Dictionary<string, string> nodeOfInput = group.Members
            .SelectMany(n => n.Ports.Select(p => (Input: p.ID, Node: n.ID)))
            .ToDictionary(p => p.Input, p => p.Node);

        foreach (Node node in group.Members)
        {
            (node.Row, node.Column) = (node.Row + group.Row, node.Column + group.Column);
        }

        Connections.RemoveAll([.. into, .. outOf]);
        _ = Nodes.Remove(group);
        List<Node> members = [.. group.Members];
        Nodes.AddRange(members);
        Connections.AddRange(group.Wires);
        Connections.AddRange(into.SelectMany(c => group.Entries.Where(e => e.Port.ID == c.ParameterID)
            .SelectMany(e => e.Into.Where(nodeOfInput.ContainsKey))
            .Select(id => new Connection(c.SourceID, nodeOfInput[id], id, c.Output) { Breakpoint = c.Breakpoint })));
        Connections.AddRange(outOf.Select(c => group.ExitOf(c.Output) is GroupOutput exit
            ? new Connection(exit.NodeID, c.TargetID, c.ParameterID, exit.Output) { Breakpoint = c.Breakpoint }
            : null).OfType<Connection>());

        FrameNode frame = new() { Title = group.Name };
        frame.Fit(members);
        MakeRoom(group, frame, members);

        // Nodes it covers that weren't in the group stay out when it is grouped again.
        frame.Outside.AddRange(Nodes.Where(n => n is not FrameNode && !members.Contains(n) && frame.Holds(n)).Select(n => n.ID));
        Nodes.Add(frame);
        SelectOnly(null);
        foreach (Node node in members)
        {
            node.IsSelected = true;
        }
    }

    /// <summary>
    /// Room for a group opened into <paramref name="frame"/>, bigger than the group was. The frame starts where the
    /// group did; beside it, what is in the way moves right, and below it, down (pushing what is in its way in turn).
    /// The frame that held the group grows around what it holds, and makes room among what is beside it the same way,
    /// and so on out to the canvas. A frame moved takes what it holds along; no other frame changes size.
    /// </summary>
    private static void MakeRoom(GroupNode group, FrameNode frame, List<Node> members)
    {
        (double top, double left, double _, double _) = Layout.Footprint(group);
        (int down, int across) = ((int)Math.Round(top) - frame.Row, (int)Math.Round(left) - frame.Column);
        foreach (Node node in members)
        {
            (node.Row, node.Column) = (node.Row + down, node.Column + across);
        }

        frame.Fit(members);

        // What each frame holds, as the design is before anything moves; the frames around the group, innermost first.
        List<Node> others = [.. Nodes.Where(n => !members.Contains(n))];
        Dictionary<FrameNode, List<Node>> held = others.OfType<FrameNode>().ToDictionary(f => f, f => others.Where(f.Holds).ToList());
        List<FrameNode> around = [.. held.Keys.Where(f => f.Holds(group)).OrderBy(f => f.Rows * f.Columns)];
        FrameNode? Parent(Node node) => held.Where(h => h.Value.Contains(node)).Select(h => h.Key).MinBy(f => f.Rows * f.Columns);

        Node growing = frame;
        (double Top, double Left, double Bottom, double Right) was = Layout.Footprint(group);
        foreach (FrameNode? container in around.Append(null))
        {
            List<Node> beside = [.. others.Where(n => n != growing && n != container && Parent(n) == container)];
            Push(beside, growing, was, held, across: true);
            Push(beside, growing, was, held, across: false);
            if (container is null)
            {
                break;
            }

            // Grown, never shrunk, around what it holds, the opened nodes and their frame among them.
            was = Layout.Footprint(container);
            List<(double Top, double Left, double Bottom, double Right)> inside =
                [.. held[container].Append(frame).Concat(members).Select(Layout.Footprint)];
            container.Columns = Math.Max(container.Columns, (int)Math.Ceiling(inside.Max(f => f.Right)) + FrameNode.Padding - container.Column);
            container.Rows = Math.Max(container.Rows, (int)Math.Ceiling(inside.Max(f => f.Bottom)) + FrameNode.Padding - container.Row);
            growing = container;
        }
    }

    /// <summary>
    /// Moves what is in the way of <paramref name="growing"/>, now bigger than it <paramref name="was"/>: of the nodes
    /// <paramref name="beside"/> it, those after where it ended (right of it, or below it), nearest first, each as far
    /// as needed to be a gap away from it and from those moved before it alongside.
    /// </summary>
    private static void Push(List<Node> beside, Node growing, (double Top, double Left, double Bottom, double Right) was,
        Dictionary<FrameNode, List<Node>> held, bool across)
    {
        (double Start, double End) Along((double Top, double Left, double Bottom, double Right) f) => across ? (f.Left, f.Right) : (f.Top, f.Bottom);
        (double Start, double End) Side((double Top, double Left, double Bottom, double Right) f) => across ? (f.Top, f.Bottom) : (f.Left, f.Right);
        int gap = across ? Layout.ColumnGap : Layout.RowGap;
        double end = Along(was).End;

        List<(double Top, double Left, double Bottom, double Right)> placed = [Layout.Footprint(growing)];
        foreach (Node node in beside.Where(n => Along(Layout.Footprint(n)).Start >= end).OrderBy(n => Along(Layout.Footprint(n)).Start))
        {
            (double Start, double End) along = Along(Layout.Footprint(node));
            (double Start, double End) side = Side(Layout.Footprint(node));
            int shift = (int)Math.Ceiling(placed
                .Where(p => Side(p).Start < side.End && side.Start < Side(p).End)
                .Select(p => Along(p).End + gap - along.Start)
                .DefaultIfEmpty(0)
                .Max());
            if (shift > 0)
            {
                foreach (Node moved in node is FrameNode f ? held[f].Prepend(node) : [node])
                {
                    (moved.Row, moved.Column) = across ? (moved.Row, moved.Column + shift) : (moved.Row + shift, moved.Column);
                }
            }

            placed.Add(Layout.Footprint(node));
        }
    }
}
