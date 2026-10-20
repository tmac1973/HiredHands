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
        private const int VariantTakeOff = 0, VariantFuel = 1, VariantLoad = 2, VariantTend = 3;
        // Watching food cook re-plans this often, so fuel or the next load still get done.
        private const float TendSeconds = 10f;
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

        private const float LooseRadius = 6f;

        private static bool LooseNear(CookingStation s)
        {
            HashSet<string> outputs = Outputs(s);
            return ItemDrop.s_instances.Any(d => d != null && d.m_itemData?.m_dropPrefab != null && outputs.Contains(d.m_itemData.m_dropPrefab.name) &&
                                                 !d.m_itemData.m_customData.ContainsKey(DropPile.Tag) && d.GetTimeSinceSpawned() > 2.0 &&
                                                 Vector3.Distance(d.transform.position, s.transform.position) < LooseRadius);
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
            bool two = CookEnhancements.TwoAtOnce(ctx.Hireling);
            bool Near(Component c) => busy == null || two || Vector3.Distance(c.transform.position, busy.Value) <= KitchenState.StayNear;
            List<CookingStation> stoves = Stoves(ctx);

            // Done or burnt food first, wherever it is in range; and cooked food lying by a stove (taking it off throws it).
            foreach (CookingStation s in stoves.Where(s => Near(s) && (KitchenState.HasSlot(s, CookingStation.Status.Done) || KitchenState.HasSlot(s, CookingStation.Status.Burnt) || LooseNear(s))))
                jobs.Add(Job(s, 1.0f, VariantTakeOff, ctx, "$vfh_cook_takeoff"));

            // Ovens below half fuel, with fuel in the chests.
            foreach (CookingStation s in stoves.Where(s => s.m_useFuel && s.m_fuelItem != null && s.GetFuel() < s.m_maxFuel * CookEnhancements.FuelBelow(ctx.Hireling) && Near(s)))
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
                    if (!StorageRoom.NoRoom(ctx.PutChests, t.Info.Output))
                        jobs.Add(Job(free, 0.6f, VariantLoad, ctx, "$vfh_cook_load", WorkSteps.SharedName(t.Info.Output)));
                    else
                        Missing = ChoreNotes.First(Missing, ActivityText.Make("$vfh_status_storage_full", WorkSteps.SharedName(t.Info.Output)));
                }
                else if (stoves.Any(s => Utils.GetPrefabName(s.gameObject) == t.Info.Station && KitchenState.FreeSlots(s) > 0))
                    Missing = ChoreNotes.First(Missing, ActivityText.Make("$vfh_need_fire", stoves.First(s => Utils.GetPrefabName(s.gameObject) == t.Info.Station).m_name));
            }
            else if (r.Task == null && r.Missing != null)
                Missing ??= r.Missing;

            // Something cooking and nothing else to do: stand by the stove and watch it, rather than wander back to the
            // board and have to run back before it burns.
            foreach (CookingStation s in stoves.Where(s => Near(s) && KitchenState.HasSlot(s, CookingStation.Status.NotDone) && KitchenState.Heated(s)))
                jobs.Add(Job(s, 0.15f, VariantTend, ctx, "$vfh_cook_tend"));
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
            if (_variant == VariantTend)
            {
                h.HoldDeliveries = true; // what it carries can wait: the food can't
                _tendUntil = Time.time + TendSeconds;
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
                // Spit master (an enhancement): every free stove of the same kind in this one trip.
                _more.Clear();
                if (h.Enh.Has(Core.Enhancements.EnhancementId.SpitMaster))
                {
                    string kind = Utils.GetPrefabName(_station.gameObject);
                    _more.AddRange(Stoves(ctx).Where(s => s != _station && Utils.GetPrefabName(s.gameObject) == kind && KitchenState.FreeSlots(s) > 0 && KitchenState.Heated(s))
                        .OrderBy(s => Vector3.Distance(_station.transform.position, s.transform.position)));
                    _count = Mathf.Min(_task.Batches, KitchenState.FreeSlots(_station) + _more.Sum(KitchenState.FreeSlots));
                }
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
        private float _tendUntil;
        private readonly List<CookingStation> _more = new();

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
            _nextAt = Time.time + ItemEvery * CookEnhancements.StepMult(h);
            switch (_variant)
            {
                case VariantTend:
                    // Done (or burning): end now so the take-off job, the most urgent, comes straight away.
                    if (KitchenState.HasSlot(s, CookingStation.Status.Done) || KitchenState.HasSlot(s, CookingStation.Status.Burnt) ||
                        !KitchenState.HasSlot(s, CookingStation.Status.NotDone) || Time.time > _tendUntil)
                        return End(h, 0f);
                    return ChoreProgress.Running;
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
                    // Taking food off throws it towards the Cook: pick up around the stove and around itself.
                    int picked = WorkSteps.PickUpDrops(h, s.transform.position, outputs, LooseRadius) + WorkSteps.PickUpDrops(h, h.transform.position, outputs, 4f);
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
                    if (_done < _count && h.CargoInventory!.CountItems(shared) > 0 && KitchenState.FreeSlots(s) <= 0 && _more.Count > 0)
                    {
                        // Spit master: on to the next free stove of the same kind.
                        _station = _more[0];
                        _more.RemoveAt(0);
                        _walk.Reset();
                        EnhancementEvents.Fire(h, "SpitMaster", Core.Diagnostics.LogCat.Work, ("station", _station.name));
                        return ChoreProgress.Running;
                    }
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

        // Food on a stove in its area that's cooked, or will be within this long: what it carries waits.
        private const float DueSoonSeconds = 90f;

        /// <summary>
        /// A stove in its area has food cooked or nearly so. Svanhild took the meat off one of three spits over a campfire,
        /// went to put it away (75 s: no chest had room) and the other two spits burned.
        /// </summary>
        private static bool FoodDueSoon(Hireling h)
        {
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(h.WorkCentre, h.Radius, pieces);
            foreach (CookingStation s in pieces.Where(p => p != null).Select(p => p.GetComponentInChildren<CookingStation>())
                         .Where(s => s != null && s.m_nview != null && s.m_nview.IsValid() && KitchenState.Heated(s)))
                for (int i = 0; i < s.m_slots.Length; i++)
                {
                    s.GetSlot(i, out string item, out float cooked, out CookingStation.Status st, out _);
                    if (item.Length == 0 || st == CookingStation.Status.Burnt)
                        continue;
                    if (st == CookingStation.Status.Done)
                        return true;
                    CookingStation.ItemConversion? c = s.m_conversion.FirstOrDefault(x => x?.m_from != null && x.m_from.name == item);
                    if (c != null && c.m_cookTime - cooked <= DueSoonSeconds)
                        return true;
                }
            return false;
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            h.HoldDeliveries = FoodDueSoon(h);
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
