using System;
using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core.Data;

namespace VikingsForHire.Core.Chores
{
    /// <summary>Which chores a Steward of a given level may do, and its per-Steward chore toggles.</summary>
    public static class ChoreRules
    {
        /// <summary>The station prefabs that are Mills (the rest of the Steward's stations are Stations).</summary>
        public static readonly IReadOnlyCollection<string> MillStations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "windmill", "piece_spinningwheel" };

        /// <summary>Lowest Steward level for a chore key or a station prefab; a key with no entry is level 1.</summary>
        public static int MinLevel(JobData steward, string key) =>
            steward.ChoreLevels.TryGetValue(key, out int level) ? level
            : steward.ChoreLevels.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase)) is { Key: not null } kv ? kv.Value
            : 1;

        public static bool Unlocked(JobData steward, int level, string key) => level >= MinLevel(steward, key);

        public static ChoreKind KindOfStation(string prefab) => MillStations.Contains(prefab) ? ChoreKind.Mills : ChoreKind.Stations;

        /// <summary>The keys a chore kind is gated by: its own key, or for stations and mills each of its stations.</summary>
        public static IEnumerable<string> GateKeys(JobData steward, ChoreKind kind) =>
            kind is ChoreKind.Stations or ChoreKind.Mills
                ? steward.Stations.Where(s => KindOfStation(s) == kind)
                : new[] { ChoreKeys.Key(kind) };

        /// <summary>Lowest level at which any of the kind's keys unlocks (int.MaxValue for a kind with nothing to gate, e.g. no mills listed).</summary>
        public static int FirstUnlock(JobData steward, ChoreKind kind)
        {
            List<int> levels = GateKeys(steward, kind).Select(k => MinLevel(steward, k)).ToList();
            return levels.Count == 0 ? int.MaxValue : levels.Min();
        }

        /// <summary>The chores a job does, in the Shift+E panel's order (none for jobs without a chore loop).</summary>
        public static IReadOnlyList<ChoreKind> ChoresFor(JobType job) => job switch
        {
            JobType.Smelter => ChoreKeys.All.Where(k => k < ChoreKind.Harvest).ToList(),
            JobType.Farmer => new[] { ChoreKind.Harvest, ChoreKind.Plant },
            JobType.Cook => new[] { ChoreKind.Stoves, ChoreKind.Craft },
            _ => Array.Empty<ChoreKind>(),
        };

        /// <summary>The chores switched off in a contract's skip list.</summary>
        public static HashSet<ChoreKind> ChoresOff(string? skipItems)
        {
            var off = new HashSet<ChoreKind>();
            foreach (string entry in GatherRules.ParseSkip(skipItems))
                if (entry.StartsWith(ChoreKeys.SkipPrefix, StringComparison.OrdinalIgnoreCase) &&
                    ChoreKeys.TryParse(entry.Substring(ChoreKeys.SkipPrefix.Length), out ChoreKind kind))
                    off.Add(kind);
            return off;
        }

        /// <summary>The skip list with one chore switched on or off; other entries are left as they are.</summary>
        public static string WithChore(string? skipItems, ChoreKind kind, bool on)
        {
            HashSet<string> skip = GatherRules.ParseSkip(skipItems);
            string entry = ChoreKeys.SkipPrefix + ChoreKeys.Key(kind);
            skip.RemoveWhere(e => string.Equals(e, entry, StringComparison.OrdinalIgnoreCase));
            if (!on)
                skip.Add(entry);
            return GatherRules.FormatSkip(skip);
        }
    }
}
