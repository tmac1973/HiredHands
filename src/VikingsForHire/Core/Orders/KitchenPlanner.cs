using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Orders
{
    public sealed class KitchenResult
    {
        public KitchenTask? Task { get; set; }

        /// <summary>Why the first short order can't be worked, as activity text ("token|arg|arg").</summary>
        public string? Missing { get; set; }

        /// <summary>Order item -> why that short order can't be worked now (activity text); ones that can be aren't here.</summary>
        public Dictionary<string, string> Why { get; } = new();
    }

    /// <summary>
    /// The Cook's next job from the kitchen orders, top to bottom: the first short order it can make (stoves before
    /// crafting when both can), with one step of chaining (dough for bread). Protected items (the seed reserve and what the
    /// Farmer plants for seeds) are never used.
    /// </summary>
    public static class KitchenPlanner
    {
        public static KitchenResult Next(IReadOnlyList<KitchenInfo> infos, OrderList orders, IReadOnlyDictionary<string, int> stock,
            IReadOnlyDictionary<string, int> protectedItems, int level, IReadOnlyDictionary<string, int> stations,
            IReadOnlyDictionary<string, int>? madeElsewhere = null)
        {
            var result = new KitchenResult();
            int Stock(string item) => stock.TryGetValue(item, out int n) ? n : 0;
            // Made but out of reach (the board's pile): counts toward an order, never as an ingredient.
            int Have(string item) => Stock(item) + (madeElsewhere != null && madeElsewhere.TryGetValue(item, out int m) ? m : 0);
            int Avail(string item) => Math.Max(0, Stock(item) - (protectedItems.TryGetValue(item, out int p) ? p : 0));

            foreach (ProductionOrder order in orders.Active().Where(o => o.Kind == OrderKind.Kitchen))
            {
                int shortBy = order.Target - Have(order.Item);
                if (shortBy <= 0)
                    continue;
                (KitchenTask? task, string? why) = Plan(order, shortBy, infos, level, stations, Avail);
                if (task != null)
                {
                    result.Task ??= task;
                    continue;
                }
                if (why == null)
                    continue;
                result.Why[order.Item] = why;
                // The first reason the hireling shows, as before: only while no earlier order gave it work.
                if (result.Task == null && !why.StartsWith("$vfh_need_recipe"))
                    result.Missing ??= why;
            }
            return result;
        }

        // One short order: a task for it (with one step of chaining: dough for bread), or why there's none.
        private static (KitchenTask? Task, string? Why) Plan(ProductionOrder order, int shortBy, IReadOnlyList<KitchenInfo> infos, int level,
            IReadOnlyDictionary<string, int> stations, Func<string, int> Avail)
        {
            List<KitchenInfo> makesIt = infos.Where(i => i.Output == order.Item).ToList();
            List<KitchenInfo> makers = makesIt.Where(i => i.Level <= level && i.Inputs.Count > 0 && Usable(i, stations))
                .OrderBy(i => i.Kind == StationKind.Stove ? 0 : 1).ToList();
            if (makers.Count == 0)
                return (null, makesIt.Count == 0 ? $"$vfh_need_recipe|{order.Item}"
                    : makesIt.All(i => i.Level > level) ? $"$vfh_need_cook_level|{order.Item}|{makesIt.Min(i => i.Level)}"
                    : $"$vfh_need_kitchen|{order.Item}");
            string? why = null;
            foreach (KitchenInfo maker in makers)
            {
                int needed = (int)Math.Ceiling(shortBy / (double)Math.Max(1, maker.OutputAmount));
                int possible = Possible(maker, Avail);
                if (possible > 0)
                    return (new KitchenTask { Info = maker, Batches = Math.Min(needed, possible), ForOrder = order.Item }, null);
                KeyValuePair<string, int> missing = maker.Inputs.FirstOrDefault(kv => Avail(kv.Key) < kv.Value);
                if (missing.Key == null)
                    continue;
                int want = missing.Value * needed - Avail(missing.Key);
                KitchenInfo? sub = infos.Where(i => i.Output == missing.Key && i.Level <= level && Usable(i, stations) && Possible(i, Avail) > 0)
                    .OrderBy(i => i.Kind == StationKind.Stove ? 0 : 1).FirstOrDefault();
                if (sub != null)
                {
                    int subBatches = Math.Min(Possible(sub, Avail), (int)Math.Ceiling(want / (double)Math.Max(1, sub.OutputAmount)));
                    int made = subBatches * sub.OutputAmount;
                    int thenBatches = Math.Min(needed, (Avail(missing.Key) + made) / Math.Max(1, missing.Value));
                    return (new KitchenTask
                    {
                        Info = sub, Batches = subBatches, ForOrder = order.Item,
                        Then = thenBatches > 0 ? new KitchenTask { Info = maker, Batches = thenBatches, ForOrder = order.Item } : null,
                    }, null);
                }
                why ??= $"$vfh_need_ingredient|{order.Item}|{missing.Value - Avail(missing.Key)}|{missing.Key}";
            }
            return (null, why);
        }

        // A station of the right kind is usable now, at the level the recipe needs.
        private static bool Usable(KitchenInfo info, IReadOnlyDictionary<string, int> stations) =>
            stations.TryGetValue(info.Station, out int stationLevel) && stationLevel >= info.StationLevelNeeded;

        // How many batches the inputs on hand allow.
        private static int Possible(KitchenInfo info, Func<string, int> avail) =>
            info.Inputs.Count == 0 ? 0 : info.Inputs.Min(kv => avail(kv.Key) / Math.Max(1, kv.Value));
    }
}
