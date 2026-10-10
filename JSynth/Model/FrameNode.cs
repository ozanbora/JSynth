using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JSynth.Model
{
    /// <summary>
    /// A titled box drawn behind a group of nodes, to say what they do: a comment on the canvas, not part of what the
    /// design works out. Its <see cref="Node.Row"/> and <see cref="Node.Column"/> are its top left cell; moving it moves
    /// the nodes in it.
    /// </summary>
    public class FrameNode : Node
    {
        // Cells left between a frame's edge and the nodes it is drawn around; more above, for its title.
        public const int Padding = 2;
        public const int TitleRows = 2;

        public const int MinColumns = 10;
        public const int MinRows = 5;

        private string _title = "Frame";
        private string _note = string.Empty;
        private int _columns = 30;
        private int _rows = 16;

        public FrameNode()
        {
            Color = NodePalette.Frame;
        }

        [JsonProperty]
        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        /// <summary>
        /// Lines below the title, e.g. what the nodes in it do and why.
        /// </summary>
        [JsonProperty]
        public string Note
        {
            get => _note;
            set => this.RaiseAndSetIfChanged(ref _note, value);
        }

        /// <summary>
        /// Its width, in cells.
        /// </summary>
        [JsonProperty]
        public int Columns
        {
            get => _columns;
            set => this.RaiseAndSetIfChanged(ref _columns, Math.Max(MinColumns, value));
        }

        /// <summary>
        /// Its height, in cells.
        /// </summary>
        [JsonProperty]
        public int Rows
        {
            get => _rows;
            set => this.RaiseAndSetIfChanged(ref _rows, Math.Max(MinRows, value));
        }

        public override bool HasOutput => false;

        /// <summary>
        /// The nodes it was drawn over without being in the group opened into it (see MainViewModel.OpenGroup): grouping
        /// the frame again leaves them out. Empty for a frame not opened from a group.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> Outside { get; init; } = [];

        /// <summary>
        /// The nodes grouping it would make one: those in it, but those it was drawn over when opened from a group.
        /// </summary>
        public IEnumerable<Node> Grouped(IEnumerable<Node> nodes) => nodes.Where(n => n is not FrameNode && Holds(n) && !Outside.Contains(n.ID));

        /// <summary>
        /// Whether <paramref name="node"/> is drawn in this frame: its middle is; a frame, all of it (a frame opened
        /// in a bigger one can have the bigger one's middle in it, and doesn't hold it).
        /// </summary>
        public bool Holds(Node node)
        {
            if (node == this)
            {
                return false;
            }

            (double top, double left, double bottom, double right) = Layout.Footprint(node);
            if (node is FrameNode)
            {
                return top >= Row && left >= Column && bottom <= Row + Rows && right <= Column + Columns;
            }

            (double middleRow, double middleColumn) = ((top + bottom) / 2, (left + right) / 2);
            return middleRow > Row && middleRow < Row + Rows && middleColumn > Column && middleColumn < Column + Columns;
        }

        /// <summary>
        /// Sizes and places the frame around <paramref name="nodes"/>, a little apart from them.
        /// </summary>
        public void Fit(IReadOnlyCollection<Node> nodes)
        {
            if (nodes.Count == 0)
            {
                return;
            }

            List<(double Top, double Left, double Bottom, double Right)> footprints = [.. nodes.Select(Layout.Footprint)];
            int left = (int)Math.Floor(footprints.Min(f => f.Left)) - Padding;
            int right = (int)Math.Ceiling(footprints.Max(f => f.Right)) + Padding;
            int top = (int)Math.Floor(footprints.Min(f => f.Top)) - Padding - TitleRows - NoteRows(right - left);
            int bottom = (int)Math.Ceiling(footprints.Max(f => f.Bottom)) + Padding;
            (Row, Column) = (Math.Max(0, top), Math.Max(0, left));
            (Rows, Columns) = (bottom - Row, right - Column);
        }

        // The note's text: about this wide a character, this tall a line, in pixels.
        private const double NoteCharacterWidth = 6.5;
        private const double NoteLineHeight = 16;

        /// <summary>
        /// About how many rows of cells the note takes, in a frame <paramref name="columns"/> wide.
        /// </summary>
        public int NoteRows(int columns)
        {
            if (Note.Trim().Length == 0)
            {
                return 0;
            }

            int perLine = Math.Max(1, (int)((columns * Constants.WIDTH - 28) / NoteCharacterWidth));
            int lines = Note.Trim().Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)perLine)));
            return (int)Math.Ceiling(lines * NoteLineHeight / Constants.HEIGHT);
        }
    }
}
