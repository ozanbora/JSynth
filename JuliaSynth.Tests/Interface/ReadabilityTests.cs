using Avalonia.Media;
using JuliaSynth.Converters;
using System;
using System.Globalization;
using System.Numerics;
using Xunit;

namespace JuliaSynth.Tests.Interface
{
    public class ReadabilityTests
    {
        private static readonly Color DarkCanvas = Color.Parse("#1E1E1E");
        private static readonly Color LightCanvas = Color.Parse("#F0F0F0");

        [Theory]
        [InlineData("0", "0")]
        [InlineData("-2147483648", "-2,147,483,648")]
        [InlineData("4294967295", "4,294,967,295")]
        [InlineData("-9223372036854775808", "−2⁶³")]
        [InlineData("9223372036854775807", "2⁶³ − 1")]
        [InlineData("18446744073709551615", "2⁶⁴ − 1")]
        [InlineData("340282366920938463463374607431768211455", "2¹²⁸ − 1")]
        [InlineData("123456789012345678", "1.23457E+17")]
        public void Integers_are_shown_so_they_fit_on_a_node(string value, string expected)
        {
            string shown = CompactIntegerConverter.Format(BigInteger.Parse(value), CultureInfo.InvariantCulture);

            Assert.Equal(expected, shown);
            Assert.True(shown.Length <= CompactIntegerConverter.MaxLength);
        }

        [Fact]
        public void The_converter_takes_the_node_value_types()
        {
            CompactIntegerConverter converter = new();

            Assert.Equal("−2⁶³", converter.Convert(long.MinValue, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Equal("2⁶⁴ − 1", converter.Convert((UInt128)ulong.MaxValue, typeof(string), null, CultureInfo.InvariantCulture));
        }

        [Theory]
        [InlineData("DarkBlue")]
        [InlineData("DarkGreen")]
        [InlineData("DarkOrchid")]
        [InlineData("DarkOrange")]
        [InlineData("Black")]
        public void Wires_stand_out_on_the_dark_canvas(string node)
        {
            Color wire = ColorContrast.StandOut(Color.Parse(node), DarkCanvas);

            Assert.True(ColorContrast.Contrast(wire, DarkCanvas) >= ColorContrast.MinimumContrast);
        }

        [Theory]
        [InlineData("Yellow")]
        [InlineData("White")]
        [InlineData("DarkOrange")]
        public void Wires_stand_out_on_the_light_canvas(string node)
        {
            Color wire = ColorContrast.StandOut(Color.Parse(node), LightCanvas);

            Assert.True(ColorContrast.Contrast(wire, LightCanvas) >= ColorContrast.MinimumContrast);
        }

        [Fact]
        public void A_wire_that_already_stands_out_keeps_the_node_color()
        {
            Assert.Equal(Colors.DarkOrange, ColorContrast.StandOut(Colors.DarkOrange, DarkCanvas));
        }
    }
}
