using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace VikingsForHire.Core.Diagnostics
{
    public enum LogLevel
    {
        Error,
        Warning,
        Info,
        Debug,
        Trace,
    }

    public enum LogCat
    {
        Core, Data, Board, Placement, Roster, Payment, Hireling, AI, Combat, Work, Deliver, Smelter,
        Follow, Orders, Travel, Orphan, Net, Compat, UI, Perf, Test, Nav,
    }

    public static class LogFormat
    {
        /// <summary>Field keys whose values are long ids, shortened in log lines (full id logged once via id.map).</summary>
        public static readonly HashSet<string> IdKeys = new(StringComparer.Ordinal) { "hid", "board", "contract" };

        private static readonly Dictionary<LogLevel, char> LevelChar = new()
        {
            [LogLevel.Error] = 'E', [LogLevel.Warning] = 'W', [LogLevel.Info] = 'I', [LogLevel.Debug] = 'D', [LogLevel.Trace] = 'T',
        };

        /// <summary>
        /// <c>[VFH] t=1834.2 f=99120 role=SP lvl=D cat=Work evt=deliver.plan hid=3f2a n=40</c>. One line, greppable.
        /// </summary>
        public static string Line(double time, long frame, string role, LogLevel level, LogCat cat, string evt,
            IEnumerable<(string Key, object? Value)> fields)
        {
            var sb = new StringBuilder(128);
            sb.Append("[VFH] t=").Append(time.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" f=").Append(frame)
                .Append(" role=").Append(role)
                .Append(" lvl=").Append(LevelChar[level])
                .Append(" cat=").Append(cat)
                .Append(" evt=").Append(Value(evt));
            foreach ((string key, object? value) in fields)
            {
                sb.Append(' ').Append(key).Append('=');
                sb.Append(IdKeys.Contains(key) && value is string id ? ShortId(id) : Value(value));
            }
            return sb.ToString();
        }

        /// <summary>Formats a value: invariant numbers, lowercase bools, quotes around anything with spaces or quotes.</summary>
        public static string Value(object? value)
        {
            string s = value switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                float f => f.ToString("0.###", CultureInfo.InvariantCulture),
                double d => d.ToString("0.###", CultureInfo.InvariantCulture),
                IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "",
            };
            if (s.Length == 0)
                return "\"\"";
            if (s.Any(c => char.IsWhiteSpace(c) || c == '"' || c == '='))
                return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
            return s;
        }

        /// <summary>First 4 hex characters of an id (dashes ignored). Short ids stay as they are.</summary>
        public static string ShortId(string id)
        {
            string compact = id.Replace("-", "");
            return compact.Length <= 4 ? compact : compact.Substring(0, 4).ToLowerInvariant();
        }
    }

    /// <summary>Which categories log at Debug and Trace. Error, Warning and Info always log.</summary>
    public sealed class LogFilter
    {
        private readonly HashSet<LogCat> _debug;
        private readonly HashSet<LogCat> _trace;

        public LogFilter(IEnumerable<LogCat> debug, IEnumerable<LogCat> trace)
        {
            _trace = new HashSet<LogCat>(trace);
            _debug = new HashSet<LogCat>(debug);
            _debug.UnionWith(_trace);
        }

        public bool Enabled(LogLevel level, LogCat cat) => level switch
        {
            LogLevel.Debug => _debug.Contains(cat),
            LogLevel.Trace => _trace.Contains(cat),
            _ => true,
        };

        public IReadOnlyCollection<LogCat> DebugCats => _debug;
        public IReadOnlyCollection<LogCat> TraceCats => _trace;

        /// <summary>Parses "Work, AI" or "All" (case-insensitive). Unknown names are returned in <paramref name="unknown"/>.</summary>
        public static HashSet<LogCat> ParseCategories(string text, out List<string> unknown)
        {
            unknown = new List<string>();
            var result = new HashSet<LogCat>();
            foreach (string raw in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (raw.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    result.UnionWith((LogCat[])Enum.GetValues(typeof(LogCat)));
                    continue;
                }
                if (Enum.TryParse(raw, true, out LogCat cat) && Enum.IsDefined(typeof(LogCat), cat))
                    result.Add(cat);
                else
                    unknown.Add(raw);
            }
            return result;
        }

        public static string FormatCategories(IEnumerable<LogCat> cats)
        {
            var list = cats.Distinct().OrderBy(c => c).ToList();
            return list.Count == Enum.GetValues(typeof(LogCat)).Length ? "All" : string.Join(",", list);
        }
    }
}
