using System.Collections.Generic;
using System.Linq;

namespace JSynth.Model
{
    public static partial class ScriptGenerator
    {
        /// <summary>
        /// How a design goes through files one at a time, as they are written to a watched folder (a stream, until
        /// stopped) or those of a folder: the nodes after the files again for each, and after the inputs taking a next
        /// value; the others once, before. Folders zipped or merged come first, then a watched folder; one loop a
        /// design. Zipped or merged, the loop is led by a watched one (else the first one): zipped, the others' files of
        /// the same names go with its; merged, their files are gone through too.
        /// </summary>
        /// <param name="Root">The folder, or the clock, leading the loop; null when the design goes through no files one at a time and has no clock.</param>
        /// <param name="Twins">The folders zipped with it: their files of the same names go with its.</param>
        /// <param name="Merged">The folders merged, it among them, numbered from 1 in this order.</param>
        /// <param name="Streams">The Zip or Merge node the folders are wired to, if any.</param>
        /// <param name="Carried">The inputs taking a next value after each file.</param>
        /// <param name="Looped">The ids of the nodes worked out again for each file.</param>
        /// <param name="Gates">
        /// For each node held back while a condition is false, the nodes holding it: the Only when nodes before it and,
        /// merged, the folders before it (not through the merge) that may have no file yet.
        /// </param>
        private sealed record LoopPlan(
            Node? Root,
            List<FilesNode> Twins,
            List<FilesNode> Merged,
            Node? Streams,
            List<Node> Carried,
            HashSet<string> Looped,
            Dictionary<string, List<Node>> Gates)
        {
            public static LoopPlan Of(List<Node> order, List<Connection> connections, List<Connection> nextWires, Dictionary<string, Node> byID)
            {
                Node? streams = order.Where(n => n is ZipNode or MergeNode).FirstOrDefault(s => FolderStreams(s, connections, byID).Problem is null);
                List<FilesNode> folders = streams is null ? [] : FolderStreams(streams, connections, byID).Streams;
                Node? root = folders.OfType<WatchNode>().FirstOrDefault() ?? folders.FirstOrDefault()
                    ?? order.OfType<WatchNode>().FirstOrDefault(w => w.Error is null)
                    ?? (Node?)order.OfType<TickNode>().FirstOrDefault()
                    ?? order.OfType<FilesNode>().FirstOrDefault(f => f.EachFile && f.Error is null);
                List<FilesNode> twins = streams is ZipNode ? folders.Where(f => f != root).ToList() : [];
                List<FilesNode> merged = streams is MergeNode ? folders : [];
                List<Node> carried = root is null ? [] : nextWires.Select(c => byID[c.TargetID]).Distinct().ToList();

                HashSet<string> looped = root is null ? [] : Downstream(root, connections, byID);
                foreach (Node from in folders.Concat(carried))
                {
                    looped.UnionWith(Downstream(from, connections, byID));
                }

                // An input with a next value starts with its own, worked out once, before the loop.
                looped.ExceptWith(carried.Select(c => c.ID));

                // Not through the merge, which gives whichever file came.
                Dictionary<string, List<Node>> gates = [];
                foreach (Node gate in order.Where(n => n is WhenNode || merged.Contains(n)))
                {
                    foreach (string id in Downstream(gate, connections, byID, stop: streams is MergeNode ? streams.ID : null).Where(id => id != gate.ID))
                    {
                        (gates.TryGetValue(id, out List<Node>? list) ? list : gates[id] = []).Add(gate);
                    }
                }

                return new LoopPlan(root, twins, merged, streams, carried, looped, gates);
            }

            /// <summary>
            /// Merged, the number of <paramref name="node"/> among the folders (from 1); else 0.
            /// </summary>
            public int StreamOf(Node node) => node is FilesNode folder ? Merged.IndexOf(folder) + 1 : 0;

            /// <summary>
            /// Whether <paramref name="node"/> reads the file the loop is at (its own, or of the same name): the folder
            /// leading it, one zipped or one merged.
            /// </summary>
            public bool ReadsFrame(Node node) => node == Root || Twins.Contains(node) || StreamOf(node) > 0;

            /// <summary>
            /// The Julia expression giving the path of the file <paramref name="folder"/> reads (see
            /// <see cref="ReadsFrame"/>): the one the loop is at, or zipped, its own of that name.
            /// </summary>
            public string PathOf(FilesNode folder) => Twins.Contains(folder) ? $"joinpath({ArrayFile.PathLiteral(folder.Folder)}, basename(_path))" : "_path";

            /// <summary>
            /// Whether <paramref name="node"/> is the Merge node the folders are wired to.
            /// </summary>
            public bool IsMerge(Node node) => node == Streams && Merged.Count > 0;
        }
    }
}
