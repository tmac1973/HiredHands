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
    /// Keeps shield generators fed: below the refill fraction, it fetches their fuel (bones, the first of their fuel
    /// items the chests hold) and adds it one at a time through the generator's own RPC, stopping at full (the RPC
    /// itself doesn't).
    /// </summary>
    internal sealed class ShieldsChore : IChore
    {
        private const float ItemSeconds = 0.3f;
        private const float StuckSeconds = 20f;
        private const float ChestSkipSeconds = 60f;

        private readonly StewardSteps _walk = new();
        private ShieldGenerator? _gen;
        private string _fuel = "";
        private Container? _chest;
        private int _need;
        private bool _fetching;
        private float _nextItemAt;
        private int _toAdd;

        public ChoreKind Kind => ChoreKind.Shields;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        private static IEnumerable<ShieldGenerator> Generators(StewardContext ctx)
        {
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            foreach (Piece p in pieces)
            {
                if (p == null || p.GetCreator() == 0L || !ctx.BoardOwnerMayUse(p.transform.position))
                    continue;
                ShieldGenerator g = p.GetComponentInChildren<ShieldGenerator>();
                if (g != null && g.m_nview != null && g.m_nview.IsValid() && g.m_fuelItems != null && g.m_fuelItems.Count > 0)
                    yield return g;
            }
        }

        private static string? FuelFor(StewardContext ctx, ShieldGenerator g) =>
            g.m_fuelItems.Where(i => i != null).Select(i => i.gameObject.name)
                .FirstOrDefault(f => ctx.Available(f) + (ctx.Carried.TryGetValue(f, out int c) ? c : 0) > 0);

        public IEnumerable<ChoreJob> Candidates(StewardContext ctx)
        {
            Missing = null;
            var jobs = new List<ChoreJob>();
            foreach (ShieldGenerator g in Generators(ctx))
            {
                float u = ChoreUrgency.Shield(g.GetFuel(), g.m_maxFuel, VfhConfig.StewardFireRefillFraction.Value);
                if (u <= 0f || Reservations.IsReservedByOther(g, ctx.Hireling.Hid) || Reservations.IsSkipped(g))
                    continue;
                if (FuelFor(ctx, g) == null)
                {
                    Missing ??= ActivityText.Make("$vfh_need_fuel", g.m_name, StewardSteps.SharedName(g.m_fuelItems[0].gameObject.name));
                    continue;
                }
                jobs.Add(new ChoreJob
                {
                    Kind = Kind, Target = g, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, g.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_steward_shield", g.m_name),
                });
            }
            return jobs;
        }

        public void Begin(ChoreJob job, StewardContext ctx)
        {
            RestAfter = 0f;
            _walk.Reset();
            _gen = (ShieldGenerator)job.Target;
            Reservations.TryReserve(_gen, ctx.Hireling.Hid);
            _fuel = FuelFor(ctx, _gen) ?? "";
            int carried = ctx.Carried.TryGetValue(_fuel, out int c) ? c : 0;
            int want = Mathf.Max(0, Mathf.FloorToInt(_gen.m_maxFuel - _gen.GetFuel()));
            int stack = Mathf.Max(1, StewardSteps.MaxStack(_fuel));
            int room = carried % stack == 0 ? ctx.FreeSlots * stack : stack - carried % stack + ctx.FreeSlots * stack;
            _need = Mathf.Min(Mathf.Max(0, want - carried), ctx.Available(_fuel), room);
            _chest = _need > 0 ? ctx.NearestChestWith(new[] { _fuel }) : null;
            _fetching = _chest != null;
            _toAdd = want;
            RestAfter = _need > 0 && _chest == null && carried == 0 ? 3f : 0f;
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            ShieldGenerator? g = _gen;
            if (g == null || g.m_nview == null || !g.m_nview.IsValid() || _fuel.Length == 0)
                return End(h, 0f);
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "steward.shield_stuck", ("hid", h.Hid), ("pos", h.transform.position));
                if (_chest != null)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                return End(h, 5f, ChoreProgress.Failed);
            }
            if (_fetching)
            {
                Container? chest = _chest;
                if (chest == null)
                    return End(h, 0f);
                if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                if (StewardSteps.TakeFromChest(chest, h, new Dictionary<string, int> { [_fuel] = _need }, VfhConfig.ChestReserve) == 0)
                {
                    Reservations.Skip(chest, ChestSkipSeconds);
                    return End(h, 1f, ChoreProgress.Failed);
                }
                _fetching = false;
                _walk.Reset();
                return ChoreProgress.Running;
            }
            if (!_walk.Approach(ai, dt, g, g.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(g.transform.position);
            string shared = StewardSteps.SharedName(_fuel);
            // Counted here: the generator's own reading lags a round trip when another game owns it, and it doesn't cap.
            if (_toAdd <= 0 || h.CargoInventory!.CountItems(shared) <= 0)
                return End(h, RestAfter);
            if (Time.time < _nextItemAt)
                return ChoreProgress.Running;
            _nextItemAt = Time.time + ItemSeconds;
            h.CargoInventory.RemoveItem(shared, 1);
            g.m_nview.InvokeRPC("RPC_AddFuel"); // plays its own effect
            _toAdd--;
            return ChoreProgress.Running;
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            if (_gen != null)
                Reservations.Release(_gen, h.Hid);
            _gen = null;
            _chest = null;
            _fetching = false;
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h) => End(h, 0f);
    }
}
