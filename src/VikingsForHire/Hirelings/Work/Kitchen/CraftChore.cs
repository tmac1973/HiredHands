using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Orders;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Kitchen
{
    /// <summary>
    /// The Cook at the cauldron, food preparation table and mead ketill: the kitchen's next crafting task, made like a
    /// player would (the station at the recipe's level, fire under it or a roof over it as it needs), from ingredients in
    /// the chests (never the farm's protected seed stock). A chained intermediate (dough) is carried straight on to its
    /// follow-up (the oven) instead of being put away.
    /// </summary>
    internal sealed class CraftChore : IChore
    {
        private const float CraftSeconds = 2.5f;
        private const float StuckSeconds = 20f;
        private const float ChestSkipSeconds = 60f;
        private const int MaxChests = 4;

        private readonly WorkSteps _walk = new();
        private CraftingStation? _station;
        private KitchenTask? _task;
        private int _batches;
        private int _made;
        private Dictionary<string, int> _need = new();
        private Container? _chest;
        private int _chestsVisited;
        private float _craftAt;
        private WorkContext? _ctx;

        public ChoreKind Kind => ChoreKind.Craft;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        /// <summary>Items Cooks have crafted since login (for the tests).</summary>
        public static readonly Dictionary<string, int> Crafted = new();

        /// <summary>Why a station can't be used for this recipe now (activity text), or null.</summary>
        private static string? Unusable(CraftingStation s, KitchenInfo info)
        {
            if (s.GetLevel() < info.StationLevelNeeded)
                return ActivityText.Make("$vfh_need_station_level", WorkSteps.SharedName(info.Output), info.StationLevelNeeded.ToString(), s.m_name);
            return KitchenState.CraftBlocked(s);
        }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            HiringBoard? board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            if (board == null)
                return Enumerable.Empty<ChoreJob>();
            KitchenResult r = KitchenState.Next(board, ctx);
            if (r.Task is not KitchenTask t || t.Info.Kind != StationKind.Craft)
                return Enumerable.Empty<ChoreJob>();
            Vector3? busy = KitchenState.Busy(ctx.Hireling.Hid);
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            List<CraftingStation> stations = pieces.Where(p => p != null && Utils.GetPrefabName(p.gameObject) == t.Info.Station)
                .Select(p => p.GetComponentInChildren<CraftingStation>()).Where(s => s != null && ctx.BoardOwnerMayUse(s.transform.position))
                .OrderBy(s => Vector3.Distance(ctx.Position, s.transform.position)).ToList();
            CraftingStation? usable = null;
            foreach (CraftingStation s in stations)
            {
                if (Unusable(s, t.Info) is string why)
                {
                    Missing ??= why;
                    continue;
                }
                usable = s;
                break;
            }
            // Food on the stoves: only crafting close enough to keep an eye on it.
            if (usable == null || busy != null && Vector3.Distance(usable.transform.position, busy.Value) > KitchenState.StayNear)
                return Enumerable.Empty<ChoreJob>();
            if (t.Then == null && StorageRoom.NoRoom(ctx.AllChests, t.Info.Output))
            {
                Missing = ActivityText.Make("$vfh_status_storage_full", WorkSteps.SharedName(t.Info.Output));
                return Enumerable.Empty<ChoreJob>();
            }
            const float u = 0.5f;
            return new[]
            {
                new ChoreJob
                {
                    Kind = Kind, Target = usable, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, usable.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_cook_craft", WorkSteps.SharedName(t.Info.Output), usable.m_name),
                },
            };
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            RestAfter = 0f;
            _walk.Reset();
            _ctx = ctx;
            _station = (CraftingStation)job.Target;
            _made = 0;
            _chestsVisited = 0;
            HiringBoard? board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            _task = board != null ? KitchenState.Next(board, ctx).Task : null;
            if (_task == null || _task.Info.Kind != StationKind.Craft)
            {
                _batches = 0;
                return;
            }
            ctx.Hireling.FetchingSupplies = true;
            // As many batches as the cargo can hold the ingredients (and the result) for.
            _batches = _task.Batches;
            while (_batches > 1 && SlotsFor(_task.Info, _batches, ctx) > ctx.FreeSlots)
                _batches--;
            _need = _task.Info.Inputs.ToDictionary(kv => kv.Key, kv => kv.Value * _batches);
            _chest = NextChest(ctx);
        }

        private static int SlotsFor(KitchenInfo info, int batches, WorkContext ctx) =>
            info.Inputs.Sum(kv => Mathf.CeilToInt(Mathf.Max(0, kv.Value * batches - (ctx.Carried.TryGetValue(kv.Key, out int c) ? c : 0)) / (float)Mathf.Max(1, WorkSteps.MaxStack(kv.Key))))
            + Mathf.CeilToInt(info.OutputAmount * batches / (float)Mathf.Max(1, WorkSteps.MaxStack(info.Output)));

        // The nearest chest with any ingredient still short, a few chests a trip.
        private Container? NextChest(WorkContext ctx)
        {
            if (_chestsVisited >= MaxChests)
                return null;
            List<string> shortOf = _need.Where(kv => Carried(ctx.Hireling, kv.Key) < kv.Value).Select(kv => kv.Key).ToList();
            return shortOf.Count == 0 ? null : ctx.NearestChestWith(shortOf);
        }

        private static int Carried(Hireling h, string item) => h.CargoInventory!.CountItems(WorkSteps.SharedName(item));

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            CraftingStation? s = _station;
            KitchenTask? task = _task;
            if (s == null || task == null || _batches <= 0 || _ctx == null)
                return End(h, 3f);
            if (_walk.SinceProgress > StuckSeconds)
            {
                if (_chest != null)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                return End(h, 5f, ChoreProgress.Failed);
            }
            if (_chest != null)
            {
                if (!_walk.Approach(ai, dt, _chest, _chest.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                var want = _need.ToDictionary(kv => kv.Key, kv => Mathf.Max(0, kv.Value - Carried(h, kv.Key))).Where(kv => kv.Value > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                if (WorkSteps.TakeFromChest(_chest, h, want, Config.VfhConfig.ChestReserve) == 0)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                _chestsVisited++;
                _chest = NextChest(_ctx);
                _walk.Reset();
                return ChoreProgress.Running;
            }
            if (!_walk.Approach(ai, dt, s, s.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(s.transform.position);
            if (_made >= _batches || task.Info.Inputs.Any(kv => Carried(h, kv.Key) < kv.Value))
                return End(h, 1f);
            if (_craftAt <= 0f)
            {
                _craftAt = Time.time + CraftSeconds; // a moment at the station, as a player crafting
                return ChoreProgress.Running;
            }
            if (Time.time < _craftAt)
                return ChoreProgress.Running;
            _craftAt = 0f;
            CraftOne(h, s, task.Info);
            _made++;
            return ChoreProgress.Running;
        }

        // As InventoryGui.DoCrafting, without a player: the ingredients out of cargo, the result in.
        private static void CraftOne(Hireling h, CraftingStation s, KitchenInfo info)
        {
            foreach (KeyValuePair<string, int> kv in info.Inputs)
                h.CargoInventory!.RemoveItem(WorkSteps.SharedName(kv.Key), kv.Value);
            GameObject? output = ObjectDB.instance.GetItemPrefab(info.Output);
            if (output != null)
                WorkSteps.AddToCargo(h, output, info.OutputAmount);
            s.m_craftItemEffects.Create(s.transform.position, Quaternion.identity);
            Crafted[info.Output] = (Crafted.TryGetValue(info.Output, out int n) ? n : 0) + info.OutputAmount;
            VfhLog.I(LogCat.Work, "cook.craft", ("hid", h.Hid), ("item", info.Output), ("station", Utils.GetPrefabName(s.gameObject)), ("level", s.GetLevel()));
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            h.FetchingSupplies = false;
            if (_made == 0 && result == ChoreProgress.Done)
                result = ChoreProgress.Failed; // nothing made: count it, so a job that can't be done gets set aside
            if (_made > 0 && _task != null)
            {
                if (_task.Then != null)
                    KitchenState.Pin(h.Hid, _task.Then); // carry the dough straight to the oven
                else
                {
                    ChoreDeliveryPolicy.ChoreOutputs.Add(_task.Info.Output);
                    if (h.Zdo != null)
                        h.Zdo.Set(HirelingZdo.DeliverPending, true);
                }
            }
            if (BoardOrders.BoardOf(h.BoardId) is HiringBoard board)
                KitchenState.Forget(board, h.Hid);
            _station = null;
            _task = null;
            _chest = null;
            _craftAt = 0f;
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h)
        {
            if (_station != null)
                End(h, 0f);
        }
    }
}
