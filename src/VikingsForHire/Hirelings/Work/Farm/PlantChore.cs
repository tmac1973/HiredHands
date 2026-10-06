using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Orders;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Chores;
using VikingsForHire.Hirelings.Nav;

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>
    /// The Farmer's planting: the farm plan's first crop (seed orders first), its seeds fetched from the chests (the
    /// planner already kept the seed reserve back), planted in rows on free cultivated ground the way the cultivator does.
    /// </summary>
    internal sealed class PlantChore : IChore
    {
        private const float StuckSeconds = 20f;
        private const float PlantEvery = 0.6f;
        private const float ChestSkipSeconds = 60f;

        private readonly WorkSteps _walk = new();
        private Crop? _crop;
        private int _count;
        private Container? _chest;
        private bool _fetching;
        private readonly List<Vector3> _spots = new();
        private float _nextPlant;
        private float _progressAt;
        private int _planted;
        private long _owner;
        private HiringBoard? _board;

        public ChoreKind Kind => ChoreKind.Plant;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        /// <summary>Saplings planted by Farmers since login (for the tests).</summary>
        public static readonly Dictionary<string, int> Planted = new();

        /// <summary>Free spots per sapling for the board's farm plan (FarmState's hook).</summary>
        public static Dictionary<string, int> FreeSpots(HiringBoard board)
        {
            var result = new Dictionary<string, int>();
            if (board.Zdo == null)
                return result;
            ContractEntry? farmer = BoardRosterOps.Read(board.Zdo).Entries.FirstOrDefault(e => e.Job == JobType.Farmer && e.State != ContractState.Leaving);
            if (farmer == null)
                return result;
            long owner = DoorRules.BoardOwner(board.Id);
            foreach (Crop c in CropCatalog.All.Where(c => !c.Info.Regrowing && c.Info.Level <= farmer.Level))
                result[c.Info.Plant] = FieldGrid.Spots(c, board.transform.position, farmer.Radius, board.transform.right, owner).Count;
            return result;
        }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            HiringBoard? board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            if (board == null)
                return Enumerable.Empty<ChoreJob>();
            FarmPlan plan = FarmState.For(board);
            Missing = plan.Missing.FirstOrDefault();
            if (plan.Plant.Count == 0 || ctx.FreeSlots <= 0 && !plan.Plant.Any(p => ctx.Carried.ContainsKey(p.Crop.Consumes)))
                return Enumerable.Empty<ChoreJob>();
            (CropInfo info, _) = plan.Plant[0];
            const float u = 0.45f; // below harvesting, which frees ground for it
            return new[]
            {
                new ChoreJob
                {
                    // The Farmer itself, not the board: a failure skips its own planting, never the board (the Steward stocks it).
                    Kind = Kind, Target = ctx.Hireling, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, board.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_farmer_plant", WorkSteps.SharedName(info.Consumes)),
                },
            };
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            RestAfter = 0f;
            _walk.Reset();
            _spots.Clear();
            _planted = 0;
            _progressAt = Time.time;
            _board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            if (_board == null)
            {
                _count = 0;
                return;
            }
            _owner = DoorRules.BoardOwner(_board.Id);
            FarmPlan plan = FarmState.For(_board);
            (CropInfo info, int count) = plan.Plant[0];
            _crop = CropCatalog.BySapling(info.Plant);
            if (_crop == null)
            {
                _count = 0;
                return;
            }
            Hireling h = ctx.Hireling;
            h.FetchingSupplies = true;
            int each = Mathf.Max(1, info.ConsumesAmount);
            int carried = ctx.Carried.TryGetValue(info.Consumes, out int c) ? c : 0;
            int stack = Mathf.Max(1, WorkSteps.MaxStack(info.Consumes));
            int room = (carried % stack == 0 ? 0 : stack - carried % stack) + ctx.FreeSlots * stack;
            _count = Mathf.Min(count, (carried + room) / each);
            _spots.AddRange(FieldGrid.Spots(_crop, ctx.Home, ctx.Radius, _board.transform.right, _owner).Take(_count));
            _count = Mathf.Min(_count, _spots.Count);
            int need = _count * each - carried;
            _chest = need > 0 ? ctx.NearestChestWith(new[] { info.Consumes }) : null;
            _fetching = _chest != null;
            _fetchAmount = Mathf.Max(0, need);
        }

        private int _fetchAmount;

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            Crop? crop = _crop;
            if (crop == null || _count <= 0)
                return End(h, 3f);
            if (_fetching)
            {
                Container? chest = _chest;
                if (chest == null)
                    return End(h, 3f);
                if (_walk.SinceProgress > StuckSeconds)
                {
                    Reservations.Skip(chest, ChestSkipSeconds);
                    return End(h, 5f, ChoreProgress.Failed);
                }
                if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                if (WorkSteps.TakeFromChest(chest, h, new Dictionary<string, int> { [crop.Info.Consumes] = _fetchAmount }, Config.VfhConfig.ChestReserve) == 0)
                    Reservations.Skip(chest, ChestSkipSeconds);
                _fetching = false;
                _progressAt = Time.time;
                return ChoreProgress.Running;
            }
            string seed = WorkSteps.SharedName(crop.Info.Consumes);
            int each = Mathf.Max(1, crop.Info.ConsumesAmount);
            if (_spots.Count == 0 || _planted >= _count || h.CargoInventory!.CountItems(seed) < each)
                return End(h, 1f);
            Vector3 spot = _spots[0];
            if (Utils.DistanceXZ(ai.transform.position, spot) > 1.6f)
            {
                if (Time.time - _progressAt > StuckSeconds)
                {
                    FieldGrid.Avoid(spot); // can't get to this one: left alone for a while; try the next
                    _spots.RemoveAt(0);
                    _progressAt = Time.time;
                    return ChoreProgress.Running;
                }
                ai.WalkTo(dt, spot, 1f, run: false);
                return ChoreProgress.Running;
            }
            ai.Halt();
            ai.Face(spot);
            if (Time.time < _nextPlant)
                return ChoreProgress.Running;
            _nextPlant = Time.time + PlantEvery;
            _progressAt = Time.time;
            _spots.RemoveAt(0);
            if (!FieldGrid.IsFree(crop, spot, _owner))
                return ChoreProgress.Running; // taken meanwhile
            PlantAt(h, crop, spot);
            h.CargoInventory.RemoveItem(seed, each);
            _planted++;
            return ChoreProgress.Running;
        }

        // As the cultivator places a sapling: through the terrain-modifier trigger, owned by the board's owner.
        private void PlantAt(Hireling h, Crop crop, Vector3 spot)
        {
            TerrainModifier.SetTriggerOnPlaced(true);
            GameObject go;
            try
            {
                go = Object.Instantiate(crop.Prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            finally
            {
                TerrainModifier.SetTriggerOnPlaced(false);
            }
            if (go.GetComponent<Piece>() is Piece piece)
            {
                // Built by the board's owner, as if they'd planted it (the platform id isn't needed for that).
                if (_owner != 0L && piece.m_nview != null && piece.m_nview.IsValid() && piece.GetCreator() == 0L)
                {
                    piece.m_creator = _owner;
                    piece.m_nview.GetZDO().Set(ZDOVars.s_creator, _owner);
                }
                piece.m_placeEffect.Create(spot, go.transform.rotation, go.transform);
            }
            Planted[crop.Info.Plant] = (Planted.TryGetValue(crop.Info.Plant, out int n) ? n : 0) + 1;
            VfhLog.D(LogCat.Work, "farmer.plant", ("hid", h.Hid), ("sapling", crop.Info.Plant), ("at", spot));
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            h.FetchingSupplies = false;
            if (_planted == 0 && result == ChoreProgress.Done)
                result = ChoreProgress.Failed; // nothing planted: count it, so a job that can't be done gets set aside
            if (_planted > 0 && _board != null)
            {
                FarmState.Invalidate(_board);
                FieldGrid.Forget();
                VfhLog.I(LogCat.Work, "farmer.planted", ("hid", h.Hid), ("sapling", _crop?.Info.Plant ?? ""), ("n", _planted));
            }
            _crop = null;
            _chest = null;
            _fetching = false;
            _spots.Clear();
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h)
        {
            if (_crop != null)
                End(h, 0f);
        }
    }
}
