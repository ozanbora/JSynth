using System;
using System.Globalization;
using System.Linq;

namespace JSynth.Model
{
    /// <summary>
    /// The step a number's slider moves in, in the parameters panel; set in the panel or in the number's editor.
    /// </summary>
    public static class SliderStep
    {
        /// <summary>
        /// A hundredth of the range, rounded down to 1, 2 or 5 times a power of ten: 5000 for 0 to 500000, 0.001 for 0 to
        /// 0.1. At least 1 for whole numbers.
        /// </summary>
        public static double Default(double from, double to, bool whole)
        {
            double size = (to - from) / 100;
            double step = size > 0 && double.IsFinite(size) ? RoundDown(size) : 1;
            return whole ? Math.Max(1, step) : step;
        }

        /// <summary>
        /// A step as typed: a number above 0, a whole one of at least 1 for whole numbers. Empty: null, the default. Not
        /// a step: false.
        /// </summary>
        public static bool TryParse(string? text, bool whole, out double? step)
        {
            step = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed) || typed <= 0 || !double.IsFinite(typed))
            {
                return false;
            }

            step = whole ? Math.Max(1, Math.Round(typed)) : typed;
            return true;
        }

        /// <summary>
        /// The largest of 1, 2 or 5 times a power of ten that isn't above <paramref name="size"/>.
        /// </summary>
        private static double RoundDown(double size)
        {
            double power = Math.Pow(10, Math.Floor(Math.Log10(size)));
            double round = new[] { 5, 2, 1 }.Select(m => m * power).First(r => r <= size * (1 + 1e-12));

            // Exactly: 0.001, not 0.0010000000000000002.
            return Math.Round(round, Math.Max(0, -(int)Math.Floor(Math.Log10(round))));
        }
    }
}
