using System.Globalization;

namespace Game.UI
{
    /// <summary>Short number formatting for prices and currencies (1.2K, 3.4M, ...).</summary>
    public static class NumberFormat
    {
        private const double Thousand = 1000d;
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi" };

        /// <summary>Formats <paramref name="value"/> with at most three significant digits and a suffix.</summary>
        public static string Short(long value)
        {
            double v = value;
            var index = 0;
            while (System.Math.Abs(v) >= Thousand && index < Suffixes.Length - 1)
            {
                v /= Thousand;
                index++;
            }
            var format = index == 0 || System.Math.Abs(v) >= 100 ? "0" : System.Math.Abs(v) >= 10 ? "0.#" : "0.##";
            return v.ToString(format, CultureInfo.InvariantCulture) + Suffixes[index];
        }
    }
}
