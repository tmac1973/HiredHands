using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Orders
{
    public sealed class RanchPlan
    {
        /// <summary>The animal to slaughter next, or null.</summary>
        public HerdAnimal? Slaughter { get; set; }

        /// <summary>The meat order it's slaughtered for.</summary>
        public string ForOrder { get; set; } = "";

        /// <summary>Egg items to pick up now (their order is short).</summary>
        public HashSet<string> CollectEggs { get; } = new();

        /// <summary>Order item (meat, egg or animal kind) -> why it isn't being met, as activity text ("token|arg|arg").</summary>
        public Dictionary<string, string> Why { get; } = new();

        /// <summary>Animal kind -> its unnamed adults (the breeding stock and what may be slaughtered).</summary>
        public Dictionary<string, int> Adults { get; } = new();
    }

    /// <summary>
    /// The Rancher's decisions from the herd, the board's ranch orders and the stock: slaughter only for a short meat order,
    /// only above a kind's breeding pool, lowest stars first (the pool is always the best stars), never young or named
    /// animals, never a kind with no breeding pool order; collect eggs while their order is short; and why each order
    /// isn't being met. The herd arrives sorted by Id, so ties always go the same way.
    /// </summary>
    public static class RanchPlanner
    {
        public static RanchPlan Plan(IReadOnlyList<AnimalKind> kinds, IReadOnlyList<HerdAnimal> herd, OrderList orders,
            IReadOnlyDictionary<string, int> stock, ICollection<string> kindsWithFood, int extraPool = 0)
        {
            var plan = new RanchPlan();
            int Stock(string item) => stock.TryGetValue(item, out int n) ? n : 0;
            List<ProductionOrder> ranch = orders.Ranch();
            Dictionary<string, int> pools = ranch.Where(o => o.Kind == OrderKind.Herd)
                .GroupBy(o => o.Item, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Target + Math.Max(0, extraPool), StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, HerdAnimal> g in herd.Where(a => a.Adult && !a.Named).GroupBy(a => a.Kind))
                plan.Adults[g.Key] = g.Count();

            // Above its pool, a kind's unnamed adults that aren't among its best: lowest stars go first.
            List<HerdAnimal> Cullable(string kind)
            {
                if (!pools.TryGetValue(kind, out int pool))
                    return new List<HerdAnimal>();
                List<HerdAnimal> stockAnimals = herd.Where(a => a.Kind == kind && a.Adult && !a.Named).ToList();
                List<HerdAnimal> keep = stockAnimals.OrderByDescending(a => a.Stars).Take(pool).ToList();
                return stockAnimals.Where(a => !keep.Contains(a)).ToList();
            }

            foreach (ProductionOrder order in ranch.Where(o => o.Kind == OrderKind.Meat))
            {
                if (Stock(order.Item) >= order.Target)
                    continue;
                List<AnimalKind> dropping = kinds.Where(k => k.Meats.ContainsKey(order.Item)).ToList();
                if (dropping.Count == 0)
                {
                    plan.Why[order.Item] = $"$vfh_ranch_no_animal|{order.Item}";
                    continue;
                }
                List<AnimalKind> pooled = dropping.Where(k => pools.ContainsKey(k.Prefab)).ToList();
                if (pooled.Count == 0)
                {
                    plan.Why[order.Item] = $"$vfh_ranch_need_pool|{order.Item}|{dropping[0].Prefab}";
                    continue;
                }
                HerdAnimal? pick = pooled.SelectMany(k => Cullable(k.Prefab)).OrderBy(a => a.Stars).FirstOrDefault();
                if (pick == null)
                {
                    plan.Why[order.Item] = $"$vfh_ranch_at_pool|{order.Item}|{pooled[0].Prefab}|{pools[pooled[0].Prefab]}";
                    continue;
                }
                if (plan.Slaughter == null)
                {
                    plan.Slaughter = pick;
                    plan.ForOrder = order.Item;
                }
            }

            foreach (ProductionOrder order in ranch.Where(o => o.Kind == OrderKind.Egg))
            {
                if (Stock(order.Item) >= order.Target)
                    continue;
                bool layers = kinds.Any(k => k.EggItem == order.Item && herd.Any(a => a.Kind == k.Prefab && a.Adult));
                if (layers)
                    plan.CollectEggs.Add(order.Item);
                else
                    plan.Why[order.Item] = $"$vfh_ranch_no_layers|{order.Item}";
            }

            foreach (ProductionOrder order in ranch.Where(o => o.Kind == OrderKind.Herd))
            {
                AnimalKind? kind = kinds.FirstOrDefault(k => string.Equals(k.Prefab, order.Item, StringComparison.OrdinalIgnoreCase));
                if (kind == null)
                    continue;
                List<HerdAnimal> ofKind = herd.Where(a => a.Kind == kind.Prefab).ToList();
                int escaped = ofKind.Count(a => a.Escaped);
                if (escaped > 0)
                    plan.Why[order.Item] = $"$vfh_ranch_escaped|{kind.Prefab}|{escaped}";
                else if (BreedingProblem(kind, ofKind, kindsWithFood) is string why)
                    plan.Why[order.Item] = why;
            }
            return plan;
        }

        private static string Num(float f) => Math.Round(f).ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Why a kind isn't breeding (null: nothing in the way), in vanilla's own order of checks.
        private static string? BreedingProblem(AnimalKind kind, List<HerdAnimal> all, ICollection<string> kindsWithFood)
        {
            List<HerdAnimal> adults = all.Where(a => a.Adult).ToList();
            if (adults.Count < 2)
                return $"$vfh_ranch_need_pair|{kind.Prefab}";
            if (adults.Any(a => a.Hungry))
                return kindsWithFood.Contains(kind.Prefab) ? $"$vfh_ranch_hungry|{kind.Prefab}" : $"$vfh_ranch_no_feed|{kind.Prefab}";
            static float Dist(HerdAnimal a, HerdAnimal b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
            if (!adults.Any(a => adults.Any(b => b != a && Dist(a, b) <= kind.PartnerCheckRange)))
                return $"$vfh_ranch_no_partner|{kind.Prefab}|{Num(kind.PartnerCheckRange)}";
            if (adults.All(a => all.Count(b => Dist(a, b) <= kind.TotalCheckRange) >= kind.MaxCreatures))
                return $"$vfh_ranch_crowded|{kind.Prefab}|{kind.MaxCreatures}|{Num(kind.TotalCheckRange)}";
            return null;
        }
    }
}
