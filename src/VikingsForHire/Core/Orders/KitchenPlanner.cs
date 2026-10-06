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
    }

    /// <summary>
    /// The Cook's next job from the kitchen orders, top to bottom: the first short order it can make (stoves before
    /// crafting when both can), with one step of chaining (dough for bread). Protected items (the seed reserve and what the
    /// Farmer plants for seeds) are never used.
    /// </summary>
    public static class KitchenPlanner
    {
        public static KitchenResult Next(IReadOnlyList<KitchenInfo> infos, OrderList orders, IReadOnlyDictionary<string, int> stock,
            IReadOnlyDictionary<string, int> protectedItems, int level, ICollection<string> stations)
        {
            var result = new KitchenResult();
            int Stock(string item) => stock.TryGetValue(item, out int n) ? n : 0;
            int Avail(string item) => Math.Max(0, Stock(item) - (protectedItems.TryGetValue(item, out int p) ? p : 0));

            foreach (ProductionOrder order in orders.Active().Where(o => o.Kind == OrderKind.Kitchen))
            {
                int shortBy = order.Target - Stock(order.Item);
                if (shortBy <= 0)
                    continue;
                List<KitchenInfo> makesIt = infos.Where(i => i.Output == order.Item).ToList();
                List<KitchenInfo> makers = makesIt.Where(i => i.Level <= level && stations.Contains(i.Station))
                    .OrderBy(i => i.Kind == StationKind.Stove ? 0 : 1).ToList();
                if (makers.Count == 0)
                {
                    result.Missing ??= makesIt.Count == 0 ? null
                        : makesIt.All(i => i.Level > level) ? $"$vfh_need_cook_level|{order.Item}|{makesIt.Min(i => i.Level)}"
                        : $"$vfh_need_kitchen|{order.Item}";
                    continue;
                }
                foreach (KitchenInfo maker in makers)
                {
                    int needed = (int)Math.Ceiling(shortBy / (double)Math.Max(1, maker.OutputAmount));
                    int possible = Possible(maker, Avail);
                    if (possible > 0)
                    {
                        result.Task = new KitchenTask { Info = maker, Batches = Math.Min(needed, possible), ForOrder = order.Item };
                        return result;
                    }
                    // One step of chaining: make the first missing input first, if its own inputs are there.
                    KeyValuePair<string, int> missing = maker.Inputs.First(kv => Avail(kv.Key) < kv.Value);
                    int want = missing.Value * needed - Avail(missing.Key);
                    KitchenInfo? sub = infos.Where(i => i.Output == missing.Key && i.Level <= level && stations.Contains(i.Station) && Possible(i, Avail) > 0)
                        .OrderBy(i => i.Kind == StationKind.Stove ? 0 : 1).FirstOrDefault();
                    if (sub != null)
                    {
                        int subBatches = Math.Min(Possible(sub, Avail), (int)Math.Ceiling(want / (double)Math.Max(1, sub.OutputAmount)));
                        int made = subBatches * sub.OutputAmount;
                        int thenBatches = Math.Min(needed, (Avail(missing.Key) + made) / Math.Max(1, missing.Value));
                        result.Task = new KitchenTask
                        {
                            Info = sub, Batches = subBatches, ForOrder = order.Item,
                            Then = thenBatches > 0 ? new KitchenTask { Info = maker, Batches = thenBatches, ForOrder = order.Item } : null,
                        };
                        return result;
                    }
                    result.Missing ??= $"$vfh_need_ingredient|{order.Item}|{missing.Value - Avail(missing.Key)}|{missing.Key}";
                }
            }
            return result;
        }

        // How many batches the inputs on hand allow.
        private static int Possible(KitchenInfo info, Func<string, int> avail) =>
            info.Inputs.Count == 0 ? 0 : info.Inputs.Min(kv => avail(kv.Key) / Math.Max(1, kv.Value));
    }
}
