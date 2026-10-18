using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Orders;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>
    /// A board's current farm plan (what to plant, which bushes to pick, the seed reserve), recomputed at most every 10 s
    /// and shared by the Farmer's chores and the Cook (which keeps off the seed reserve).
    /// </summary>
    internal static class FarmState
    {
        private const float RecomputeSeconds = 10f;
        private static readonly Dictionary<string, (float At, FarmPlan Plan)> Plans = new();

        /// <summary>Free planting spots per sapling for a board (phase 07's field grid); empty: plan no planting.</summary>
        public static System.Func<HiringBoard, Dictionary<string, int>>? FreeSpots { get; set; }

        public static FarmPlan For(HiringBoard board)
        {
            if (Plans.TryGetValue(board.Id, out var hit) && Time.time - hit.At < RecomputeSeconds)
                return hit.Plan;
            Stock stock = BoardOrders.Stock(board);
            int level = BoardOrders.WorkerLevel(board, JobType.Farmer);
            Dictionary<string, int> spots = FreeSpots?.Invoke(board) ?? new Dictionary<string, int>();
            // What the Farmer carries (seeds fetched, a harvest not yet put away) counts as stock too.
            var have = new Dictionary<string, int>(stock.Chests);
            foreach (Hireling h in Hireling.Loaded.Where(h => h != null && h.Job == JobType.Farmer && h.BoardId == board.Id && h.CargoInventory != null))
                foreach (ItemDrop.ItemData i in h.CargoInventory!.GetAllItems().Where(i => i.m_dropPrefab != null))
                    have[i.m_dropPrefab.name] = (have.TryGetValue(i.m_dropPrefab.name, out int n) ? n : 0) + i.m_stack;
            // What's in the board's pile counts like what's growing: toward the target, never as seed to plant.
            var coming = new Dictionary<string, int>(stock.Growing);
            foreach (KeyValuePair<string, int> kv in stock.Piled)
                coming[kv.Key] = (coming.TryGetValue(kv.Key, out int g) ? g : 0) + kv.Value;
            OrderList orders = BoardOrders.For(board);
            FarmPlan plan = FarmPlanner.Plan(CropCatalog.Infos, orders, have, coming, level, spots);
            Plans[board.Id] = (Time.time, plan);
            Log(board, orders, have, coming, spots, plan);
            return plan;
        }

        // The plan and what it was made from, when it changes (Debug, Work): why a farm stands idle can't be read otherwise.
        private static readonly Dictionary<string, string> Logged = new();

        private static void Log(HiringBoard board, OrderList orders, Dictionary<string, int> have, Dictionary<string, int> coming,
            Dictionary<string, int> spots, FarmPlan plan)
        {
            int Get(Dictionary<string, int> d, string k) => d.TryGetValue(k, out int v) ? v : 0;
            var items = new HashSet<string>();
            foreach (ProductionOrder o in orders.Active().Where(o => o.IsFarm))
            {
                items.Add(o.Item);
                foreach (CropInfo c in CropCatalog.Infos.Where(c => c.YieldOf(o.Item) > 0 && c.Consumes.Length > 0))
                    items.Add(c.Consumes);
            }
            string orderText = string.Join(" ", orders.Active().Where(o => o.IsFarm).Select(o => $"{o.Item}:{o.Kind}:{o.Target}"));
            string stockText = string.Join(" ", items.OrderBy(i => i).Select(i => $"{i}={Get(have, i)}+{Get(coming, i)}"));
            string spotText = string.Join(" ", spots.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}={kv.Value}"));
            string planText = string.Join(" ", plan.Plant.Select(p => $"{p.Crop.Plant}x{p.Count}"));
            string sig = $"{orderText}|{stockText}|{spotText}|{planText}|{string.Join(",", plan.Why.Values)}";
            if (Logged.TryGetValue(board.Id, out string was) && was == sig)
                return;
            Logged[board.Id] = sig;
            VfhLog.D(LogCat.Work, "farm.plan", ("board", board.Id), ("orders", orderText), ("stock", stockText), ("spots", spotText), ("plant", planText),
                ("spares", string.Join(" ", plan.Spares.Select(kv => $"{kv.Key}={kv.Value}"))), ("bootstrap", string.Join(" ", plan.Bootstrap)),
                ("why", string.Join(" ", plan.Why.Select(kv => $"{kv.Key}={kv.Value}"))));
        }

        /// <summary>Forget a board's plan (after planting or picking, so the next survey sees the change).</summary>
        public static void Invalidate(HiringBoard board)
        {
            Plans.Remove(board.Id);
            StockCounter.Forget();
        }
    }
}
