using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Keeps fires fuelled: fire pits, hearths, braziers, torches, sconces and hot tubs (vanilla Fireplace) built in the
    /// work radius, below the refill fraction, get fuel from chests up to full, one item at a time through the fire's
    /// own RPC as a player adds it. One trip fetches for every low fire burning the same fuel and visits them nearest first.
    /// </summary>
    internal sealed class FiresChore : IChore
    {
        private enum Step { None, Fetch, Fuel }

        private const float ItemSeconds = 0.3f;
        private const float StuckSeconds = 20f;
        private const float ChestSkipSeconds = 60f;

        /// <summary>Fuel items Stewards put into each fire since load, by the fire's instance id (for the tests).</summary>
        public static readonly Dictionary<int, int> FuelAdded = new();

        private readonly StewardSteps _walk = new();
        private readonly List<Fireplace> _route = new();
        private Step _step;
        private string _fuel = "";
        private int _need;
        private Container? _chest;
        private float _nextItemAt;
        private int _toAdd = -1; // for the fire being fuelled, counted from when it got there
        private Hireling? _h;

        public ChoreKind Kind => ChoreKind.Fires;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        private static IEnumerable<Fireplace> Fires(StewardContext ctx)
        {
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            foreach (Piece p in pieces)
            {
                if (p == null || p.GetCreator() == 0L)
                    continue;
                Fireplace f = p.GetComponentInChildren<Fireplace>();
                if (f == null || f.m_nview == null || !f.m_nview.IsValid() || f.m_fuelItem == null || f.m_infiniteFuel || !f.m_canRefill)
                    continue;
                if (!ctx.BoardOwnerMayUse(f.transform.position))
                    continue;
                yield return f;
            }
        }

        private static float Fuel(Fireplace f) => f.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel);

        private static float Urgency(Fireplace f) => ChoreUrgency.Fire(Fuel(f), f.m_maxFuel, VfhConfig.StewardFireRefillFraction.Value);

        public IEnumerable<ChoreJob> Candidates(StewardContext ctx)
        {
            Missing = null;
            var jobs = new List<ChoreJob>();
            foreach (Fireplace f in Fires(ctx))
            {
                float u = Urgency(f);
                if (u <= 0f || Reservations.IsReservedByOther(f, ctx.Hireling.Hid) || Reservations.IsSkipped(f))
                    continue;
                string fuel = f.m_fuelItem.gameObject.name;
                int have = ctx.Available(fuel) + (ctx.Carried.TryGetValue(fuel, out int c) ? c : 0);
                if (have <= 0)
                {
                    Missing ??= ActivityText.Make("$vfh_need_fuel", f.m_name, StewardSteps.SharedName(fuel));
                    continue;
                }
                jobs.Add(new ChoreJob
                {
                    Kind = Kind, Target = f, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, f.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_steward_fuel", f.m_name),
                });
            }
            return jobs;
        }

        public void Begin(ChoreJob job, StewardContext ctx)
        {
            Hireling h = ctx.Hireling;
            _h = h;
            RestAfter = 0f;
            _walk.Reset();
            var first = (Fireplace)job.Target;
            _fuel = first.m_fuelItem.gameObject.name;
            // Every low fire burning the same fuel, nearest first from the first one.
            _route.Clear();
            _route.Add(first);
            foreach (Fireplace f in Fires(ctx).Where(f => f != first && f.m_fuelItem.gameObject.name == _fuel && Urgency(f) > 0f &&
                                                       !Reservations.IsReservedByOther(f, h.Hid) && !Reservations.IsSkipped(f))
                         .OrderBy(f => Vector3.Distance(first.transform.position, f.transform.position)))
                _route.Add(f);
            foreach (Fireplace f in _route)
                Reservations.TryReserve(f, h.Hid);
            int want = _route.Sum(f => Mathf.Max(0, Mathf.CeilToInt(f.m_maxFuel - Fuel(f))));
            int carried = ctx.Carried.TryGetValue(_fuel, out int c) ? c : 0;
            int stack = Mathf.Max(1, StewardSteps.MaxStack(_fuel));
            int room = carried % stack == 0 ? ctx.FreeSlots * stack : stack - carried % stack + ctx.FreeSlots * stack;
            _need = Mathf.Min(Mathf.Max(0, want - carried), ctx.Available(_fuel), room);
            _chest = _need > 0 ? ctx.NearestChestWith(new[] { _fuel }) : null;
            _step = _chest != null ? Step.Fetch : Step.Fuel;
            RestAfter = _need > 0 && _chest == null ? 3f : 0f; // its chest is open: try again in a moment
            VfhLog.D(LogCat.Smelter, "steward.fires", ("hid", h.Hid), ("fires", _route.Count), ("fuel", _fuel), ("want", want), ("carried", carried), ("fetch", _need));
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "steward.fire_stuck", ("hid", h.Hid), ("step", _step), ("pos", h.transform.position));
                if (_chest != null)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                return End(h, 5f, ChoreProgress.Failed);
            }
            if (_step == Step.Fetch)
            {
                Container? chest = _chest;
                if (chest == null)
                    return End(h, 0f);
                if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                ai.Face(chest.transform.position);
                if (StewardSteps.TakeFromChest(chest, h, new Dictionary<string, int> { [_fuel] = _need }, VfhConfig.ChestReserve) == 0)
                {
                    Reservations.Skip(chest, ChestSkipSeconds);
                    return End(h, 1f, ChoreProgress.Failed);
                }
                _step = Step.Fuel;
                _walk.Reset();
                return ChoreProgress.Running;
            }
            return FuelNext(ai, h, dt);
        }

        private ChoreProgress FuelNext(HirelingAI ai, Hireling h, float dt)
        {
            string shared = StewardSteps.SharedName(_fuel);
            while (_route.Count > 0 && (_route[0] == null || _route[0].m_nview == null || !_route[0].m_nview.IsValid() ||
                                        Mathf.CeilToInt(Fuel(_route[0])) >= _route[0].m_maxFuel))
                NextFire(h);
            if (_route.Count == 0 || h.CargoInventory!.CountItems(shared) <= 0)
                return End(h, 0f);
            Fireplace f = _route[0];
            if (!_walk.Approach(ai, dt, f, f.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(f.transform.position);
            // How many it takes, counted once on arrival: the fire's reading lags when another game owns it.
            if (_toAdd < 0)
                _toAdd = Mathf.Max(0, Mathf.CeilToInt(f.m_maxFuel - Fuel(f)));
            if (_toAdd == 0)
            {
                NextFire(h);
                return ChoreProgress.Running;
            }
            if (Time.time < _nextItemAt)
                return ChoreProgress.Running;
            _nextItemAt = Time.time + ItemSeconds;
            h.CargoInventory.RemoveItem(shared, 1);
            f.m_nview.InvokeRPC("RPC_AddFuel"); // the fire caps it at full and plays its own effect
            _toAdd--;
            int id = f.GetInstanceID();
            FuelAdded[id] = (FuelAdded.TryGetValue(id, out int n) ? n : 0) + 1;
            return ChoreProgress.Running;
        }

        private void NextFire(Hireling h)
        {
            Fireplace done = _route[0];
            _route.RemoveAt(0);
            _toAdd = -1;
            if (done != null)
            {
                Reservations.Release(done, h.Hid);
                VfhLog.D(LogCat.Smelter, "steward.fire", ("hid", h.Hid), ("fire", Utils.GetPrefabName(done.gameObject)), ("fuel", done.m_nview != null && done.m_nview.IsValid() ? Fuel(done) : -1f));
            }
            _walk.Reset();
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            foreach (Fireplace f in _route.Where(f => f != null))
                Reservations.Release(f, h.Hid);
            _route.Clear();
            _step = Step.None;
            _chest = null;
            _toAdd = -1;
            RestAfter = System.Math.Max(rest, RestAfter);
            return result;
        }

        public void Abort(Hireling h) => End(h, 0f);
    }
}
