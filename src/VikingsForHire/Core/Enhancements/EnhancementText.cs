using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VikingsForHire.Core.Data;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>
    /// The text for an enhancement: its name and description keys ($vfh_enh_<job>_<name>[_desc]) and the description's
    /// numbers from the data file, in order ($1, $2…), percentages already formatted. Generated alongside English.json
    /// from one table so the two can't drift apart.
    /// </summary>
    public static class EnhancementText
    {
        // Each name's numbers in the order its description uses them, and whether each shows as a percentage.
        private static readonly Dictionary<string, (string Name, bool Percent)[]> Params = new()
        {
            ["Archmage"] = new (string, bool)[] { ("damage", true), ("interval", true) },
            ["Banquet"] = new (string, bool)[] { ("share", true) },
            ["BigHerd"] = new (string, bool)[] { ("extra", false) },
            ["BiggerFields"] = new (string, bool)[] { ("radius", true) },
            ["Blink"] = new (string, bool)[] { ("trigger", false), ("distance", false), ("cooldown", false) },
            ["Bloodthirst"] = new (string, bool)[] { ("share", true) },
            ["Bodyguard"] = new (string, bool)[] { ("cooldown", false), ("range", false) },
            ["Bonesetter"] = new (string, bool)[] { ("range", false), ("rate", true) },
            ["Bountiful"] = new (string, bool)[] { ("chance", true) },
            ["Breeder"] = new (string, bool)[] { ("share", true) },
            ["Brisk"] = new (string, bool)[] { ("speed", true) },
            ["Butcher"] = new (string, bool)[] { ("chance", true) },
            ["ChainSpell"] = new (string, bool)[] { ("range", false), ("share", true) },
            ["Champion"] = new (string, bool)[] { ("damage", true), ("health", true) },
            ["ChefsTouch"] = new (string, bool)[] { ("share", true) },
            ["CleanCut"] = new (string, bool)[] { ("gather", true) },
            ["Cleave"] = new (string, bool)[] { ("extra", false), ("share", true) },
            ["DeepDigger"] = new (string, bool)[] { ("depth", false) },
            ["DoubleBatch"] = new (string, bool)[] { ("chance", true) },
            ["DoubleHarvest"] = new (string, bool)[] { ("chance", true) },
            ["EagleEye"] = new (string, bool)[] { ("range", false) },
            ["ElementalMastery"] = new (string, bool)[] { ("mult", true) },
            ["Elite"] = new (string, bool)[] { ("damage", true), ("health", true) },
            ["EntanglingRoots"] = new (string, bool)[] { ("cooldown", false) },
            ["ErrandRunner"] = new (string, bool)[] { ("chain", false) },
            ["FermentersTouch"] = new (string, bool)[] { ("share", true) },
            ["FineEye"] = new (string, bool)[] { ("share", true) },
            ["FireArrows"] = new (string, bool)[] { ("damage", false), ("seconds", false) },
            ["Focus"] = new (string, bool)[] { ("damage", true) },
            ["Forager"] = new (string, bool)[] {  },
            ["Forester"] = new (string, bool)[] {  },
            ["FrostArrows"] = new (string, bool)[] { ("slow", true), ("seconds", false), ("damage", false) },
            ["Frugal"] = new (string, bool)[] { ("chance", true) },
            ["GemEye"] = new (string, bool)[] { ("chance", true) },
            ["GentleHand"] = new (string, bool)[] { ("range", false) },
            ["GreenThumb"] = new (string, bool)[] { ("speed", true) },
            ["Handyman"] = new (string, bool)[] { ("range", false), ("quiet", false) },
            ["HardyStock"] = new (string, bool)[] {  },
            ["Haste"] = new (string, bool)[] { ("interval", true) },
            ["HeadChef"] = new (string, bool)[] { ("speed", true), ("saveChance", true), ("extraChance", true) },
            ["Headshot"] = new (string, bool)[] { ("chance", true) },
            ["HeartyFeed"] = new (string, bool)[] { ("share", true) },
            ["HeavyArmour"] = new (string, bool)[] { ("armor", false) },
            ["HeavyHauler"] = new (string, bool)[] { ("oreWeight", true) },
            ["HerdGuard"] = new (string, bool)[] { ("damage", true) },
            ["Herder"] = new (string, bool)[] {  },
            ["Hunter"] = new (string, bool)[] { ("damage", true) },
            ["KeeperOfFlames"] = new (string, bool)[] { ("refill", true) },
            ["KeeperOfStoves"] = new (string, bool)[] {  },
            ["LastStand"] = new (string, bool)[] { ("cooldown", false), ("seconds", false) },
            ["LightFeet"] = new (string, bool)[] { ("speed", true) },
            ["LongReach"] = new (string, bool)[] { ("range", false) },
            ["Lumberjack"] = new (string, bool)[] { ("share", true) },
            ["Marksman"] = new (string, bool)[] { ("damage", true), ("range", false) },
            ["MasterFarmer"] = new (string, bool)[] { ("speed", true), ("share", true) },
            ["MasterMiner"] = new (string, bool)[] { ("gather", true), ("share", true) },
            ["MasterRancher"] = new (string, bool)[] { ("speed", true), ("chance", true) },
            ["MasterSteward"] = new (string, bool)[] { ("speed", true) },
            ["MasterWoodsman"] = new (string, bool)[] { ("gather", true), ("share", true) },
            ["Masterwork"] = new (string, bool)[] { ("quality", false) },
            ["Mender"] = new (string, bool)[] { ("cooldown", false), ("range", false), ("heal", true) },
            ["PackMule"] = new (string, bool)[] { ("slots", false) },
            ["PantrySense"] = new (string, bool)[] { ("rangeMult", false) },
            ["PrepChef"] = new (string, bool)[] {  },
            ["Prospector"] = new (string, bool)[] { ("radius", true) },
            ["Protection"] = new (string, bool)[] { ("cooldown", false) },
            ["Quartermaster"] = new (string, bool)[] { ("days", false) },
            ["QuickDraw"] = new (string, bool)[] { ("interval", true) },
            ["QuickHands"] = new (string, bool)[] { ("speed", true) },
            ["QuietWork"] = new (string, bool)[] {  },
            ["RainOfArrows"] = new (string, bool)[] { ("cooldown", false), ("arrows", false), ("share", true) },
            ["RaiseDead"] = new (string, bool)[] { ("cooldown", false), ("seconds", false) },
            ["RallyingCry"] = new (string, bool)[] { ("range", false), ("damage", true) },
            ["Replanter"] = new (string, bool)[] {  },
            ["RichVeins"] = new (string, bool)[] { ("share", true) },
            ["RockBreaker"] = new (string, bool)[] { ("gather", true) },
            ["SecondWind"] = new (string, bool)[] { ("below", true), ("heal", true) },
            ["SecondarySpell"] = new (string, bool)[] { ("cooldown", false) },
            ["SeedKeeper"] = new (string, bool)[] { ("chance", true) },
            ["SeedSaver"] = new (string, bool)[] {  },
            ["ShieldBash"] = new (string, bool)[] { ("cooldown", false), ("damage", false) },
            ["ShieldKeeper"] = new (string, bool)[] { ("rangeMult", false) },
            ["ShieldTraining"] = new (string, bool)[] { ("block", true) },
            ["Skinner"] = new (string, bool)[] { ("chance", true) },
            ["Skirmisher"] = new (string, bool)[] { ("range", false), ("seconds", false) },
            ["SmeltersHand"] = new (string, bool)[] { ("mult", false) },
            ["ClosePlanting"] = new (string, bool)[] { ("space", true) },
            ["SpitMaster"] = new (string, bool)[] {  },
            ["StarBreeder"] = new (string, bool)[] { ("chance", true) },
            ["SteadyAim"] = new (string, bool)[] { ("damage", true) },
            ["StrongBack"] = new (string, bool)[] { ("weight", false) },
            ["StrongSwing"] = new (string, bool)[] { ("gather", true) },
            ["Swift"] = new (string, bool)[] { ("speed", true) },
            ["SwiftStrikes"] = new (string, bool)[] { ("interval", true) },
            ["Taunt"] = new (string, bool)[] { ("cooldown", false), ("range", false), ("seconds", false) },
            ["TenderCare"] = new (string, bool)[] { ("share", true) },
            ["TidyMind"] = new (string, bool)[] { ("seconds", false), ("rangeMult", false) },
            ["Timber"] = new (string, bool)[] {  },
            ["Tireless"] = new (string, bool)[] { ("share", true) },
            ["Toughened"] = new (string, bool)[] { ("health", true) },
            ["TwoStations"] = new (string, bool)[] {  },
            ["Volley"] = new (string, bool)[] { ("cooldown", false), ("arrows", false) },
            ["Warding"] = new (string, bool)[] { ("share", true) },
            ["Watchman"] = new (string, bool)[] { ("damage", true) },
            ["WideRange"] = new (string, bool)[] { ("radius", true) },
            ["Wrangler"] = new (string, bool)[] { ("cooldown", false) },
        };

        public static string NameKey(JobType job, EnhancementId id) => $"$vfh_enh_{job.ToString().ToLowerInvariant()}_{id.ToString().ToLowerInvariant()}";

        public static string DescKey(JobType job, EnhancementId id) => NameKey(job, id) + "_desc";

        /// <summary>The description's numbers, formatted (10%, 2.5).</summary>
        public static string[] Args(VfhData? data, JobType job, EnhancementId id)
        {
            if (!Params.TryGetValue(id.ToString(), out (string Name, bool Percent)[]? ps))
                return Array.Empty<string>();
            return ps.Select(p => Format(EnhancementNumbers.Get(data, job, id, p.Name), p.Percent)).ToArray();
        }

        /// <summary>The number names a description uses (tests: every one must have a default).</summary>
        public static IEnumerable<string> NumberNames(EnhancementId id) =>
            Params.TryGetValue(id.ToString(), out (string Name, bool Percent)[]? ps) ? ps.Select(p => p.Name) : Enumerable.Empty<string>();

        public static string Format(float value, bool percent) => percent
            ? Math.Round(value * 100f).ToString(CultureInfo.InvariantCulture) + "%"
            : Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
