using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GAS.Editor
{
    internal static class GASCenterParseHelper
    {
        // Tolerant parser for Excel cells that may contain formats like:
        // "1;2;3", "1,2,3", "all=1,2;any=0;none=0", or JSON-like text.
        private static readonly Regex IntRegex = new(@"-?\d+", RegexOptions.Compiled);

        public static List<int> ParseIntListLoose(string raw, bool positiveOnly = true)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var matches = IntRegex.Matches(raw);
            foreach (Match match in matches)
            {
                if (!int.TryParse(match.Value, out var value))
                {
                    continue;
                }

                if (positiveOnly && value <= 0)
                {
                    continue;
                }

                result.Add(value);
            }

            return result;
        }

        public static bool TryNormalizeIntList(string raw, out string normalized, out string error, bool positiveOnly = true)
        {
            normalized = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            var parts = Regex.Split(raw.Trim(), @"[;,\s]+");
            var result = new List<int>();
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                if (!int.TryParse(part, out var value))
                {
                    error = $"无法解析 ID: {part}";
                    return false;
                }

                if (positiveOnly && value <= 0)
                {
                    error = $"ID 必须是正整数: {value}";
                    return false;
                }

                result.Add(value);
            }

            normalized = string.Join(";", result);
            return true;
        }
    }
}
