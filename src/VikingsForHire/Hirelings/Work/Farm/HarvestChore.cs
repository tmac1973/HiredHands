using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Compat;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Orders;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>
    /// The Farmer's harvest: every ripe crop in its radius (always), and regrowing plants (bushes, mushrooms) whose item an
    /// order is short of. One trip walks up to 8 spots, nearest first, and at each picks everything ripe within the harvest
    /// radius (PlantEasily's, else 3 m); then the harvest is put away.
    /// </summary>
    internal sealed class HarvestChore : IChore
    {
        private const int MaxStops = 8;
        private const float StopChain = 15f;
        private const float StuckSeconds = 15f;
        private const float SkipSeconds = 300f;

        private readonly WorkSteps _walk = new();
        private readonly List<Pickable> _stops = new();
        private readonly List<Plant> _plants = new();
        private readonly List<Pickable> _pickables = new();
        private HashSet<string> _pickRegrowing = new();
        private WorkContext? _ctx;
        private int _picked;

        public ChoreKind Kind => ChoreKind.Harvest;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        /// <summary>Items picked by Farmers since login, by item (for the tests).</summary>
        public static readonly Dictionary<string, int> Harvested = new();

        private bool Wanted(Pickable p, WorkContext ctx, bool checkRoom)
        {
            if (p == null || p.m_nview == null || !p.m_nview.IsValid() || !PickableYield.Ripe(p))
                return false;
            if (CropCatalog.ByGrown(Utils.GetPrefabName(p.gameObject)) is not Crop crop)
                return false;
            if (crop.Info.Regrowing && (crop.Info.Level > ctx.Level || !_pickRegrowing.Contains(crop.Info.Yields)))
                return false;
            if (Utils.DistanceXZ(p.transform.position, ctx.Home) > ctx.Radius || Reservations.IsSkipped(p) || !ctx.BoardOwnerMayUse(p.transform.position))
                return false;
            return !checkRoom || !StorageRoom.NoRoom(ctx.AllChests, crop.Info.Yields);
        }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            HiringBoard? board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            _pickRegrowing = board != null ? FarmState.For(board).PickRegrowing : new HashSet<string>();
            if (ctx.FreeSlots <= 0)
                return Enumerable.Empty<ChoreJob>();
            FarmScan.Nearby(ctx.Home, ctx.Radius, _plants, _pickables);
            Pickable? best = null;
            float bestD = float.MaxValue;
            bool regrowing = false;
            foreach (Pickable p in _pickables)
            {
                if (!Wanted(p, ctx, checkRoom: false))
                    continue;
                Crop crop = CropCatalog.ByGrown(Utils.GetPrefabName(p.gameObject))!;
                if (StorageRoom.NoRoom(ctx.AllChests, crop.Info.Yields))
                {
                    Missing ??= ActivityText.Make("$vfh_status_storage_full", WorkSteps.SharedName(crop.Info.Yields));
                    continue;
                }
                float d = Vector3.Distance(ctx.Position, p.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                    regrowing = crop.Info.Regrowing;
                }
            }
            if (best == null)
                return Enumerable.Empty<ChoreJob>();
            float u = regrowing ? 0.4f : 0.5f;
            return new[]
            {
                new ChoreJob
                {
                    Kind = Kind, Target = best, Urgency = u,
                    Score = ChoreUrgency.Score(u, bestD, ctx.Radius),
                    Label = ActivityText.Make("$vfh_farmer_harvest", WorkSteps.SharedName(CropCatalog.ByGrown(Utils.GetPrefabName(best.gameObject))!.Info.Yields)),
                },
            };
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            RestAfter = 0f;
            _ctx = ctx;
            _picked = 0;
            _walk.Reset();
            _stops.Clear();
            ctx.Hireling.HoldDeliveries = true;
            var left = _pickables.Where(p => Wanted(p, ctx, checkRoom: true)).ToList();
            Vector3 from = ctx.Position;
            float reach = PlantMods.HarvestRadius;
            while (_stops.Count < MaxStops && left.Count > 0)
            {
                Pickable next = left.OrderBy(p => Vector3.Distance(from, p.transform.position)).First();
                if (_stops.Count > 0 && Vector3.Distance(from, next.transform.position) > StopChain)
                    break;
                _stops.Add(next);
                from = next.transform.position;
                left.RemoveAll(p => Vector3.Distance(p.transform.position, from) <= reach);
            }
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            WorkContext? ctx = _ctx;
            while (_stops.Count > 0 && (_stops[0] == null || _stops[0].m_nview == null || !_stops[0].m_nview.IsValid() || !PickableYield.Ripe(_stops[0])))
                _stops.RemoveAt(0);
            if (ctx == null || _stops.Count == 0 || h.CargoInventory == null || h.CargoInventory.NrOfItems() >= h.CargoSlots)
                return Finish(h);
            Pickable stop = _stops[0];
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.D(LogCat.Work, "farmer.harvest_stuck", ("hid", h.Hid), ("target", Utils.GetPrefabName(stop.gameObject)), ("pos", stop.transform.position));
                Reservations.Skip(stop, SkipSeconds);
                _stops.RemoveAt(0);
                _walk.Reset();
                return ChoreProgress.Running;
            }
            if (!_walk.Approach(ai, dt, stop, stop.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(stop.transform.position);
            float reach = PlantMods.HarvestRadius;
            foreach (Pickable p in _pickables.Where(p => p != null && Vector3.Distance(p.transform.position, h.transform.position) <= reach + 1f && Wanted(p, ctx, checkRoom: true)).ToList())
            {
                if (h.CargoInventory.NrOfItems() >= h.CargoSlots)
                    break;
                Pick(h, p);
            }
            _stops.RemoveAt(0);
            _walk.Reset();
            return ChoreProgress.Running;
        }

        // A pick without a player: the yield into cargo (what doesn't fit drops at its feet), then the pickable is marked picked
        // (a crop is removed by its owner, a bush starts regrowing).
        private void Pick(Hireling h, Pickable p)
        {
            if (!p.m_nview.IsOwner())
                p.m_nview.ClaimOwnership();
            int n = PickableYield.Main(p);
            var items = new List<(GameObject Prefab, int Amount)>();
            if (n > 0 && p.m_itemPrefab != null)
                items.Add((p.m_itemPrefab, n));
            foreach (ItemDrop.ItemData extra in p.m_extraDrops?.GetDropListItems() ?? new List<ItemDrop.ItemData>())
                if (extra.m_dropPrefab != null)
                    items.Add((extra.m_dropPrefab, extra.m_stack));
            foreach ((GameObject prefab, int amount) in items)
            {
                int left = amount;
                while (left > 0)
                {
                    int chunk = Mathf.Min(left, Mathf.Max(1, prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize));
                    if (!h.CargoInventory!.AddItem(prefab, chunk))
                    {
                        ItemDrop.DropItem(prefab.GetComponent<ItemDrop>().m_itemData.Clone(), left, h.transform.position + h.transform.forward * 0.5f + Vector3.up * 0.3f, Quaternion.identity);
                        break;
                    }
                    left -= chunk;
                }
                Harvested[prefab.name] = (Harvested.TryGetValue(prefab.name, out int t) ? t : 0) + amount;
                ChoreDeliveryPolicy.ChoreOutputs.Add(prefab.name);
            }
            p.m_pickEffector.Create(p.transform.position, Quaternion.identity);
            p.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);
            _picked++;
            VfhLog.D(LogCat.Work, "farmer.harvest", ("hid", h.Hid), ("plant", Utils.GetPrefabName(p.gameObject)), ("items", string.Join(",", items.Select(i => $"{i.Prefab.name}x{i.Amount}"))));
        }

        private ChoreProgress Finish(Hireling h)
        {
            h.HoldDeliveries = false;
            if (_picked > 0 && h.Zdo != null)
                h.Zdo.Set(HirelingZdo.DeliverPending, true);
            if (_picked > 0 && BoardOrders.BoardOf(h.BoardId) is HiringBoard board)
                FarmState.Invalidate(board);
            _stops.Clear();
            _picked = 0;
            return ChoreProgress.Done;
        }

        public void Abort(Hireling h)
        {
            if (_stops.Count > 0 || _picked > 0)
                Finish(h);
        }
    }
}
