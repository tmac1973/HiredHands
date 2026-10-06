using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Empties beehives (Beehives) or sap collectors (Sap) once they're half full: extracts as a player does (the
    /// producer's own RPC drops the items at its spawn point), picks them up, and the delivery rule takes them to a chest
    /// that already holds them (else the pile). One trip empties every producer of its kind that's due, nearest first.
    /// </summary>
    internal sealed class ProducersChore : IChore
    {
        private const float StuckSeconds = 20f;
        private const float PickupDelay = 0.6f;
        private const float PickupRadius = 2.5f;

        private readonly StewardSteps _walk = new();
        private readonly List<Producer> _route = new();
        private float _extractedAt = -1f;
        private int _picked;

        /// <summary>A beehive or a sap collector, read the same way.</summary>
        private sealed class Producer
        {
            public Component Comp = null!;
            public ZNetView View = null!;
            public int Max;
            public string Item = "";
            public string Name = "";
            public Vector3 SpawnAt;
            public int Level => View.IsValid() ? View.GetZDO().GetInt(ZDOVars.s_level) : 0;
        }

        public ProducersChore(ChoreKind kind) => Kind = kind;

        public ChoreKind Kind { get; }
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        private IEnumerable<Producer> Producers(StewardContext ctx)
        {
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            foreach (Piece p in pieces)
            {
                if (p == null || p.GetCreator() == 0L || !ctx.BoardOwnerMayUse(p.transform.position))
                    continue;
                Producer? x = Read(p);
                if (x != null && x.View != null && x.View.IsValid() && x.Max > 0 && x.Item.Length > 0)
                    yield return x;
            }
        }

        private Producer? Read(Piece p)
        {
            if (Kind == ChoreKind.Beehives && p.GetComponentInChildren<Beehive>() is Beehive b && b.m_honeyItem != null)
                return new Producer
                {
                    Comp = b, View = b.m_nview, Max = b.m_maxHoney, Item = b.m_honeyItem.gameObject.name, Name = b.m_name,
                    SpawnAt = b.m_spawnPoint != null ? b.m_spawnPoint.position : b.transform.position,
                };
            if (Kind == ChoreKind.Sap && p.GetComponentInChildren<SapCollector>() is SapCollector s && s.m_spawnItem != null)
                return new Producer
                {
                    Comp = s, View = s.m_nview, Max = s.m_maxLevel, Item = s.m_spawnItem.gameObject.name, Name = s.m_name,
                    SpawnAt = s.m_spawnPoint != null ? s.m_spawnPoint.position : s.transform.position,
                };
            return null;
        }

        public IEnumerable<ChoreJob> Candidates(StewardContext ctx)
        {
            Missing = null;
            var jobs = new List<ChoreJob>();
            foreach (Producer x in Producers(ctx))
            {
                float u = ChoreUrgency.Producer(x.Level, x.Max);
                if (u <= 0f || Reservations.IsReservedByOther(x.Comp, ctx.Hireling.Hid) || Reservations.IsSkipped(x.Comp))
                    continue;
                SmelterDeliveryPolicy.StewardOutputs.Add(x.Item);
                // No chest holds it yet: it still gets emptied (to the pile), and the status says where to put it.
                bool home = ctx.Chests.Any(c => c.GetInventory().GetAllItems().Any(i => i.m_dropPrefab != null && i.m_dropPrefab.name == x.Item));
                jobs.Add(new ChoreJob
                {
                    Kind = Kind, Target = x.Comp, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, x.Comp.transform.position), ctx.Radius),
                    Label = home ? ActivityText.Make("$vfh_steward_collect", x.Name)
                        : ActivityText.Make("$vfh_need_room", x.Name, StewardSteps.SharedName(x.Item)),
                });
            }
            return jobs;
        }

        public void Begin(ChoreJob job, StewardContext ctx)
        {
            Hireling h = ctx.Hireling;
            RestAfter = 0f;
            _walk.Reset();
            _extractedAt = -1f;
            _route.Clear();
            List<Producer> due = Producers(ctx).Where(x => ChoreUrgency.Producer(x.Level, x.Max) > 0f &&
                                                          !Reservations.IsReservedByOther(x.Comp, h.Hid) && !Reservations.IsSkipped(x.Comp)).ToList();
            Producer? first = due.FirstOrDefault(x => x.Comp == job.Target);
            if (first == null)
                return;
            _route.Add(first);
            _route.AddRange(due.Where(x => x != first).OrderBy(x => Vector3.Distance(first.Comp.transform.position, x.Comp.transform.position)));
            foreach (Producer x in _route)
                Reservations.TryReserve(x.Comp, h.Hid);
            h.HoldDeliveries = true;
            _picked = 0;
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_route.Count == 0)
                return End(h, 0f);
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "steward.collect_stuck", ("hid", h.Hid), ("kind", Kind), ("pos", h.transform.position));
                return End(h, 5f, ChoreProgress.Failed);
            }
            Producer x = _route[0];
            if (x.Comp == null || !x.View.IsValid())
            {
                Next(h);
                return ChoreProgress.Running;
            }
            if (!_walk.Approach(ai, dt, x.Comp, x.Comp.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(x.Comp.transform.position);
            if (_extractedAt < 0f)
            {
                if (x.Level <= 0)
                {
                    Next(h);
                    return ChoreProgress.Running;
                }
                VfhLog.D(LogCat.Smelter, "steward.collect", ("hid", h.Hid), ("kind", Kind), ("level", x.Level));
                x.View.InvokeRPC("RPC_Extract");
                _extractedAt = Time.time;
                return ChoreProgress.Running;
            }
            if (Time.time - _extractedAt < PickupDelay)
                return ChoreProgress.Running;
            int picked = StewardSteps.PickUpDrops(h, x.SpawnAt, new HashSet<string> { x.Item }, PickupRadius);
            _picked += picked;
            VfhLog.D(LogCat.Smelter, "steward.collected", ("hid", h.Hid), ("kind", Kind), ("item", x.Item), ("n", picked));
            Next(h);
            // Cargo full: deliver what it has (the delivery rule) and come back for the rest later.
            return h.CargoInventory!.NrOfItems() >= h.CargoSlots ? End(h, 0f) : ChoreProgress.Running;
        }

        private void Next(Hireling h)
        {
            if (_route.Count > 0)
            {
                Reservations.Release(_route[0].Comp, h.Hid);
                _route.RemoveAt(0);
            }
            _extractedAt = -1f;
            _walk.Reset();
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            h.HoldDeliveries = false;
            // Put it all away now. Sap is also refinery fuel, so the delivery rule alone would keep it as supply.
            if (_picked > 0 && h.Zdo != null)
                h.Zdo.Set(HirelingZdo.DeliverPending, true);
            _picked = 0;
            foreach (Producer x in _route.Where(x => x.Comp != null))
                Reservations.Release(x.Comp, h.Hid);
            _route.Clear();
            _extractedAt = -1f;
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h) => End(h, 0f);
    }
}
