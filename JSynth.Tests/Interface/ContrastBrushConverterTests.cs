using Avalonia.Media;
using JSynth.Converters;
using System.Globalization;
using Xunit;

namespace JSynth.Tests.Interface
{
    public class ContrastBrushConverterTests
    {
        [Theory]
        [InlineData("DarkOrange", "Black")]
        [InlineData("DarkBlue", "White")]
        [InlineData("DarkGreen", "White")]
        [InlineData("White", "Black")]
        public void Text_on_a_node_color_uses_the_more_legible_of_black_and_white(string background, string expected)
        {
            object? brush = new ContrastBrushConverter().Convert(Color.Parse(background), typeof(IBrush), null, CultureInfo.InvariantCulture);

            Assert.Equal(Color.Parse(expected), Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color);
        }
    }
}
