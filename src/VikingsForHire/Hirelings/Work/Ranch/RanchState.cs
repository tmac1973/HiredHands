using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Orders;
using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>
    /// A board's ranch plan (what to slaughter, which eggs to collect, why each ranch order isn't met), recomputed at most
    /// every 10 s from the herd in the Rancher's area, the chests and the board's orders. The Rancher's chores and his
    /// Problems list read it; a board without a Rancher loaded here uses the board's spot and a 40 m area.
    /// </summary>
    internal static class RanchState
    {
        private const float RecomputeSeconds = 10f;
        private const float NoRancherRadius = 40f;
        private static readonly Dictionary<string, (float At, RanchPlan Plan, List<HerdScan.Found> Herd)> Plans = new();

        public static RanchPlan For(HiringBoard board) => Get(board).Plan;

        /// <summary>The herd behind the latest plan (the animals themselves, for the chores).</summary>
        public static List<HerdScan.Found> Herd(HiringBoard board) => Get(board).Herd;

        public static void Invalidate(HiringBoard board)
        {
            Plans.Remove(board.Id);
            StockCounter.Forget();
        }

        /// <summary>The Rancher of a board loaded on this game, or null.</summary>
        public static Hireling? Rancher(HiringBoard board) =>
            Hireling.Loaded.FirstOrDefault(h => h != null && h.Job == JobType.Rancher && h.BoardId == board.Id && h.Zdo != null);

        /// <summary>Where the herd is looked for: the Rancher's area, else around the board.</summary>
        public static (Vector3 Centre, float Radius) Area(HiringBoard board, out Hireling? rancher)
        {
            rancher = Rancher(board);
            return rancher != null ? (rancher.Home, rancher.Radius) : (board.transform.position, NoRancherRadius);
        }

        private static (float At, RanchPlan Plan, List<HerdScan.Found> Herd) Get(HiringBoard board)
        {
            if (Plans.TryGetValue(board.Id, out var hit) && Time.time - hit.At < RecomputeSeconds)
                return hit;
            (Vector3 centre, float radius) = Area(board, out Hireling? rancher);
            WorkContext? ctx = rancher != null ? new WorkContext(rancher) : null;
            List<HerdScan.Found> herd = HerdScan.Scan(centre, radius, p => ctx == null || ctx.BoardOwnerMayUse(p));
            Stock stock = BoardOrders.Stock(board);
            var have = new Dictionary<string, int>(stock.Chests);
            if (rancher?.CargoInventory != null)
                foreach (ItemDrop.ItemData i in rancher.CargoInventory.GetAllItems().Where(i => i.m_dropPrefab != null))
                    have[i.m_dropPrefab.name] = (have.TryGetValue(i.m_dropPrefab.name, out int n) ? n : 0) + i.m_stack;
            var fed = new HashSet<string>(AnimalCatalog.Kinds.Where(k => k.Foods.Any(f => have.TryGetValue(f, out int n) && n > 0)).Select(k => k.Prefab));
            RanchPlan plan = RanchPlanner.Plan(AnimalCatalog.Kinds, herd.Select(f => f.Model).ToList(), BoardOrders.For(board), have, fed);
            var entry = (Time.time, plan, herd);
            Plans[board.Id] = entry;
            return entry;
        }
    }
}
