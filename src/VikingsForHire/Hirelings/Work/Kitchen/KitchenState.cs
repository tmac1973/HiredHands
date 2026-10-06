using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core.Orders;
using VikingsForHire.Hirelings.Work.Chores;
using VikingsForHire.Hirelings.Work.Farm;

namespace VikingsForHire.Hirelings.Work.Kitchen
{
    /// <summary>
    /// The Cook's next task (recomputed at most every 5 s), a pinned follow-up for chained tasks (dough, then the oven),
    /// and the stoves it has food cooking on, so it stays near them until the food is off.
    /// </summary>
    internal static class KitchenState
    {
        private const float RecomputeSeconds = 5f;
        public const float StayNear = 10f;
        private static readonly Dictionary<string, (float At, KitchenResult Result)> Results = new();
        private static readonly Dictionary<string, (KitchenTask Task, float Until)> Pinned = new();
        private static readonly Dictionary<string, HashSet<CookingStation>> Cooking = new();

        /// <summary>Kitchen station prefabs within the worker's radius.</summary>
        public static HashSet<string> Stations(WorkContext ctx)
        {
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            return new HashSet<string>(pieces.Where(p => p != null && (p.GetComponent<CookingStation>() != null ||
                                                                      KitchenCatalog.CraftStations.Contains(Utils.GetPrefabName(p.gameObject))))
                .Select(p => Utils.GetPrefabName(p.gameObject)));
        }

        /// <summary>What the farm keeps from the Cook (the seed reserve and what's planted for seed orders).</summary>
        public static Dictionary<string, int> Protected(HiringBoard board) => FarmState.For(board).Protected();

        public static KitchenResult Next(HiringBoard board, WorkContext ctx)
        {
            string hid = ctx.Hireling.Hid;
            if (Pinned.TryGetValue(hid, out var pin) && Time.time < pin.Until)
                return new KitchenResult { Task = pin.Task };
            Pinned.Remove(hid);
            string key = board.Id + "|" + hid;
            if (Results.TryGetValue(key, out var hit) && Time.time - hit.At < RecomputeSeconds)
                return hit.Result;
            Stock stock = BoardOrders.Stock(board);
            KitchenResult r = KitchenPlanner.Next(KitchenCatalog.All, BoardOrders.For(board), stock.Chests, Protected(board), ctx.Level, Stations(ctx));
            Results[key] = (Time.time, r);
            return r;
        }

        /// <summary>A chained task's follow-up stays the Cook's next task until done or 120 s pass.</summary>
        public static void Pin(string hid, KitchenTask task) => Pinned[hid] = (task, Time.time + 120f);

        public static void Unpin(string hid) => Pinned.Remove(hid);

        public static void Forget(HiringBoard board, string hid) => Results.Remove(board.Id + "|" + hid);

        public static void Loaded(string hid, CookingStation station)
        {
            if (!Cooking.TryGetValue(hid, out var set))
                Cooking[hid] = set = new HashSet<CookingStation>();
            set.Add(station);
        }

        /// <summary>Where this Cook has food cooking (a stove it loaded with something not done yet), or null.</summary>
        public static Vector3? Busy(string hid)
        {
            if (!Cooking.TryGetValue(hid, out var set))
                return null;
            set.RemoveWhere(s => s == null || s.m_nview == null || !s.m_nview.IsValid() || !HasSlot(s, CookingStation.Status.NotDone));
            return set.Count == 0 ? null : set.First().transform.position;
        }

        public static bool HasSlot(CookingStation s, CookingStation.Status status)
        {
            for (int i = 0; i < s.m_slots.Length; i++)
            {
                s.GetSlot(i, out string item, out _, out CookingStation.Status st, out _);
                if (item.Length > 0 && st == status)
                    return true;
            }
            return false;
        }

        public static int FreeSlots(CookingStation s)
        {
            int n = 0;
            for (int i = 0; i < s.m_slots.Length; i++)
            {
                s.GetSlot(i, out string item, out _, out _, out _);
                if (item.Length == 0)
                    n++;
            }
            return n;
        }

        public static bool Heated(CookingStation s) =>
            (!s.m_requireFire || s.IsFireLit()) && (!s.m_useFuel || s.GetFuel() > 0f);
    }
}
