using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Chores
{
    /// <summary>What a Steward can do. The order is the tie-break order (after distance) and the panel's row order.</summary>
    public enum ChoreKind
    {
        Fires,
        Beehives,
        Stations,
        Mills,
        Sap,
        Animals,
        Repairs,
        Fermenters,
        Shields,
        Tidy,
        Board,
        Harvest,
        Plant,
        Stoves,
        Craft,
    }

    /// <summary>The stable keys chores go by in data files (choreLevels) and in a contract's skip list ("chore:fires").</summary>
    public static class ChoreKeys
    {
        public const string SkipPrefix = "chore:";

        private static readonly Dictionary<ChoreKind, string> Keys = new()
        {
            [ChoreKind.Fires] = "fires",
            [ChoreKind.Beehives] = "beehives",
            [ChoreKind.Stations] = "stations",
            [ChoreKind.Mills] = "mills",
            [ChoreKind.Sap] = "sap",
            [ChoreKind.Animals] = "animals",
            [ChoreKind.Repairs] = "repairs",
            [ChoreKind.Fermenters] = "fermenters",
            [ChoreKind.Shields] = "shields",
            [ChoreKind.Tidy] = "tidy",
            [ChoreKind.Board] = "board",
            [ChoreKind.Harvest] = "harvest",
            [ChoreKind.Plant] = "plant",
            [ChoreKind.Stoves] = "stoves",
            [ChoreKind.Craft] = "craft",
        };

        public static IReadOnlyList<ChoreKind> All { get; } = Enum.GetValues(typeof(ChoreKind)).Cast<ChoreKind>().ToList();

        public static string Key(ChoreKind kind) => Keys[kind];

        public static bool TryParse(string key, out ChoreKind kind)
        {
            foreach (KeyValuePair<ChoreKind, string> k in Keys)
            {
                if (string.Equals(k.Value, key, StringComparison.OrdinalIgnoreCase))
                {
                    kind = k.Key;
                    return true;
                }
            }
            kind = default;
            return false;
        }
    }
}
