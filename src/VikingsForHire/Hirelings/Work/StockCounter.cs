using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Hirelings.Work.Farm;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>What a base has of each item, as orders count it: chests above their reserves, and what's growing.</summary>
    internal sealed class Stock
    {
        public Dictionary<string, int> Chests { get; } = new();
        public Dictionary<string, int> Growing { get; } = new();

        /// <summary>
        /// Left in the board's pile in the area (no chest takes it yet, e.g. the first mead base): made, so it counts
        /// toward an order's target, but nobody can fetch it as an ingredient.
        /// </summary>
        public Dictionary<string, int> Piled { get; } = new();

        public int Have(string item) => (Chests.TryGetValue(item, out int c) ? c : 0) + (Growing.TryGetValue(item, out int g) ? g : 0) +
                                        (Piled.TryGetValue(item, out int p) ? p : 0);
    }

    internal static class StockCounter
    {
        private const float CacheSeconds = 5f;
        private static readonly Dictionary<string, (float At, Stock Stock)> Cache = new();
        private static readonly List<Plant> Plants = new();
        private static readonly List<Pickable> Pickables = new();

        /// <summary>Count afresh next time (after planting, harvesting or cooking changed things).</summary>
        public static void Forget() => Cache.Clear();

        public static Stock Count(Vector3 home, float radius) => Count(new[] { (home, radius) });

        /// <summary>
        /// The same over several areas together (the board's and its posted workers' round their posts), each chest, pile
        /// and plant counted once.
        /// </summary>
        public static Stock Count(IReadOnlyList<(Vector3 Centre, float Radius)> areas)
        {
            string key = string.Join(";", areas.Select(a => $"{a.Centre.x:0.0},{a.Centre.y:0.0},{a.Centre.z:0.0},{a.Radius:0.0}"));
            if (Cache.TryGetValue(key, out var hit) && Time.time - hit.At < CacheSeconds)
                return hit.Stock;
            bool Inside(Vector3 p) => areas.Any(a => Utils.DistanceXZ(p, a.Centre) <= a.Radius);
            var stock = new Stock();
            Dictionary<string, int> keep = DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out JobData? s) ? s.KeepInStorage : new Dictionary<string, int>();
            var total = new Dictionary<string, int>();
            foreach (Container c in ChestModes.Stock(areas.SelectMany(a => ChestFinder.Find(a.Centre, a.Radius)).Distinct()))
                foreach (var g in c.GetInventory().GetAllItems().Where(i => i.m_dropPrefab != null).GroupBy(i => i.m_dropPrefab.name))
                {
                    int n = g.Sum(i => i.m_stack);
                    total[g.Key] = (total.TryGetValue(g.Key, out int t) ? t : 0) + n;
                    stock.Chests[g.Key] = (stock.Chests.TryGetValue(g.Key, out int a) ? a : 0) + System.Math.Max(0, n - VfhConfig.ChestReserve);
                }
            foreach (KeyValuePair<string, int> k in keep)
                if (stock.Chests.ContainsKey(k.Key))
                    stock.Chests[k.Key] = System.Math.Min(stock.Chests[k.Key], System.Math.Max(0, total[k.Key] - k.Value));

            foreach (ItemDrop d in ItemDrop.s_instances)
                if (d != null && d.m_itemData?.m_dropPrefab != null && d.m_itemData.m_customData.ContainsKey(DropPile.Tag) &&
                    Inside(d.transform.position))
                    stock.Piled[d.m_itemData.m_dropPrefab.name] = (stock.Piled.TryGetValue(d.m_itemData.m_dropPrefab.name, out int pn) ? pn : 0) + d.m_itemData.m_stack;
            var plants = new HashSet<Plant>();
            var pickables = new HashSet<Pickable>();
            foreach ((Vector3 centre, float radius) in areas)
            {
                FarmScan.Nearby(centre, radius, Plants, Pickables);
                plants.UnionWith(Plants);
                pickables.UnionWith(Pickables);
            }
            // Only plants that are growing: one that can't (too hot, no sun…) will wither, so it doesn't count.
            foreach (Plant p in plants.Where(p => p != null && p.GetStatus() == Plant.Status.Healthy))
                if (CropCatalog.BySapling(Utils.GetPrefabName(p.gameObject)) is Crop crop)
                    Add(stock.Growing, crop);
            foreach (Pickable p in pickables.Where(p => p != null))
                if (CropCatalog.ByGrown(Utils.GetPrefabName(p.gameObject)) is Crop crop && !crop.Info.Regrowing && PickableYield.Ripe(p))
                    Add(stock.Growing, crop);
            Cache[key] = (Time.time, stock);
            return stock;
        }

        private static void Add(Dictionary<string, int> growing, Crop crop)
        {
            growing[crop.Info.Yields] = (growing.TryGetValue(crop.Info.Yields, out int n) ? n : 0) + crop.Info.YieldPerPlant;
            foreach (KeyValuePair<string, double> x in crop.Info.ExtraYields)
                growing[x.Key] = (growing.TryGetValue(x.Key, out int m) ? m : 0) + (int)x.Value;
        }
    }
}
