using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Orders;

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
            FarmPlan plan = FarmPlanner.Plan(CropCatalog.Infos, BoardOrders.For(board), have, stock.Growing, level, spots);
            Plans[board.Id] = (Time.time, plan);
            return plan;
        }

        /// <summary>Forget a board's plan (after planting or picking, so the next survey sees the change).</summary>
        public static void Invalidate(HiringBoard board)
        {
            Plans.Remove(board.Id);
            StockCounter.Forget();
        }
    }
}
