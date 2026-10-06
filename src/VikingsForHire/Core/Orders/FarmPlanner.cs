using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Orders
{
    public sealed class FarmPlan
    {
        /// <summary>What to plant, most important first.</summary>
        public List<(CropInfo Crop, int Count)> Plant { get; } = new();

        /// <summary>Items whose regrowing plants (bushes, mushrooms) should be picked: an order for them is short.</summary>
        public HashSet<string> PickRegrowing { get; } = new();

        /// <summary>Seed item -> the seed order's target: never planted for produce, never used by the Cook.</summary>
        public Dictionary<string, int> SeedReserve { get; } = new();

        /// <summary>What the planting for seed orders uses up (the carrots that grow carrot seeds): kept from the Cook too.</summary>
        public Dictionary<string, int> PlantedForSeeds { get; } = new();

        /// <summary>Why an order can't be worked, as activity text ("token|arg|arg"), first order first.</summary>
        public List<string> Missing { get; } = new();

        /// <summary>Items (and amounts) the Cook mustn't take: the seed reserve plus what seed-order planting needs.</summary>
        public Dictionary<string, int> Protected()
        {
            var p = new Dictionary<string, int>(SeedReserve);
            foreach (KeyValuePair<string, int> kv in PlantedForSeeds)
                p[kv.Key] = (p.TryGetValue(kv.Key, out int n) ? n : 0) + kv.Value;
            return p;
        }
    }

    /// <summary>
    /// Turns the board's orders and the stock into what to plant: seed orders first (planting the produce that grows
    /// seeds), then crop orders out of the seeds above the seed reserve. Stock counts what's growing, so a field already on
    /// its way isn't planted twice.
    /// </summary>
    public static class FarmPlanner
    {
        public static FarmPlan Plan(IReadOnlyList<CropInfo> crops, OrderList orders, IReadOnlyDictionary<string, int> stock,
            IReadOnlyDictionary<string, int> growing, int level, IDictionary<string, int> freeSpots)
        {
            var plan = new FarmPlan();
            List<ProductionOrder> active = orders.Active().Where(o => o.IsFarm).ToList();
            foreach (ProductionOrder o in active.Where(o => o.Kind == OrderKind.Seed))
                plan.SeedReserve[o.Item] = o.Target;

            var work = new Dictionary<string, int>(stock.ToDictionary(kv => kv.Key, kv => kv.Value));
            var spots = new Dictionary<string, int>(freeSpots);
            var planned = new Dictionary<string, double>();
            int Get(IReadOnlyDictionary<string, int> d, string k) => d.TryGetValue(k, out int v) ? v : 0;

            foreach (ProductionOrder order in active)
            {
                double have = Get(stock, order.Item) + Get(growing, order.Item) + (planned.TryGetValue(order.Item, out double p) ? p : 0d);
                double shortBy = order.Target - have;
                if (shortBy <= 0)
                    continue;
                bool regrows = crops.Any(c => c.Regrowing && c.Level <= level && c.YieldOf(order.Item) > 0);
                if (regrows)
                    plan.PickRegrowing.Add(order.Item);

                List<CropInfo> growsIt = crops.Where(c => !c.Regrowing && c.YieldOf(order.Item) > 0).ToList();
                List<CropInfo> candidates = growsIt.Where(c => c.Level <= level && c.Consumes.Length > 0)
                    .OrderByDescending(c => c.Yields == order.Item).ThenByDescending(c => c.YieldOf(order.Item)).ToList();
                int plantedHere = 0;
                string? blocked = null;
                foreach (CropInfo crop in candidates)
                {
                    double each = crop.YieldOf(order.Item);
                    int needed = (int)Math.Ceiling(shortBy / each);
                    // Raising seeds may spend produce down to the usual reserves; produce orders only spend seeds above the seed reserve.
                    int reserve = order.Kind == OrderKind.Seed ? 0 : (plan.SeedReserve.TryGetValue(crop.Consumes, out int r) ? r : 0);
                    int have0 = Get(work, crop.Consumes);
                    int possible = Math.Max(0, have0 - reserve) / Math.Max(1, crop.ConsumesAmount);
                    int free = spots.TryGetValue(crop.Plant, out int f) ? f : 0;
                    int count = Math.Min(needed, Math.Min(possible, free));
                    if (count <= 0)
                    {
                        blocked ??= free <= 0 ? "$vfh_need_field"
                            : have0 > 0 && reserve > 0 ? $"$vfh_need_seed_reserve|{order.Item}|{crop.Consumes}"
                            : $"$vfh_need_seed|{order.Item}|{crop.Consumes}";
                        continue;
                    }
                    int used = count * Math.Max(1, crop.ConsumesAmount);
                    work[crop.Consumes] = have0 - used;
                    foreach (string k in spots.Keys.ToList())
                        spots[k] = Math.Max(0, spots[k] - count);
                    planned[order.Item] = (planned.TryGetValue(order.Item, out double q) ? q : 0d) + count * each;
                    if (order.Kind == OrderKind.Seed)
                        plan.PlantedForSeeds[crop.Consumes] = (plan.PlantedForSeeds.TryGetValue(crop.Consumes, out int u) ? u : 0) + used;
                    plan.Plant.Add((crop, count));
                    plantedHere += count;
                    shortBy -= count * each;
                    if (shortBy <= 0)
                        break;
                }
                if (plantedHere > 0 || regrows)
                    continue;
                if (candidates.Count == 0 && growsIt.Count > 0)
                    plan.Missing.Add($"$vfh_need_farmer_level|{order.Item}|{growsIt.Min(c => c.Level)}");
                else if (blocked != null)
                    plan.Missing.Add(blocked);
            }
            return plan;
        }
    }
}
