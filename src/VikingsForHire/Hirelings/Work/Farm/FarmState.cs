using System.Collections.Generic;
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
            FarmPlan plan = FarmPlanner.Plan(CropCatalog.Infos, BoardOrders.For(board), stock.Chests, stock.Growing, level, spots);
            Plans[board.Id] = (Time.time, plan);
            return plan;
        }

        /// <summary>Forget a board's plan (after planting or picking, so the next survey sees the change).</summary>
        public static void Invalidate(HiringBoard board) => Plans.Remove(board.Id);
    }
}
