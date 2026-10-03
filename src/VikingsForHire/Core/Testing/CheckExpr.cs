using System;
using System.Globalization;

namespace VikingsForHire.Core.Testing
{
    /// <summary>Comparisons used by vfh_assert: numbers compare numerically, true/false as bools, anything else as text.</summary>
    public static class CheckExpr
    {
        public static readonly string[] Operators = { "==", "!=", ">=", "<=", ">", "<" };

        public static bool IsOperator(string token) => Array.IndexOf(Operators, token) >= 0;

        public static bool Compare(string actual, string op, string expected)
        {
            if (!IsOperator(op))
                throw new ArgumentException($"unknown operator '{op}' (use {string.Join(" ", Operators)})");

            if (TryNumber(actual, out double a) && TryNumber(expected, out double e))
            {
                const double eps = 1e-4;
                return op switch
                {
                    "==" => Math.Abs(a - e) < eps,
                    "!=" => Math.Abs(a - e) >= eps,
                    ">=" => a >= e - eps,
                    "<=" => a <= e + eps,
                    ">" => a > e + eps,
                    _ => a < e - eps,
                };
            }

            if (bool.TryParse(actual, out bool ab) && bool.TryParse(expected, out bool eb))
            {
                return op switch
                {
                    "==" => ab == eb,
                    "!=" => ab != eb,
                    _ => throw new ArgumentException($"operator '{op}' doesn't apply to true/false"),
                };
            }

            int cmp = string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
            return op switch
            {
                "==" => cmp == 0,
                "!=" => cmp != 0,
                ">=" => cmp >= 0,
                "<=" => cmp <= 0,
                ">" => cmp > 0,
                _ => cmp < 0,
            };
        }

        private static bool TryNumber(string s, out double value) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
