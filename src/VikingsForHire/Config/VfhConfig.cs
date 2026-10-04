using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Config
{
    /// <summary>
    /// Spronglehump.HiredHands.cfg. Gameplay keys are admin-only: Jotunn syncs them from the server and locks them for
    /// non-admins. Debug and control keys are local to each machine. Read overridable values through <see cref="Get"/>
    /// so the test harness's fast_timers mode applies.
    /// </summary>
    internal static class VfhConfig
    {
        private static ConfigFile _file = null!;

        // 1 - General
        public static ConfigEntry<bool> FriendlyFireOnHirelings = null!;
        public static ConfigEntry<bool> PermadeathEnabled = null!;
        public static ConfigEntry<float> RespawnCooldownSeconds = null!;
        public static ConfigEntry<float> RespawnCostFraction = null!;
        public static ConfigEntry<bool> AllowRawFood = null!;

        // 2 - Base
        public static ConfigEntry<float> BaseCheckRadius = null!;
        public static ConfigEntry<int> RequiredWorkbenches = null!;
        public static ConfigEntry<int> RequiredBeds = null!;
        public static ConfigEntry<int> RequiredPieces = null!;
        public static ConfigEntry<float> MinDistanceBetweenBoards = null!;
        public static ConfigEntry<int> MaxBoardsPerWorld = null!;

        // 3 - Hiring
        public static ConfigEntry<float> ArrivalDelayMinSeconds = null!;
        public static ConfigEntry<float> ArrivalDelayMaxSeconds = null!;
        public static ConfigEntry<float> ArrivalSpawnDistance = null!;
        public static ConfigEntry<int> UnpaidDaysBeforeLeaving = null!;
        public static ConfigEntry<float> DropPileOffset = null!;

        // 4 - Work
        public static ConfigEntry<float> AiScanIntervalSeconds = null!;
        public static ConfigEntry<float> ThreatScanIntervalSeconds = null!;
        public static ConfigEntry<float> TreeSafetyDistanceFromPieces = null!;
        public static ConfigEntry<bool> HirelingsOpenDoors = null!;
        public static ConfigEntry<float> TreeFallCorridorHalfWidth = null!;
        public static ConfigEntry<bool> MinerProtectsTerrain = null!;
        public static ConfigEntry<bool> GatheringMakesNoise = null!;
        public static ConfigEntry<float> MinerFieldDigClearance = null!;
        public static ConfigEntry<float> MinerSafetyDistanceFromPieces = null!;
        public static ConfigEntry<float> SmelterRefillThreshold = null!;
        public static ConfigEntry<int> KeepMinimumInChest = null!;
        public static ConfigEntry<bool> KeepLastItemInChest = null!;

        // 5 - Followers
        public static ConfigEntry<float> PortalFollowRadius = null!;
        public static ConfigEntry<bool> AllowNonTeleportableThroughPortals = null!;
        public static ConfigEntry<float> GatherNearbyRadius = null!;
        public static ConfigEntry<float> ShipStowRadius = null!;
        public static ConfigEntry<float> OrphanDistance = null!;
        public static ConfigEntry<float> OrphanDistanceSeconds = null!;
        public static ConfigEntry<float> OrphanStuckSeconds = null!;
        public static ConfigEntry<float> OrphanStayDistance = null!;
        public static ConfigEntry<float> OrphanStaySeconds = null!;
        public static ConfigEntry<float> OrphanOfflineSeconds = null!;
        public static ConfigEntry<bool> ReturnHomeWithNonTeleportable = null!;
        public static ConfigEntry<bool> BalanceLog = null!;
        public static ConfigEntry<int> BalanceLogMaxMB = null!;
        public static ConfigEntry<float> ReturnSecondsPer100m = null!;
        public static ConfigEntry<float> ReturnMinSeconds = null!;
        public static ConfigEntry<float> ReturnMaxSeconds = null!;
        public static ConfigEntry<float> StoneBoardSearchRadius = null!;

        // 8 - Combat
        public static ConfigEntry<float> MeleeAttackCooldown = null!;
        public static ConfigEntry<float> RangedAttackCooldown = null!;

        // 6 - Controls (local). The board panel is Shift+E: Valheim's alt-interact, so it follows the game's own bindings.
        public static ConfigEntry<float> PostLeashRadius = null!;
        public static ConfigEntry<float> FollowerCatchUpSpeedBonus = null!;
        public static ConfigEntry<float> FollowerCatchUpTeleportDistance = null!;
        public static ConfigEntry<float> FollowerStuckTeleportSeconds = null!;

        // 7 - Debug (local)
        public static ConfigEntry<bool> LogToFile = null!;
        public static ConfigEntry<string> DebugCategories = null!;
        public static ConfigEntry<string> TraceCategories = null!;
        public static ConfigEntry<int> LogFileMaxMB = null!;

        /// <summary>In-memory test overrides (fast_timers). Never written to the cfg file.</summary>
        private static readonly Dictionary<ConfigEntryBase, float> Overrides = new();

        public static bool FastTimers { get; private set; }

        public static void Bind(ConfigFile config)
        {
            _file = config;
            const string g = "1 - General", b = "2 - Base", h = "3 - Hiring", w = "4 - Work", f = "5 - Followers";

            FriendlyFireOnHirelings = Synced(g, "FriendlyFireOnHirelings", false, "Players can damage hirelings.");
            PermadeathEnabled = Synced(g, "PermadeathEnabled", true, "Dead hirelings are gone for good. Off: they come back to the board after a cooldown for a fee.");
            RespawnCooldownSeconds = Synced(g, "RespawnCooldownSeconds", 600f, "Seconds before a dead hireling returns (permadeath off).");
            RespawnCostFraction = Synced(g, "RespawnCostFraction", 0.5f, "Share of the hire fee charged to bring a dead hireling back (permadeath off).");
            AllowRawFood = Synced(g, "AllowRawFood", false, "Raw food (meat, berries, mushrooms…) counts toward hiring and upkeep.");

            BaseCheckRadius = Synced(b, "BaseCheckRadius", 20f, "Radius (m) around a new hiring board searched for the base requirements.");
            RequiredWorkbenches = Synced(b, "RequiredWorkbenches", 1, "Workbenches needed within the radius.");
            RequiredBeds = Synced(b, "RequiredBeds", 1, "Beds needed within the radius.");
            RequiredPieces = Synced(b, "RequiredPieces", 40, "Player-built pieces needed within the radius.");
            MinDistanceBetweenBoards = Synced(b, "MinDistanceBetweenBoards", 100f, "Minimum distance (m) between hiring boards.");
            MaxBoardsPerWorld = Synced(b, "MaxBoardsPerWorld", 0, "Hiring boards allowed per world (0 = unlimited).");

            ArrivalDelayMinSeconds = Synced(h, "ArrivalDelayMinSeconds", 90f, "Shortest wait before a hired viking arrives.");
            ArrivalDelayMaxSeconds = Synced(h, "ArrivalDelayMaxSeconds", 240f, "Longest wait before a hired viking arrives.");
            ArrivalSpawnDistance = Synced(h, "ArrivalSpawnDistance", 35f, "How far from the board (m) arriving vikings appear.");
            UnpaidDaysBeforeLeaving = Synced(h, "UnpaidDaysBeforeLeaving", 2, "Unpaid days a hireling puts up with before leaving.");
            DropPileOffset = Synced(h, "DropPileOffset", 2.5f, "Distance (m) in front of the board where overflow items are dropped.");

            AiScanIntervalSeconds = Synced(w, "AiScanIntervalSeconds", 2f, "Seconds between a hireling's scans for work (trees, rocks, chests).");
            PostLeashRadius = Synced("8 - Combat", "PostLeashRadius", 20f, "How far (m) a posted guard goes from its post to fight, before walking back to it.");
            ThreatScanIntervalSeconds = Synced("8 - Combat", "ThreatScanIntervalSeconds", 0.5f, "Seconds between a hireling's looks around for enemies. Lower reacts faster; being hit always triggers an immediate look.");
            HirelingsOpenDoors = Synced(w, "HirelingsOpenDoors", true, "Hirelings open doors in their way (only doors the board's owner may use under wards, never locked ones) and close them behind themselves.");
            TreeSafetyDistanceFromPieces = Synced(w, "TreeSafetyDistanceFromPieces", 6f, "Woodcutters skip trees with a player-built piece this close to the trunk (m).");
            TreeFallCorridorHalfWidth = Synced(w, "TreeFallCorridorHalfWidth", 4f, "Woodcutters fell a tree only in a direction where no player-built piece lies within the tree's height and this far either side of the fall line (m). If no direction is clear, the tree is left standing.");
            MinerProtectsTerrain = Synced(w, "MinerProtectsTerrain", true, "Hireling pickaxe swings never dig the ground near your buildings or inside their board's work area. Out in the field they dig like a player would, to get at ore sitting low in the ground. Turning this off lets miners dig anywhere, including in your base.");
            GatheringMakesNoise = Synced(w, "GatheringMakesNoise", true, "Chopping and mining hirelings make the same noise a player would, so nearby monsters hear them and come. Off: they work in silence (much safer, and much easier).");
            MinerFieldDigClearance = Synced(w, "MinerFieldDigClearance", 20f, "With MinerProtectsTerrain on, a miner may dig only where no player-built piece is within this distance (m).");
            MinerSafetyDistanceFromPieces = Synced(w, "MinerSafetyDistanceFromPieces", 3f, "Miners skip rocks with a player-built piece within this distance (m) of the rock's edge, since the rock may be holding the build up.");
            SmelterRefillThreshold = Synced(w, "SmelterRefillThreshold", 0.5f, "Smelters refill a station when its ore or fuel is below this fraction of max.");
            KeepMinimumInChest = Synced(w, "KeepMinimumInChest", 0, "Smelters leave at least this many of an item in each chest (on top of KeepLastItemInChest).");
            KeepLastItemInChest = Synced(w, "KeepLastItemInChest", true, "Smelters never take the last one of an item from a chest. Hirelings deliver only to chests that already hold an item, and AzuAutoStore by default only pulls items into such chests, so an emptied chest would stop receiving that item.");

            PortalFollowRadius = Synced(f, "PortalFollowRadius", 20f, "Followers within this distance (m) go through a portal with you.");
            AllowNonTeleportableThroughPortals = Synced(f, "AllowNonTeleportableThroughPortals", false, "Followers may carry ore and metals through portals.");
            GatherNearbyRadius = Synced(f, "GatherNearbyRadius", 15f, "Radius (m) around you that Gather Nearby followers work in.");
            FollowerCatchUpSpeedBonus = Synced(f, "FollowerCatchUpSpeedBonus", 0.25f, "Extra sprint speed (0.25 = +25%) for a follower more than 15 m behind you (twice this beyond 25 m), so it closes the gap even when you sprint with run skill and gear.");
            FollowerCatchUpTeleportDistance = Synced(f, "FollowerCatchUpTeleportDistance", 40f, "A following follower this far behind you (m), or stuck, appears on the ground just behind you, but only while you can't see it (off screen or out of sight). 0 turns catch-up teleports off.");
            FollowerStuckTeleportSeconds = Synced(f, "FollowerStuckTeleportSeconds", 5f, "Seconds a following follower can be stuck (not getting anywhere while it should be moving) before it's allowed to catch up by teleport.");
            ShipStowRadius = Synced(f, "ShipStowRadius", 20f, "Followers within this distance (m) board as passengers when you take the helm.");
            OrphanDistance = Synced(f, "OrphanDistance", 60f, "A following follower farther than this (m) for OrphanDistanceSeconds heads home.");
            OrphanDistanceSeconds = Synced(f, "OrphanDistanceSeconds", 30f, "See OrphanDistance.");
            OrphanStuckSeconds = Synced(f, "OrphanStuckSeconds", 20f, "A follower that can't make progress this long heads home.");
            OrphanStayDistance = Synced(f, "OrphanStayDistance", 150f, "A staying follower whose owner is farther than this (m) for OrphanStaySeconds heads home.");
            OrphanStaySeconds = Synced(f, "OrphanStaySeconds", 120f, "See OrphanStayDistance.");
            OrphanOfflineSeconds = Synced(f, "OrphanOfflineSeconds", 300f, "Followers out in the field whose owner has been offline this long (s) head home. Long enough to ride out a reconnect or a server restart.");
            ReturnHomeWithNonTeleportable = Synced(f, "ReturnHomeWithNonTeleportable", true, "A follower heading home (sent home, lost, or its owner gone) brings everything it carries. Off: it first drops whatever a portal wouldn't take (ore, metal…) where it stands, so going home isn't a free ore portal; the Send home button warns you.");
            ReturnSecondsPer100m = Synced(f, "ReturnSecondsPer100m", 25f, "Return-home trip time per 100 m of distance.");
            ReturnMinSeconds = Synced(f, "ReturnMinSeconds", 60f, "Shortest return-home trip.");
            ReturnMaxSeconds = Synced(f, "ReturnMaxSeconds", 1200f, "Longest return-home trip.");
            StoneBoardSearchRadius = Synced(f, "StoneBoardSearchRadius", 30f, "Radius (m) around the workbench searched for a hiring board when crafting a Command Stone.");

            BalanceLog = Synced("9 - Balance log", "BalanceLog", false, "Record how hirelings fight and work (each fight, death, delivery and upkeep day) to BepInEx/HiredHands/balance/*.jsonl on the server, for tuning the mod. Off by default. Set it on the server: every player's game then sends its summaries there once a minute.");
            BalanceLogMaxMB = _file.Bind("9 - Balance log", "BalanceLogMaxMB", 20, "Server: total size (MB) of the balance log files kept; the oldest days are deleted beyond it.");

            MeleeAttackCooldown = Synced("8 - Combat", "MeleeAttackCooldown", 1.2f, "Seconds between a hireling's melee swings.");
            RangedAttackCooldown = Synced("8 - Combat", "RangedAttackCooldown", 2.5f, "Seconds between an archer's shots (at least the bow's draw time plus a beat).");

            LogToFile = config.Bind("7 - Debug", "LogToFile", true, "Also write log lines to BepInEx/HiredHands.log.");
            DebugCategories = config.Bind("7 - Debug", "DebugCategories", "", $"Categories logging at Debug, comma separated, or All. Categories: {string.Join(", ", System.Enum.GetNames(typeof(LogCat)))}.");
            TraceCategories = config.Bind("7 - Debug", "TraceCategories", "", "Categories logging at Trace (very verbose; implies Debug).");
            LogFileMaxMB = config.Bind("7 - Debug", "LogFileMaxMB", 10, "HiredHands.log size before rotating (3 backups kept).");

            ApplyLogSettings();
            DebugCategories.SettingChanged += (_, _) => ApplyLogSettings();
            TraceCategories.SettingChanged += (_, _) => ApplyLogSettings();
            LogToFile.SettingChanged += (_, _) => ApplyLogSettings();
            LogFileMaxMB.SettingChanged += (_, _) => ApplyLogSettings();
        }

        public static void ApplyLogSettings()
        {
            var debug = LogFilter.ParseCategories(DebugCategories.Value, out List<string> badDebug);
            var trace = LogFilter.ParseCategories(TraceCategories.Value, out List<string> badTrace);
            VfhLog.SetFilter(new LogFilter(debug, trace));
            VfhLog.SetFileLogging(LogToFile.Value, LogFileMaxMB.Value);
            foreach (string bad in badDebug.Concat(badTrace))
                VfhLog.W(LogCat.Core, "config.unknown_category", ("name", bad));
        }

        public static float Get(ConfigEntry<float> entry) => Overrides.TryGetValue(entry, out float v) ? v : entry.Value;

        /// <summary>
        /// Test mode: short arrival/respawn waits and orphan/return timers ÷10, so macros finish in seconds. Memory only.
        /// </summary>
        public static void SetFastTimers(bool on)
        {
            FastTimers = on;
            Overrides.Clear();
            if (on)
            {
                Overrides[ArrivalDelayMinSeconds] = 5f;
                Overrides[ArrivalDelayMaxSeconds] = 10f;
                Overrides[RespawnCooldownSeconds] = 10f;
                foreach (ConfigEntry<float> e in new[] { OrphanDistanceSeconds, OrphanStuckSeconds, OrphanStaySeconds, OrphanOfflineSeconds,
                             ReturnSecondsPer100m, ReturnMinSeconds, ReturnMaxSeconds })
                    Overrides[e] = e.Value / 10f;
            }
            VfhLog.I(LogCat.Test, "fast_timers", ("on", on),
                ("overrides", string.Join(",", Overrides.Select(o => $"{o.Key.Definition.Key}:{o.Value:0.##}"))));
        }

        /// <summary>Finds an entry by key name (section ignored); used by the cfg test check and the session header.</summary>
        public static ConfigEntryBase? Find(string key) =>
            _file.Keys.Where(k => string.Equals(k.Key, key, System.StringComparison.OrdinalIgnoreCase))
                .Select(k => _file[k]).FirstOrDefault();

        /// <summary>
        /// The value in effect. Not GetSerializedValue: Jotunn patches that to return the player's own local value for
        /// server-synced entries (so the local file is never overwritten), which hides the server's value.
        /// </summary>
        public static string EffectiveValue(ConfigEntryBase entry) =>
            entry is ConfigEntry<float> fe && Overrides.ContainsKey(fe)
                ? Get(fe).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType);

        public static IEnumerable<(string Key, string Value)> AllEffective() =>
            _file.Keys.OrderBy(k => k.Section).ThenBy(k => k.Key).Select(k => ($"{k.Section}/{k.Key}", EffectiveValue(_file[k])));

        /// <summary>How many of an item a smelter leaves in each chest.</summary>
        public static int ChestReserve => System.Math.Max(KeepMinimumInChest.Value, KeepLastItemInChest.Value ? 1 : 0);

        private static ConfigEntry<T> Synced<T>(string section, string key, T value, string description) =>
            _file.Bind(section, key, value, new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { IsAdminOnly = true }));
    }
}
