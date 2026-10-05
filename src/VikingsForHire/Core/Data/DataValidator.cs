using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Data
{
    public static class DataValidator
    {
        public const int Levels = 8;
        public const int StoneQualities = 4;
        public const int MaxCargoSlots = 32;

        /// <summary>Structural checks. Any error means the file is rejected and defaults are used.</summary>
        public static List<string> Validate(VfhData data)
        {
            var errors = new List<string>();

            CheckSequence(errors, "boardLevels", data.BoardLevels.Select(b => b.Level).ToList(), Levels);
            foreach (BoardLevelData b in data.BoardLevels)
            {
                if (b.HirelingCap < 0) errors.Add($"boardLevels[{b.Level}].hirelingCap is negative");
                if (b.MaxWorkRadius <= 0) errors.Add($"boardLevels[{b.Level}].maxWorkRadius must be > 0");
                CheckCost(errors, $"boardLevels[{b.Level}].cost", b.Cost);
            }

            CheckSequence(errors, "hirelingLevels", data.HirelingLevels.Select(h => h.Level).ToList(), Levels);
            foreach (HirelingLevelData h in data.HirelingLevels)
            {
                string p = $"hirelingLevels[{h.Level}]";
                if (h.Health <= 0) errors.Add($"{p}.health must be > 0");
                if (h.CargoSlots < 1 || h.CargoSlots > MaxCargoSlots) errors.Add($"{p}.cargoSlots must be 1-{MaxCargoSlots}");
                if (h.CargoWeight < 0) errors.Add($"{p}.cargoWeight must be 0 (no limit) or more");
                if (h.ArmorBonus < 0 || h.GuardDamageMult < 0 || h.GatherMult < 0) errors.Add($"{p} has a negative stat");
                if (h.HireFood < 0 || h.HireCoins < 0 || h.UpkeepFood < 0 || h.UpkeepCoins < 0) errors.Add($"{p} has a negative price");
            }

            CheckSequence(errors, "armorSets", data.ArmorSets.Select(a => a.Level).ToList(), Levels);

            foreach (JobType job in Enum.GetValues(typeof(JobType)))
            {
                if (!data.Jobs.TryGetValue(job, out JobData? j))
                {
                    errors.Add($"jobs.{job} is missing");
                    continue;
                }
                if (j.CostMult < 0 || j.WorkerCombatFactor < 0) errors.Add($"jobs.{job} has a negative multiplier");
                if (j.MinBoardLevel < 1 || j.MinBoardLevel > Levels) errors.Add($"jobs.{job}.minBoardLevel must be 1-{Levels}");
                if (j.WorkRadiusMultiplier <= 0) errors.Add($"jobs.{job}.workRadiusMultiplier must be > 0");
                CheckSequence(errors, $"jobs.{job}.gear", j.Gear.Select(g => g.Level).ToList(), Levels);
                if (job == JobType.Smelter && j.Stations.Count == 0) errors.Add("jobs.Smelter.stations is empty");
                foreach (var keep in j.KeepInStorage)
                    if (keep.Value < 0) errors.Add($"jobs.{job}.keepInStorage.{keep.Key} is negative");
            }

            CheckSequence(errors, "commandStone", data.CommandStone.Select(s => s.Quality).ToList(), StoneQualities);
            foreach (StoneLevelData s in data.CommandStone)
            {
                if (s.RequiredBoardLevel < 1 || s.RequiredBoardLevel > Levels) errors.Add($"commandStone[{s.Quality}].requiredBoardLevel must be 1-{Levels}");
                if (s.FollowerCap < 0) errors.Add($"commandStone[{s.Quality}].followerCap is negative");
                CheckCost(errors, $"commandStone[{s.Quality}].cost", s.Cost);
            }

            if (data.Names.Male.Count == 0 || data.Names.Female.Count == 0) errors.Add("names.male and names.female need at least one name each");

            foreach (KeyValuePair<JobType, JobData> job in data.Jobs)
            {
                foreach (KeyValuePair<string, int> c in job.Value.ChoreLevels)
                {
                    string p = $"jobs.{job.Key}.choreLevels.{c.Key}";
                    if (c.Value < 1 || c.Value > Levels) errors.Add($"{p} must be 1-{Levels}");
                    bool chore = Chores.ChoreKeys.TryParse(c.Key, out _);
                    bool station = job.Value.Stations.Any(s => string.Equals(s, c.Key, StringComparison.OrdinalIgnoreCase));
                    if (!chore && !station) errors.Add($"{p}: not a chore (fires, beehives, stations, mills, sap, animals, repairs) or a station in stations");
                }
            }

            if (data.NavLinks.Include.Any(string.IsNullOrWhiteSpace) || data.NavLinks.Exclude.Any(string.IsNullOrWhiteSpace))
                errors.Add("navLinks.include and navLinks.exclude can't have empty entries");
            foreach (string both in data.NavLinks.Include.Intersect(data.NavLinks.Exclude, System.StringComparer.OrdinalIgnoreCase))
                errors.Add($"navLinks: {both} is in both include and exclude");
            return errors;
        }

        /// <summary>
        /// Fixes item names the running game doesn't know (renamed or removed by an update) instead of rejecting the file:
        /// unknown cost items are dropped, unknown gear falls back to the same slot one level down. Returns a warning per fix.
        /// </summary>
        public static List<string> Sanitize(VfhData data, Func<string, bool> itemExists)
        {
            var warnings = new List<string>();

            // A station with no chore level works from level 1: allowed, but worth a line in the log.
            foreach (KeyValuePair<JobType, JobData> job in data.Jobs)
                foreach (string station in job.Value.Stations)
                    if (job.Value.ChoreLevels.Count > 0 && !job.Value.ChoreLevels.Keys.Any(k => string.Equals(k, station, StringComparison.OrdinalIgnoreCase)))
                        warnings.Add($"jobs.{job.Key}.stations: {station} has no choreLevels entry, so any level of Steward tends it");

            foreach (BoardLevelData b in data.BoardLevels)
                DropUnknown(warnings, $"boardLevels[{b.Level}].cost", b.Cost, itemExists);
            foreach (StoneLevelData s in data.CommandStone)
                DropUnknown(warnings, $"commandStone[{s.Quality}].cost", s.Cost, itemExists);

            ArmorSetData? prevArmor = null;
            foreach (ArmorSetData a in data.ArmorSets.OrderBy(a => a.Level))
            {
                a.Helmet = Fallback(warnings, $"armorSets[{a.Level}].helmet", a.Helmet, prevArmor?.Helmet, itemExists);
                a.Chest = Fallback(warnings, $"armorSets[{a.Level}].chest", a.Chest, prevArmor?.Chest, itemExists);
                a.Legs = Fallback(warnings, $"armorSets[{a.Level}].legs", a.Legs, prevArmor?.Legs, itemExists);
                prevArmor = a;
            }

            foreach (KeyValuePair<JobType, JobData> job in data.Jobs)
            {
                WeaponSetData? prev = null;
                foreach (WeaponSetData g in job.Value.Gear.OrderBy(g => g.Level))
                {
                    string p = $"jobs.{job.Key}.gear[{g.Level}]";
                    g.Main = Fallback(warnings, p + ".main", g.Main, prev?.Main, itemExists);
                    g.Offhand = Fallback(warnings, p + ".offhand", g.Offhand, prev?.Offhand, itemExists);
                    g.Ammo = Fallback(warnings, p + ".ammo", g.Ammo, prev?.Ammo, itemExists);
                    g.Sidearm = Fallback(warnings, p + ".sidearm", g.Sidearm, prev?.Sidearm, itemExists);
                    prev = g;
                }

                int before = job.Value.PickupItems.Count;
                var unknown = job.Value.PickupItems.Where(i => !itemExists(i)).ToList();
                job.Value.PickupItems.RemoveAll(i => !itemExists(i));
                if (unknown.Count > 0)
                    warnings.Add($"jobs.{job.Key}.pickupItems: dropped unknown {string.Join(",", unknown)} ({before}->{job.Value.PickupItems.Count})");
            }

            return warnings;
        }

        private static void CheckSequence(List<string> errors, string name, List<int> levels, int expected)
        {
            if (levels.Count != expected || !levels.OrderBy(l => l).SequenceEqual(Enumerable.Range(1, expected)))
                errors.Add($"{name} must have exactly entries 1-{expected} (found: {string.Join(",", levels)})");
        }

        private static void CheckCost(List<string> errors, string name, Dictionary<string, int> cost)
        {
            foreach (KeyValuePair<string, int> c in cost)
                if (c.Value < 0)
                    errors.Add($"{name}.{c.Key} is negative");
        }

        private static void DropUnknown(List<string> warnings, string name, Dictionary<string, int> cost, Func<string, bool> itemExists)
        {
            foreach (string item in cost.Keys.Where(k => !itemExists(k)).ToList())
            {
                cost.Remove(item);
                warnings.Add($"{name}: dropped unknown item {item}");
            }
        }

        private static string Fallback(List<string> warnings, string name, string value, string? previous, Func<string, bool> itemExists)
        {
            if (value.Length == 0 || itemExists(value))
                return value;
            string replacement = previous ?? "";
            warnings.Add($"{name}: unknown item {value}, using '{replacement}'");
            return replacement;
        }
    }
}
