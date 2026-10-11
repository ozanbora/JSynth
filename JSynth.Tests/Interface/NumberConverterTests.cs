using Avalonia.Data;
using JSynth.Converters;
using System;
using System.Globalization;
using Xunit;

namespace JSynth.Tests.Interface
{
    /// <summary>
    /// What reaches the model from the number boxes in the parameters panel.
    /// </summary>
    public class NumberConverterTests
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        [Theory]
        [InlineData("2.5", 2.5)]
        [InlineData("1e-7", 1e-7)]
        [InlineData("-3.4028234663852886E+38", -3.4028234663852886E+38)]
        public void A_typed_float_reaches_the_model(string text, double expected)
        {
            Assert.Equal(expected, new DoubleToScientificString().ConvertBack(text, typeof(double), null, Culture));
        }

        [Theory]
        [InlineData("2,,5")]
        [InlineData("")]
        public void A_typo_is_an_error_on_the_field_not_a_crash(string text)
        {
            object? result = new DoubleToScientificString().ConvertBack(text, typeof(double), null, Culture);

            BindingNotification error = Assert.IsType<BindingNotification>(result);
            Assert.Equal(BindingErrorType.Error, error.ErrorType);
            Assert.Contains("isn't a number", error.Error!.Message);
        }

        [Fact]
        public void An_emptied_integer_box_keeps_the_value()
        {
            Assert.Same(BindingOperations.DoNothing, new DecimalToLongConverter().ConvertBack(null, typeof(long), null, Culture));
            Assert.Same(BindingOperations.DoNothing, new DecimalToUInt128Converter().ConvertBack(null, typeof(UInt128), null, Culture));
        }

        [Fact]
        public void An_integer_reaches_the_model_and_one_too_large_is_an_error()
        {
            Assert.Equal(42L, new DecimalToLongConverter().ConvertBack(42m, typeof(long), null, Culture));
            Assert.Equal((UInt128)42, new DecimalToUInt128Converter().ConvertBack(42m, typeof(UInt128), null, Culture));

            Assert.IsType<BindingNotification>(new DecimalToLongConverter().ConvertBack(decimal.MaxValue, typeof(long), null, Culture));
            Assert.IsType<BindingNotification>(new DecimalToUInt128Converter().ConvertBack(-1m, typeof(UInt128), null, Culture));
        }
    }
}
