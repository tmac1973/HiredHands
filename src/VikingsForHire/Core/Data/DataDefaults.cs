using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VikingsForHire.Core.Data
{
    /// <summary>
    /// Fills settings that a data file doesn't mention from the shipped defaults. Without this, a file written by an
    /// older version silently gets the C# initialiser for every setting added since (e.g. a radius multiplier of 1
    /// instead of 2). Only keys that are absent are filled: anything the file states, including an emptied list or
    /// cost table, is kept as written.
    /// </summary>
    public static class DataDefaults
    {
        /// <param name="target">What the file deserialised to.</param>
        /// <param name="defaults">The shipped defaults (same type).</param>
        /// <param name="raw">The same file deserialised untyped (dictionaries and lists), to see which keys exist.</param>
        /// <returns>Paths of the settings that were filled, for the log.</returns>
        public static List<string> FillMissing(object target, object defaults, object? raw)
        {
            var filled = new List<string>();
            Fill(target, defaults, raw, "", filled);
            return filled;
        }

        /// <summary>
        /// One-off changes for files older than a setting that came with list entries (FillMissing never touches lists).
        /// 0.4.0: a file without the Steward's choreLevels predates the windmill and spinning wheel, so they're added to
        /// its stations (once: from then on the file has choreLevels and its stations list is left as written).
        /// </summary>
        public static void Migrate(VfhData data, List<string> filled)
        {
            if (filled.Contains("jobs.Smelter.choreLevels") && data.Jobs.TryGetValue(JobType.Smelter, out JobData? steward))
            {
                foreach (string mill in new[] { "windmill", "piece_spinningwheel" })
                {
                    if (steward.Stations.Any(s => string.Equals(s, mill, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    steward.Stations.Add(mill);
                    filled.Add($"jobs.Smelter.stations+{mill}");
                }
            }
        }

        private static void Fill(object target, object defaults, object? raw, string path, List<string> filled)
        {
            if (raw is not IDictionary map)
                return;
            foreach (PropertyInfo p in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0)
                    continue;
                string key = CamelCase(p.Name);
                string here = path.Length == 0 ? key : path + "." + key;
                object? defaultValue = p.GetValue(defaults);
                if (!map.Contains(key))
                {
                    p.SetValue(target, defaultValue);
                    filled.Add(here);
                    continue;
                }
                object? value = p.GetValue(target);
                if (value == null || defaultValue == null)
                    continue;
                object? rawValue = map[key];
                if (IsRecord(p.PropertyType))
                {
                    Fill(value, defaultValue, rawValue, here, filled);
                }
                else if (value is IList list && defaultValue is IList defaultList && rawValue is IList rawList && IsRecord(ElementType(p.PropertyType)))
                {
                    for (int i = 0; i < list.Count && i < defaultList.Count && i < rawList.Count; i++)
                        if (list[i] != null && defaultList[i] != null)
                            Fill(list[i]!, defaultList[i]!, rawList[i], $"{here}[{i}]", filled);
                }
                else if (value is IDictionary dict && defaultValue is IDictionary defaultDict && rawValue is IDictionary rawDict &&
                         IsRecord(p.PropertyType.GetGenericArguments().LastOrDefault()))
                {
                    // Keyed records (jobs): fill inside each one the file lists, and add whole ones it doesn't.
                    foreach (object k in defaultDict.Keys)
                    {
                        string name = k.ToString()!;
                        if (!dict.Contains(k))
                        {
                            dict[k] = defaultDict[k];
                            filled.Add($"{here}.{name}");
                        }
                        else if (dict[k] != null && rawDict.Contains(name))
                        {
                            Fill(dict[k]!, defaultDict[k]!, rawDict[name], $"{here}.{name}", filled);
                        }
                    }
                }
            }
        }

        // A class with its own settings (not a string, number, list or dictionary).
        private static bool IsRecord(Type? t) =>
            t != null && t.IsClass && t != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(t);

        private static Type? ElementType(Type t) =>
            t.IsArray ? t.GetElementType() : t.IsGenericType ? t.GetGenericArguments()[0] : null;

        private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
