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
    /// The Cook at the spits and the oven: takes food off as soon as it's done (or burnt, as coal), keeps the oven fuelled,
    /// and loads raw food for short kitchen orders. While its own food is cooking it only takes jobs near those stoves.
    /// </summary>
    internal sealed class StovesChore : IChore
    {
        private const int VariantTakeOff = 0, VariantFuel = 1, VariantLoad = 2;
        private const float StuckSeconds = 20f;
        private const float ItemEvery = 0.3f;
        private const float ChestSkipSeconds = 60f;

        private readonly WorkSteps _walk = new();
        private CookingStation? _station;
        private int _variant;
        private string _item = "";
        private int _count;
        private int _done;
        private Container? _chest;
        private bool _fetching;
        private float _nextAt;
        private float _waitPickup;
        private KitchenTask? _task;

        public ChoreKind Kind => ChoreKind.Stoves;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        /// <summary>Items Cooks have taken off stoves since login, by item (for the tests).</summary>
        public static readonly Dictionary<string, int> TakenOff = new();

        private static List<CookingStation> Stoves(WorkContext ctx)
        {
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            return pieces.Where(p => p != null).Select(p => p.GetComponentInChildren<CookingStation>())
                .Where(s => s != null && s.m_nview != null && s.m_nview.IsValid() && ctx.BoardOwnerMayUse(s.transform.position)).ToList();
        }

        private static HashSet<string> Outputs(CookingStation s)
        {
            var set = new HashSet<string>(s.m_conversion.Where(c => c?.m_to != null).Select(c => c.m_to.name));
            if (s.m_overCookedItem != null)
                set.Add(s.m_overCookedItem.name);
            return set;
        }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            var jobs = new List<ChoreJob>();
            HiringBoard? board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            if (board == null)
                return jobs;
            Vector3? busy = KitchenState.Busy(ctx.Hireling.Hid);
            bool Near(Component c) => busy == null || Vector3.Distance(c.transform.position, busy.Value) <= KitchenState.StayNear;
            List<CookingStation> stoves = Stoves(ctx);

            // Done or burnt food first, wherever it is in range.
            foreach (CookingStation s in stoves.Where(s => Near(s) && (KitchenState.HasSlot(s, CookingStation.Status.Done) || KitchenState.HasSlot(s, CookingStation.Status.Burnt))))
                jobs.Add(Job(s, 1.0f, VariantTakeOff, ctx, "$vfh_cook_takeoff"));

            // Ovens below half fuel, with fuel in the chests.
            foreach (CookingStation s in stoves.Where(s => s.m_useFuel && s.m_fuelItem != null && s.GetFuel() < s.m_maxFuel / 2f && Near(s)))
                if (ctx.Available(s.m_fuelItem.name) + (ctx.Carried.TryGetValue(s.m_fuelItem.name, out int c) ? c : 0) > 0)
                    jobs.Add(Job(s, 0.7f, VariantFuel, ctx, "$vfh_cook_fuel"));

            // Load raw food for the kitchen's next stove task.
            KitchenResult r = KitchenState.Next(board, ctx);
            if (r.Task is KitchenTask t && t.Info.Kind == StationKind.Stove)
            {
                CookingStation? free = stoves.Where(s => Utils.GetPrefabName(s.gameObject) == t.Info.Station && KitchenState.FreeSlots(s) > 0 && Near(s))
                    .OrderBy(s => Vector3.Distance(ctx.Position, s.transform.position)).FirstOrDefault(s => KitchenState.Heated(s));
                if (free != null)
                {
                    if (!StorageRoom.NoRoom(ctx.AllChests, t.Info.Output))
                        jobs.Add(Job(free, 0.6f, VariantLoad, ctx, "$vfh_cook_load", WorkSteps.SharedName(t.Info.Output)));
                    else
                        Missing ??= ActivityText.Make("$vfh_status_storage_full", WorkSteps.SharedName(t.Info.Output));
                }
                else if (stoves.Any(s => Utils.GetPrefabName(s.gameObject) == t.Info.Station && KitchenState.FreeSlots(s) > 0))
                    Missing ??= ActivityText.Make("$vfh_need_fire", stoves.First(s => Utils.GetPrefabName(s.gameObject) == t.Info.Station).m_name);
            }
            else if (r.Task == null && r.Missing != null)
                Missing ??= r.Missing;
            return jobs;
        }

        private ChoreJob Job(CookingStation s, float u, int variant, WorkContext ctx, string label, string? arg = null) => new()
        {
            Kind = Kind, Target = s, Urgency = u, Variant = variant,
            Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, s.transform.position), ctx.Radius),
            Label = ActivityText.Make(label, arg ?? s.m_name),
        };

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            RestAfter = 0f;
            _walk.Reset();
            _station = (CookingStation)job.Target;
            _variant = job.Variant;
            _done = 0;
            _chest = null;
            _fetching = false;
            _waitPickup = 0f;
            _task = null;
            Hireling h = ctx.Hireling;
            if (_variant == VariantTakeOff)
            {
                h.HoldDeliveries = true;
                return;
            }
            if (_variant == VariantFuel)
            {
                _item = _station.m_fuelItem.name;
                _count = Mathf.Max(0, _station.m_maxFuel - 1 - Mathf.CeilToInt(_station.GetFuel()));
            }
            else
            {
                HiringBoard? board = BoardOrders.BoardOf(h.BoardId);
                _task = board != null ? KitchenState.Next(board, ctx).Task : null;
                if (_task == null || _task.Info.Kind != StationKind.Stove)
                {
                    _count = 0;
                    return;
                }
                _item = _task.Info.Inputs.Keys.First();
                _count = Mathf.Min(_task.Batches, KitchenState.FreeSlots(_station));
            }
            h.FetchingSupplies = true;
            int carried = ctx.Carried.TryGetValue(_item, out int c) ? c : 0;
            int need = Mathf.Min(_count - carried, ctx.Available(_item));
            if (need > 0)
            {
                _chest = ctx.NearestChestWith(new[] { _item });
                _fetching = _chest != null;
                _fetchAmount = need;
            }
            _count = Mathf.Min(_count, carried + Mathf.Max(0, need));
        }

        private int _fetchAmount;

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            CookingStation? s = _station;
            if (s == null || s.m_nview == null || !s.m_nview.IsValid())
                return End(h, 1f);
            if (_walk.SinceProgress > StuckSeconds)
            {
                if (_fetching && _chest != null)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                VfhLog.D(LogCat.Work, "cook.stuck", ("hid", h.Hid), ("station", s.name), ("fetching", _fetching));
                return End(h, 5f, ChoreProgress.Failed);
            }
            if (_fetching)
            {
                Container? chest = _chest;
                if (chest == null)
                    return End(h, 1f);
                if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                if (WorkSteps.TakeFromChest(chest, h, new Dictionary<string, int> { [_item] = _fetchAmount }, Config.VfhConfig.ChestReserve) == 0)
                    Reservations.Skip(chest, ChestSkipSeconds);
                _fetching = false;
                _walk.Reset();
                return ChoreProgress.Running;
            }
            if (!_walk.Approach(ai, dt, s, s.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(s.transform.position);
            if (Time.time < _nextAt)
                return ChoreProgress.Running;
            _nextAt = Time.time + ItemEvery;
            switch (_variant)
            {
                case VariantTakeOff:
                    if (KitchenState.HasSlot(s, CookingStation.Status.Done) || KitchenState.HasSlot(s, CookingStation.Status.Burnt))
                    {
                        if (!s.m_nview.IsOwner())
                            s.m_nview.ClaimOwnership();
                        s.m_nview.InvokeRPC("RPC_RemoveDoneItem", ai.transform.position, 1); // drops it at the station
                        _waitPickup = Time.time + 1f;
                        return ChoreProgress.Running;
                    }
                    if (Time.time < _waitPickup)
                        return ChoreProgress.Running;
                    HashSet<string> outputs = Outputs(s);
                    int picked = WorkSteps.PickUpDrops(h, s.transform.position, outputs, 3f);
                    foreach (string o in outputs)
                        ChoreDeliveryPolicy.ChoreOutputs.Add(o);
                    TakenOff["any"] = (TakenOff.TryGetValue("any", out int t) ? t : 0) + picked;
                    if (picked > 0 && h.Zdo != null)
                        h.Zdo.Set(HirelingZdo.DeliverPending, true);
                    VfhLog.D(LogCat.Work, "cook.takeoff", ("hid", h.Hid), ("station", Utils.GetPrefabName(s.gameObject)), ("picked", picked));
                    return End(h, 0.5f, picked > 0 ? ChoreProgress.Done : ChoreProgress.Failed);
                case VariantFuel:
                {
                    string shared = WorkSteps.SharedName(_item);
                    if (_done >= _count || h.CargoInventory!.CountItems(shared) <= 0 || s.GetFuel() >= s.m_maxFuel - 1)
                        return End(h, 0.5f, _done > 0 ? ChoreProgress.Done : ChoreProgress.Failed);
                    h.CargoInventory.RemoveItem(shared, 1);
                    s.m_nview.InvokeRPC("RPC_AddFuel");
                    _done++;
                    return ChoreProgress.Running;
                }
                default:
                {
                    string shared = WorkSteps.SharedName(_item);
                    if (!s.m_nview.IsOwner())
                        s.m_nview.ClaimOwnership(); // so the slot fills here and now, and the free-slot check below is current
                    if (_done >= _count || h.CargoInventory!.CountItems(shared) <= 0 || !KitchenState.Heated(s) || KitchenState.FreeSlots(s) <= 0)
                    {
                        KitchenState.Unpin(h.Hid); // a pinned follow-up (dough into the oven) is done now
                        return End(h, 0.5f, _done > 0 ? ChoreProgress.Done : ChoreProgress.Failed);
                    }
                    h.CargoInventory.RemoveItem(shared, 1);
                    s.m_nview.InvokeRPC("RPC_AddItem", _item, false);
                    KitchenState.Loaded(h.Hid, s);
                    _done++;
                    VfhLog.D(LogCat.Work, "cook.load", ("hid", h.Hid), ("station", Utils.GetPrefabName(s.gameObject)), ("item", _item));
                    return ChoreProgress.Running;
                }
            }
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            h.HoldDeliveries = false;
            h.FetchingSupplies = false;
            if (BoardOrders.BoardOf(h.BoardId) is HiringBoard board)
                KitchenState.Forget(board, h.Hid);
            _station = null;
            _chest = null;
            _fetching = false;
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
