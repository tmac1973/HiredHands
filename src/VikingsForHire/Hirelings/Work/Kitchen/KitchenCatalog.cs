using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Orders;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Kitchen
{
    /// <summary>
    /// Every way the Cook can make food, read from the game once a world is loaded: each cooking station's and the oven's
    /// conversions, and the recipes of the cauldron, food preparation table and mead ketill (vanilla and modded). Levels come
    /// from jobs.Cook.stationLevels (a cauldron recipe needing cauldron level N adds N - 1), with recipeLevels overrides.
    /// </summary>
    internal static class KitchenCatalog
    {
        /// <summary>Crafting stations the Cook works at (prefab names).</summary>
        public static readonly HashSet<string> CraftStations = new() { "piece_cauldron", "piece_preptable", "piece_MeadCauldron" };

        private static readonly List<KitchenInfo> _all = new();
        private static readonly Dictionary<KitchenInfo, Recipe> _recipes = new();

        public static IReadOnlyList<KitchenInfo> All => _all;

        public static Recipe? RecipeOf(KitchenInfo info) => _recipes.TryGetValue(info, out Recipe r) ? r : null;

        public static void Register()
        {
            PrefabManager.OnPrefabsRegistered += () => VfhLog.Guard(LogCat.Work, "kitchen.build_failed", Build);
            DataStore.Changed += () => VfhLog.Guard(LogCat.Work, "kitchen.levels_failed", Build);
        }

        public static void Build()
        {
            if (ObjectDB.instance == null || ZNetScene.instance == null)
                return;
            _all.Clear();
            _recipes.Clear();
            JobData cook = DataStore.Current.Jobs.TryGetValue(JobType.Cook, out JobData? j) ? j : new JobData();
            int StationLevel(string prefab) => cook.StationLevels.TryGetValue(prefab, out int l) ? l : 1;
            int Level(string output, int computed) => cook.RecipeLevels.TryGetValue(output, out int l) ? l : Mathf.Clamp(computed, 1, 8);

            foreach (GameObject go in ZNetScene.instance.m_prefabs.Where(p => p != null && p.GetComponent<Piece>() != null))
            {
                CookingStation? stove = go.GetComponent<CookingStation>();
                if (stove == null)
                    continue;
                foreach (CookingStation.ItemConversion c in stove.m_conversion.Where(c => c?.m_from != null && c.m_to != null))
                    _all.Add(new KitchenInfo
                    {
                        Output = c.m_to.name, Station = go.name, Kind = StationKind.Stove,
                        Inputs = new Dictionary<string, int> { [c.m_from.name] = 1 },
                        CookSeconds = c.m_cookTime, Level = Level(c.m_to.name, StationLevel(go.name)),
                    });
            }
            foreach (Recipe r in ObjectDB.instance.m_recipes.Where(r => r != null && r.m_enabled && r.m_item != null && r.m_craftingStation != null))
            {
                string station = Utils.GetPrefabName(r.m_craftingStation.gameObject);
                if (!CraftStations.Contains(station) || r.m_resources == null || r.m_resources.Length == 0)
                    continue;
                var info = new KitchenInfo
                {
                    Output = r.m_item.name, OutputAmount = Mathf.Max(1, r.m_amount), Station = station, Kind = StationKind.Craft,
                    Inputs = r.m_resources.Where(x => x?.m_resItem != null && x.m_amount > 0)
                        .GroupBy(x => x.m_resItem.name).ToDictionary(g => g.Key, g => g.Sum(x => x.m_amount)),
                    StationLevelNeeded = Mathf.Max(1, r.m_minStationLevel),
                    Level = Level(r.m_item.name, StationLevel(station) + Mathf.Max(1, r.m_minStationLevel) - 1),
                };
                if (info.Inputs.Count == 0)
                    continue; // a modded recipe with only per-level amounts: nothing to make it from at quality 1
                _all.Add(info);
                _recipes[info] = r;
            }
            VfhLog.I(LogCat.Work, "kitchen.built", ("stove", _all.Count(i => i.Kind == StationKind.Stove)), ("craft", _all.Count(i => i.Kind == StationKind.Craft)));
        }
    }
}
