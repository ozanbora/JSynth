using Avalonia.Media;
using JuliaSynth.Converters;
using JuliaSynth.Model;
using Newtonsoft.Json;
using System;
using System.Linq;
using Xunit;

namespace JuliaSynth.Tests
{
    /// <summary>
    /// The pastel colours nodes start with, and designs saved before them.
    /// </summary>
    public class PaletteTests
    {
        private static readonly Node[] Kinds =
        [
            new Function(), new Float(), new Integer(), new UnsignedInteger(), new VectorNode(), new MatrixNode(),
            new SparseInput(), new ArrayBuilder(), new ArrayFile(), new DisplayNode(), new SaveFile(), new PlotNode(),
            new FilesNode(), new MapNode(), new RunningNode(), new WatchNode(),
        ];

        [Fact]
        public void Every_kind_starts_pastel_with_its_own_colour_and_readable_dark_text()
        {
            Assert.Equal(Kinds.Length, Kinds.Select(n => n.Color).Distinct().Count());

            foreach (Node node in Kinds)
            {
                // Light: black text, with plenty of contrast.
                Assert.Same(Brushes.Black, new ContrastBrushConverter().Convert(node.Color, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture));
                Assert.True(ColorContrast.Contrast(node.Color, Colors.Black) >= 10, $"{node.GetType().Name} {node.Color}");
            }
        }

        [Fact]
        public void An_old_default_colour_becomes_the_pastel_but_a_picked_one_stays()
        {
            Function function = new();
            Assert.Equal(NodePalette.Function, NodePalette.Upgrade(function, Colors.DarkOrchid));
            Assert.Equal(Colors.HotPink, NodePalette.Upgrade(function, Colors.HotPink));

            // Another kind's old default is a colour picked by hand.
            Assert.Equal(Colors.DarkOrange, NodePalette.Upgrade(function, Colors.DarkOrange));
        }

        [Fact]
        public void Reading_a_node_saved_with_an_old_default_gives_it_the_pastel()
        {
            JsonSerializerSettings settings = new() { TypeNameHandling = TypeNameHandling.Auto };
            Float old = new() { Color = Colors.DarkOrange };
            Float picked = new() { Color = Colors.HotPink };

            Node[] read = JsonConvert.DeserializeObject<Node[]>(JsonConvert.SerializeObject(new Node[] { old, picked }, settings), settings)!;

            Assert.Equal(NodePalette.Float, read[0].Color);
            Assert.Equal(Colors.HotPink, read[1].Color);
        }
    }
}
