using System;
using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core.Data;

namespace VikingsForHire.Core
{
    /// <summary>
    /// Which items a hireling wears, given the data and whether each item exists in this game (a mod's items vanish when
    /// the mod is removed): nobody is ever left without a weapon or with an empty slot that has a shared item for it.
    /// </summary>
    public static class GearRules
    {
        public const string LastStaff = "StaffFireball";
        public const string LastWeapon = "Club";

        /// <summary>
        /// A wizard's staff: the level's staff in the effective data (a loaded mod's), else the job's own (no-mod) staff
        /// for the level, else the Staff of Embers, else a club. <paramref name="fellBack"/> says a fallback was used.
        /// </summary>
        public static string Staff(int level, IList<WeaponSetData> effective, IList<WeaponSetData> withoutMods, Func<string, bool> exists, out bool fellBack)
        {
            fellBack = false;
            string? first = effective.FirstOrDefault(g => g.Level == level)?.Main;
            if (!string.IsNullOrEmpty(first) && exists(first!))
                return first!;
            fellBack = true;
            string? own = withoutMods.FirstOrDefault(g => g.Level == level)?.Main;
            if (!string.IsNullOrEmpty(own) && exists(own!))
                return own!;
            return exists(LastStaff) ? LastStaff : LastWeapon;
        }

        /// <summary>
        /// Helmet, chest, legs and cape for a level: the job's outfit piece where it's set and exists, else the shared
        /// set's piece for that slot (the shared sets have no cape). A job's <paramref name="hat"/>, where it exists, takes
        /// the helmet's place.
        /// </summary>
        public static string[] Armor(int level, IList<ArmorSetData> outfit, IList<ArmorSetData> shared, Func<string, bool> exists, string? hat = null)
        {
            ArmorSetData? own = outfit.FirstOrDefault(a => a.Level == level);
            ArmorSetData? common = shared.FirstOrDefault(a => a.Level == level);
            string Pick(string? mine, string? theirs) =>
                !string.IsNullOrEmpty(mine) && exists(mine!) ? mine! : theirs ?? "";
            return new[]
            {
                Pick(hat, Pick(own?.Helmet, common?.Helmet)),
                Pick(own?.Chest, common?.Chest),
                Pick(own?.Legs, common?.Legs),
                Pick(own?.Cape, ""),
            };
        }
    }
}
