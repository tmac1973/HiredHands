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

        /// <summary>Order item -> why that order can't be worked (activity text); short orders being worked aren't here.</summary>
        public Dictionary<string, string> Why { get; } = new();

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
            // Seed orders that can't be worked at all (nothing to plant for seeds, nothing growing them): their reserve
            // doesn't hold crop orders back. 7 onion seeds under a 100-seed order and no onions: the seed order needs
            // onions, the onion order may only use seeds above the reserve, and neither ever moved. Planting the seeds is
            // the way out: onions grow, and the seed order plants those for seeds.
            var bootstrap = new HashSet<string>();

            // Seeds a produce order is waiting for, held back by the seed reserve: the seed order grows that many more, so
            // there are spares above the reserve to plant. Grima's onion seeds sat exactly at their reserve: the seed order
            // was met (no seed onions planted), the onion order could only use seeds above it, and neither moved. At most
            // what the free field takes for that produce, so a big order doesn't fill the field with seed plants.
            var spares = new Dictionary<string, int>();
            foreach (ProductionOrder order in active.Where(o => o.Kind != OrderKind.Seed))
            {
                double shortBy = order.Target - Get(stock, order.Item) - Get(growing, order.Item);
                if (shortBy <= 0)
                    continue;
                CropInfo? crop = crops.Where(c => !c.Regrowing && c.Level <= level && c.Consumes.Length > 0 && c.YieldOf(order.Item) > 0 &&
                                                  plan.SeedReserve.ContainsKey(c.Consumes))
                    .OrderByDescending(c => c.Yields == order.Item).ThenByDescending(c => c.YieldOf(order.Item)).FirstOrDefault();
                if (crop == null)
                    continue;
                int each = Math.Max(1, crop.ConsumesAmount);
                int plants = Math.Min((int)Math.Ceiling(shortBy / crop.YieldOf(order.Item)), spots.TryGetValue(crop.Plant, out int fs) ? fs : 0);
                int above = Math.Max(0, Get(stock, crop.Consumes) - plan.SeedReserve[crop.Consumes]);
                int want = plants * each - above;
                if (want > 0)
                    spares[crop.Consumes] = (spares.TryGetValue(crop.Consumes, out int sp) ? sp : 0) + want;
            }

            foreach (ProductionOrder order in active)
            {
                double have = Get(stock, order.Item) + Get(growing, order.Item) + (planned.TryGetValue(order.Item, out double p) ? p : 0d);
                double shortBy = order.Target + (order.Kind == OrderKind.Seed && spares.TryGetValue(order.Item, out int spare) ? spare : 0) - have;
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
                    int reserve = order.Kind == OrderKind.Seed || bootstrap.Contains(crop.Consumes) ? 0
                        : (plan.SeedReserve.TryGetValue(crop.Consumes, out int r) ? r : 0);
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
                // Only when the reserve itself is short: spares for a produce order never let it spend the reserve.
                if (order.Kind == OrderKind.Seed && plantedHere == 0 && Get(growing, order.Item) == 0 && have < order.Target &&
                    candidates.All(c => Get(work, c.Consumes) <= 0))
                    bootstrap.Add(order.Item);
                if (plantedHere > 0 || regrows)
                    continue;
                string? why = candidates.Count == 0 && growsIt.Count > 0 ? $"$vfh_need_farmer_level|{order.Item}|{growsIt.Min(c => c.Level)}"
                    : blocked ?? (growsIt.Count == 0 ? $"$vfh_need_no_crop|{order.Item}" : null);
                if (why == null)
                    continue;
                if (growsIt.Count > 0)
                    plan.Missing.Add(why);
                plan.Why[order.Item] = why;
            }
            return plan;
        }
    }
}
