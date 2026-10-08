using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace JuliaSynth.Model
{
    /// <summary>
    /// The colour each kind of node starts with: soft pastels, one hue per kind, with dark text on them
    /// (see <see cref="Converters.ContrastBrushConverter"/>).
    /// </summary>
    public static class NodePalette
    {
        public static readonly Color Function = Color.Parse("#C9B6E4");      // lavender
        public static readonly Color Float = Color.Parse("#FFD3A5");         // peach
        public static readonly Color Integer = Color.Parse("#B5E3C4");       // mint
        public static readonly Color Unsigned = Color.Parse("#AECBEB");      // sky
        public static readonly Color Vector = Color.Parse("#A8DCD9");        // aqua
        public static readonly Color Matrix = Color.Parse("#C3C7F0");        // periwinkle
        public static readonly Color Sparse = Color.Parse("#BDB8E6");        // lilac
        public static readonly Color ArrayBuilder = Color.Parse("#BEE3B5");  // pistachio
        public static readonly Color ArrayFile = Color.Parse("#E8CDB0");     // sand
        public static readonly Color Display = Color.Parse("#D5D9DE");       // mist grey
        public static readonly Color SaveFile = Color.Parse("#E6C8AE");      // latte
        public static readonly Color Plot = Color.Parse("#B9D3CB");          // sage
        public static readonly Color Files = Color.Parse("#E9DDAF");         // wheat
        public static readonly Color Map = Color.Parse("#F3C4D6");           // rose
        public static readonly Color Watch = Color.Parse("#B8E0E8");         // ice
        public static readonly Color Window = Color.Parse("#F6D6A8");        // apricot
        public static readonly Color When = Color.Parse("#F2E6A0");          // butter
        public static readonly Color Zip = Color.Parse("#CFE3F2");           // powder blue
        public static readonly Color Merge = Color.Parse("#D8E8C8");         // celery
        public static readonly Color Frame = Color.Parse("#A9C4E0");         // slate blue, drawn faint behind the nodes

        /// <summary>
        /// Each kind's colour before the pastels, and its pastel.
        /// </summary>
        private static readonly Dictionary<Type, (Color Old, Color New)> Defaults = new()
        {
            [typeof(Model.Function)] = (Colors.DarkOrchid, Function),
            [typeof(Model.Float)] = (Colors.DarkOrange, Float),
            [typeof(Model.Integer)] = (Colors.DarkGreen, Integer),
            [typeof(UnsignedInteger)] = (Colors.DarkBlue, Unsigned),
            [typeof(VectorNode)] = (Colors.Teal, Vector),
            [typeof(MatrixNode)] = (Colors.SlateBlue, Matrix),
            [typeof(SparseInput)] = (Colors.DarkSlateBlue, Sparse),
            [typeof(Model.ArrayBuilder)] = (Colors.SeaGreen, ArrayBuilder),
            [typeof(Model.ArrayFile)] = (Colors.Sienna, ArrayFile),
            [typeof(DisplayNode)] = (Colors.DimGray, Display),
            [typeof(Model.SaveFile)] = (Colors.SaddleBrown, SaveFile),
            [typeof(PlotNode)] = (Colors.DarkSlateGray, Plot),
        };

        /// <summary>
        /// <paramref name="color"/>, or the pastel when it is the colour <paramref name="node"/>'s kind
        /// had before (a design saved earlier); a colour picked by hand stays.
        /// </summary>
        public static Color Upgrade(Node node, Color color)
        {
            return Defaults.TryGetValue(node.GetType(), out (Color Old, Color New) kind) && color == kind.Old ? kind.New : color;
        }
    }
}
