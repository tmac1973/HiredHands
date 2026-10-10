using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Tends fermenters: taps a ready one and takes the meads (the delivery rule puts them in chests), and puts a mead
    /// base from the chests into an empty one, as a player does. It never brews: the bases are the player's. A fermenter
    /// that isn't under a roof (Exposed) is left alone.
    /// </summary>
    internal sealed class FermentersChore : IChore
    {
        private enum Step { None, Fetch, Load, Tap }

        private const int VariantTap = 0;
        private const int VariantLoad = 1;
        private const float StuckSeconds = 20f;
        private const float ChestSkipSeconds = 60f;
        private const float PickupDelay = 0.8f;

        private readonly WorkSteps _walk = new();
        private Step _step;
        private Fermenter? _fermenter;
        private string _base = "";
        private Container? _chest;
        private float _tappedAt = -1f;
        private float _nextLoad;

        public ChoreKind Kind => ChoreKind.Fermenters;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        private static IEnumerable<Fermenter> Fermenters(WorkContext ctx)
        {
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            foreach (Piece p in pieces)
            {
                if (p == null || p.GetCreator() == 0L || !ctx.BoardOwnerMayUse(p.transform.position))
                    continue;
                Fermenter f = p.GetComponentInChildren<Fermenter>();
                if (f != null && f.m_nview != null && f.m_nview.IsValid())
                    yield return f;
            }
        }

        // The bases it may load: not those whose mead is at its limit.
        private static string[] Bases(Fermenter f, WorkContext ctx) =>
            f.m_conversion.Where(c => c.m_from != null && (c.m_to == null || !StewardLimits.Reached(ctx, c.m_to.gameObject.name, out _)))
                .Select(c => c.m_from.gameObject.name).ToArray();

        private static string[] Meads(Fermenter f) => f.m_conversion.Where(c => c.m_to != null).Select(c => c.m_to.gameObject.name).ToArray();

        // The mead brewing in it (from the base it was loaded with).
        private static string? MeadOf(Fermenter f)
        {
            int content = f.GetContent(); // the base's prefab name hash
            return content == 0 ? null : f.GetItemConversion(content)?.m_to?.gameObject.name;
        }

        private static int Have(WorkContext ctx, string prefab) => ctx.Available(prefab) + (ctx.Carried.TryGetValue(prefab, out int c) ? c : 0);

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            var jobs = new List<ChoreJob>();
            foreach (Fermenter f in Fermenters(ctx))
            {
                if (Reservations.IsReservedByOther(f, ctx.Hireling.Hid) || Reservations.IsSkipped(f))
                    continue;
                Fermenter.Status status = f.GetStatus();
                bool ready = status == Fermenter.Status.Ready;
                // Out in the open it never brews (vanilla keeps resetting it): don't load it; the player has to roof it over.
                bool empty = status == Fermenter.Status.Empty && !f.m_exposed && f.m_hasRoof;
                if (status == Fermenter.Status.Empty && !empty)
                {
                    Missing ??= ActivityText.Make("$vfh_need_cover", f.m_name);
                    continue;
                }
                if (empty && !Bases(f, ctx).Any(b => Have(ctx, b) > 0))
                {
                    if (f.m_conversion.Any(c => c.m_from != null && Have(ctx, c.m_from.gameObject.name) > 0))
                    {
                        Missing ??= ActivityText.Make("$vfh_paused_limits", f.m_name);
                        continue;
                    }
                    Missing ??= ActivityText.Make("$vfh_need_base", f.m_name);
                    continue;
                }
                // PauseWhenStorageFull: a ready mead keeps inside the fermenter until its chests have room.
                if (ready && MeadOf(f) is string mead0 && StorageRoom.NoRoom(ctx.AllChests, mead0))
                {
                    Missing ??= ActivityText.Make("$vfh_paused_full", f.m_name, WorkSteps.SharedName(mead0));
                    continue;
                }
                float u = ChoreUrgency.Fermenter(ready, empty);
                if (u <= 0f)
                    continue;
                foreach (string mead in Meads(f))
                    ChoreDeliveryPolicy.ChoreOutputs.Add(mead);
                jobs.Add(new ChoreJob
                {
                    Kind = Kind, Target = f, Urgency = u, Variant = ready ? VariantTap : VariantLoad,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, f.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_steward_ferment", f.m_name),
                });
            }
            return jobs;
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            RestAfter = 0f;
            _walk.Reset();
            _tappedAt = -1f;
            _fermenter = (Fermenter)job.Target;
            Reservations.TryReserve(_fermenter, ctx.Hireling.Hid);
            if (job.Variant == VariantTap)
            {
                _step = Step.Tap;
                return;
            }
            // The base the chests (and cargo) hold most of.
            _base = Bases(_fermenter, ctx).OrderByDescending(b => Have(ctx, b)).FirstOrDefault() ?? "";
            if (_base.Length == 0)
            {
                _step = Step.None;
                RestAfter = 3f;
                return;
            }
            bool carrying = ctx.Carried.TryGetValue(_base, out int c) && c > 0;
            _chest = carrying ? null : ctx.NearestChestWith(new[] { _base });
            _step = carrying ? Step.Load : _chest != null ? Step.Fetch : Step.None;
            if (_step == Step.None)
                RestAfter = 3f; // its chest is open: try again in a moment
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            Fermenter? f = _fermenter;
            if (f == null || f.m_nview == null || !f.m_nview.IsValid() || _step == Step.None)
                return End(h, 0f);
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "steward.ferment_stuck", ("hid", h.Hid), ("step", _step), ("pos", h.transform.position));
                if (_chest != null)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                return End(h, 5f, ChoreProgress.Failed);
            }
            switch (_step)
            {
                case Step.Fetch:
                {
                    Container? chest = _chest;
                    if (chest == null)
                        return End(h, 0f);
                    if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                        return ChoreProgress.Running;
                    ai.Halt();
                    if (WorkSteps.TakeFromChest(chest, h, new Dictionary<string, int> { [_base] = 1 }, VfhConfig.ChestReserve) == 0)
                    {
                        Reservations.Skip(chest, ChestSkipSeconds);
                        return End(h, 1f, ChoreProgress.Failed);
                    }
                    _step = Step.Load;
                    _walk.Reset();
                    return ChoreProgress.Running;
                }
                case Step.Load:
                {
                    if (!_walk.Approach(ai, dt, f, f.transform.position))
                        return ChoreProgress.Running;
                    ai.Halt();
                    ai.Face(f.transform.position);
                    if (f.GetStatus() != Fermenter.Status.Empty)
                        return End(h, 0f);
                    string shared = WorkSteps.SharedName(_base);
                    if (h.CargoInventory!.CountItems(shared) <= 0)
                        return End(h, 0f, ChoreProgress.Failed);
                    if (Time.time < _nextLoad)
                        return ChoreProgress.Running;
                    _nextLoad = Time.time + 1f;
                    h.CargoInventory.RemoveItem(shared, 1);
                    // As Fermenter.AddItem: the base's prefab name hash, not cheated.
                    f.m_nview.InvokeRPC("RPC_AddItem", _base.GetStableHashCode(), false);
                    VfhLog.D(LogCat.Smelter, "steward.ferment_load", ("hid", h.Hid), ("base", _base));
                    return End(h, 2f);
                }
                default:
                {
                    if (!_walk.Approach(ai, dt, f, f.transform.position))
                        return ChoreProgress.Running;
                    ai.Halt();
                    ai.Face(f.transform.position);
                    if (_tappedAt < 0f)
                    {
                        if (f.GetStatus() != Fermenter.Status.Ready)
                            return End(h, 0f);
                        f.m_nview.InvokeRPC("RPC_Tap");
                        _tappedAt = Time.time;
                        return ChoreProgress.Running;
                    }
                    _walk.Reset();
                    if (Time.time - _tappedAt < PickupDelay + f.m_tapDelay)
                        return ChoreProgress.Running;
                    Vector3 at = f.m_outputPoint != null ? f.m_outputPoint.position : f.transform.position;
                    int picked = WorkSteps.PickUpDrops(h, at, new HashSet<string>(Meads(f)), 3f);
                    VfhLog.D(LogCat.Smelter, "steward.ferment_tap", ("hid", h.Hid), ("picked", picked));
                    return End(h, 0f);
                }
            }
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            if (_fermenter != null)
                Reservations.Release(_fermenter, h.Hid);
            _fermenter = null;
            _chest = null;
            _step = Step.None;
            _tappedAt = -1f;
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h) => End(h, 0f);
    }
}
