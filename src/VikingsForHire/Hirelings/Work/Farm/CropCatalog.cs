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

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>A crop as the game has it, with the planner's description.</summary>
    internal sealed class Crop
    {
        public CropInfo Info { get; set; } = new();
        public GameObject Prefab { get; set; } = null!;

        /// <summary>Planted crops: the prefab a ripe one becomes (Pickable_Carrot…); regrowing: its own prefab.</summary>
        public string GrownPrefab { get; set; } = "";

        public Heightmap.Biome Biome { get; set; }
        public float GrowRadius { get; set; } = 0.5f;
        public bool NeedCultivated { get; set; } = true;
        public bool TolerateCold { get; set; }
        public bool TolerateHeat { get; set; }

        /// <summary>A wild regrowing plant (not one the cultivator plants): unlisted, it waits for the last level.</summary>
        public bool Wild { get; set; }
    }

    /// <summary>
    /// Every crop the game knows, read from its prefabs once a world is loaded: the cultivator's saplings (vanilla, Deep
    /// North, any mod's), PlantEverything's planted pickables and the wild regrowing ones (berry bushes, mushrooms…),
    /// with levels from the data file (jobs.Farmer.cropLevels by item).
    /// </summary>
    internal static class CropCatalog
    {
        /// <summary>Never picked as a crop, even where PlantEverything makes them plantable.</summary>
        private static readonly HashSet<string> NotCrops = new() { "Stone", "Flint", "Wood", "RoundLog", "FineWood" };

        private static readonly List<Crop> _all = new();
        private static readonly Dictionary<string, Crop> _byGrown = new();
        private static readonly Dictionary<string, Crop> _bySapling = new();
        private static readonly HashSet<int> _grownHashes = new();
        private static readonly HashSet<int> _saplingHashes = new();

        public static IReadOnlyList<Crop> All
        {
            get
            {
                EnsureBuilt();
                return _all;
            }
        }

        public static IReadOnlyList<CropInfo> Infos => All.Select(c => c.Info).ToList();

        private static float _nextTry;

        // Built when first needed in a world (the prefab event can come before the item database is ready), retried every 5 s while empty.
        private static void EnsureBuilt()
        {
            if (_all.Count > 0 || Time.time < _nextTry)
                return;
            _nextTry = Time.time + 5f;
            Build();
        }

        public static void Register()
        {
            PrefabManager.OnPrefabsRegistered += () => VfhLog.Guard(LogCat.Work, "crops.build_failed", Build);
            DataStore.Changed += () => VfhLog.Guard(LogCat.Work, "crops.levels_failed", ApplyLevels);
        }

        public static Crop? ByGrown(string prefab)
        {
            EnsureBuilt();
            return _byGrown.TryGetValue(prefab, out Crop c) ? c : null;
        }

        public static Crop? BySapling(string prefab)
        {
            EnsureBuilt();
            return _bySapling.TryGetValue(prefab, out Crop c) ? c : null;
        }

        public static bool IsGrownHash(int prefabHash)
        {
            EnsureBuilt();
            return _grownHashes.Contains(prefabHash);
        }

        public static bool IsSaplingHash(int prefabHash) => _saplingHashes.Contains(prefabHash);

        public static void Build()
        {
            if (ObjectDB.instance == null || ZNetScene.instance == null || ObjectDB.instance.GetItemPrefab("Cultivator") == null)
            {
                VfhLog.D(LogCat.Work, "crops.not_ready", ("objectDb", ObjectDB.instance != null), ("scene", ZNetScene.instance != null));
                return;
            }
            _all.Clear();
            _byGrown.Clear();
            _bySapling.Clear();
            ItemDrop? cultivator = ObjectDB.instance.GetItemPrefab("Cultivator")?.GetComponent<ItemDrop>();
            List<GameObject> pieces = cultivator?.m_itemData.m_shared.m_buildPieces?.m_pieces ?? new List<GameObject>();
            JobData farmerData = DataStore.Current.Jobs.TryGetValue(JobType.Farmer, out JobData? fd) ? fd : new JobData();
            foreach (GameObject go in pieces.Where(p => p != null))
                if (go.GetComponent<Plant>() is Plant plant)
                    AddPlanted(go, plant);
            // What counts as produce for a regrowing pickable: food, an item the crop levels name, a seed some crop is
            // planted from, or something the Cook cooks with. PlantEverything (and other mods) put all sorts of pickables
            // on the cultivator (surtling core stands, crypt loot…): those aren't farming.
            var seeds = new HashSet<string>(_all.Select(c => c.Info.Consumes).Where(s => s.Length > 0));
            var ingredients = new HashSet<string>(Kitchen.KitchenCatalog.All.SelectMany(k => k.Inputs.Keys));
            bool Produce(GameObject item) => IsFood(item) || farmerData.CropLevels.ContainsKey(item.name) || seeds.Contains(item.name) || ingredients.Contains(item.name);
            foreach (GameObject go in pieces.Where(p => p != null && p.GetComponent<Plant>() == null))
                if (go.GetComponent<Pickable>() is Pickable pick && pick.m_respawnTimeMinutes > 0f && pick.m_itemPrefab != null)
                {
                    if (Produce(pick.m_itemPrefab))
                        AddRegrowing(go, pick);
                    else
                        Reject(go.name, pick.m_itemPrefab, "not produce");
                }
            // Wild regrowing plants: only food, or items the crop levels name (thistle, dandelion…), not surtling core stands and the like.
            JobData farmer = DataStore.Current.Jobs.TryGetValue(JobType.Farmer, out JobData? fj) ? fj : new JobData();
            foreach (GameObject go in ZNetScene.instance.m_prefabs.Where(p => p != null && !_byGrown.ContainsKey(p.name)))
                if (go.GetComponent<Pickable>() is Pickable pick && pick.m_respawnTimeMinutes > 0f && go.GetComponent<Plant>() == null &&
                    pick.m_itemPrefab != null && (IsFood(pick.m_itemPrefab) || farmer.CropLevels.ContainsKey(pick.m_itemPrefab.name)) && FarmItem(pick.m_itemPrefab))
                    AddRegrowing(go, pick, wild: true);
            ApplyLevels();
            _grownHashes.Clear();
            _saplingHashes.Clear();
            foreach (Crop c in _all)
            {
                _grownHashes.Add(c.GrownPrefab.GetStableHashCode());
                if (!c.Info.Regrowing)
                    _saplingHashes.Add(c.Info.Plant.GetStableHashCode());
            }
            VfhLog.I(LogCat.Work, "crops.built", ("planted", _all.Count(c => !c.Info.Regrowing)), ("regrowing", _all.Count(c => c.Info.Regrowing)),
                ("plantEverything", Compat.PlantMods.IsPlantEverything), ("plantEasily", Compat.PlantMods.IsPlantEasily));
        }

        /// <summary>
        /// The kinds of item a farm can yield: things you eat or make things from (crops, seeds, berries, flax…). Never
        /// armor, weapons, tools, trophies or ammo, whatever plant a mod hangs them on.
        /// </summary>
        public static bool FarmItem(GameObject item) =>
            item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_itemType is ItemDrop.ItemData.ItemType t &&
            (t == ItemDrop.ItemData.ItemType.Material || t == ItemDrop.ItemData.ItemType.Consumable);

        private static void Reject(string plant, GameObject item, string why) =>
            VfhLog.I(LogCat.Work, "crops.rejected", ("plant", plant), ("item", item.name),
                ("type", item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_itemType.ToString() ?? "?"), ("why", why));

        public static bool IsFood(GameObject item) =>
            item.GetComponent<ItemDrop>()?.m_itemData.m_shared is ItemDrop.ItemData.SharedData s && (s.m_food > 0f || s.m_foodStamina > 0f || s.m_foodEitr > 0f);

        private static void AddPlanted(GameObject go, Plant plant)
        {
            GameObject? grown = plant.m_grownPrefabs?.FirstOrDefault(g => g != null);
            Pickable? pick = grown != null ? grown.GetComponent<Pickable>() : null;
            Piece? piece = go.GetComponent<Piece>();
            Piece.Requirement? seed = piece?.m_resources?.FirstOrDefault(r => r?.m_resItem != null);
            if (grown == null || pick?.m_itemPrefab == null || seed == null || NotCrops.Contains(pick.m_itemPrefab.name))
                return;
            if (!FarmItem(pick.m_itemPrefab))
            {
                Reject(go.name, pick.m_itemPrefab, "not a farm item");
                return;
            }
            var crop = new Crop
            {
                Prefab = go,
                GrownPrefab = grown.name,
                Biome = plant.m_biome,
                GrowRadius = plant.m_growRadius,
                NeedCultivated = plant.m_needCultivatedGround,
                TolerateCold = plant.m_tolerateCold,
                TolerateHeat = plant.m_tolerateHeat,
                Info = new CropInfo
                {
                    Plant = go.name,
                    Consumes = seed.m_resItem.name,
                    ConsumesAmount = Mathf.Max(1, seed.m_amount),
                    Yields = pick.m_itemPrefab.name,
                    YieldPerPlant = Mathf.Max(1, PickableYield.Main(pick)),
                    ExtraYields = PickableYield.ExpectedExtras(pick),
                },
            };
            _all.Add(crop);
            _byGrown[crop.GrownPrefab] = crop;
            _bySapling[go.name] = crop;
        }

        private static void AddRegrowing(GameObject go, Pickable pick, bool wild = false)
        {
            if (pick.m_itemPrefab == null || NotCrops.Contains(pick.m_itemPrefab.name) || _byGrown.ContainsKey(go.name))
                return;
            if (!FarmItem(pick.m_itemPrefab))
            {
                Reject(go.name, pick.m_itemPrefab, "not a farm item");
                return;
            }
            var crop = new Crop
            {
                Prefab = go,
                GrownPrefab = go.name,
                Wild = wild,
                Info = new CropInfo
                {
                    Plant = go.name,
                    Yields = pick.m_itemPrefab.name,
                    YieldPerPlant = Mathf.Max(1, PickableYield.Main(pick)),
                    Regrowing = true,
                },
            };
            _all.Add(crop);
            _byGrown[go.name] = crop;
        }

        private static void ApplyLevels()
        {
            JobData farmer = DataStore.Current.Jobs.TryGetValue(JobType.Farmer, out JobData? j) ? j : new JobData();
            // Listed: that level. Unlisted: 1 for crops the cultivator plants (modded ones), the last level for wild plants, so
            // new content (Deep North berries and the like) doesn't turn up before its biome.
            foreach (Crop c in _all)
                c.Info.Level = farmer.CropLevels.TryGetValue(c.Info.Yields, out int l) ? l : c.Wild ? 8 : 1;
        }
    }
}
