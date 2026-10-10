using Avalonia.Media;
using System;

namespace JSynth.Converters
{
    /// <summary>
    /// Contrast between colors (WCAG definitions), and making a color stand out from a background.
    /// </summary>
    public static class ColorContrast
    {
        /// <summary>
        /// WCAG's minimum contrast for lines and other graphics that must be seen.
        /// </summary>
        public const double MinimumContrast = 3.0;

        /// <summary>
        /// <paramref name="color"/> itself when it has enough contrast with <paramref name="canvas"/>;
        /// otherwise mixed towards white (dark canvas) or black (light canvas) until it has.
        /// </summary>
        public static Color StandOut(Color color, Color canvas)
        {
            Color towards = Luminance(canvas) < 0.5 ? Colors.White : Colors.Black;
            Color adjusted = color;
            for (int step = 1; step <= 20 && Contrast(adjusted, canvas) < MinimumContrast; step++)
            {
                adjusted = Mix(color, towards, step / 20.0);
            }

            return adjusted;
        }

        public static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>
        /// WCAG relative luminance, 0 (black) to 1 (white).
        /// </summary>
        public static double Luminance(Color color)
        {
            static double Linear(byte c)
            {
                double s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
        }

        private static Color Mix(Color from, Color to, double amount)
        {
            static byte Channel(byte a, byte b, double t) => (byte)Math.Round(a + ((b - a) * t));

            return Color.FromRgb(Channel(from.R, to.R, amount), Channel(from.G, to.G, amount), Channel(from.B, to.B, amount));
        }
    }
}
