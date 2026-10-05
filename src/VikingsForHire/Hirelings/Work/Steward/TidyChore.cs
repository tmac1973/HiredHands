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
    /// Tidying up, the last thing on the list: items lying in the work radius for a while, of a kind some chest there
    /// already holds, are picked up and put away (it never carries things to the pile). One trip visits up to five spots,
    /// nearest first, then it delivers. Every other chore outranks it at each survey.
    /// </summary>
    internal sealed class TidyChore : IChore
    {
        private const int MaxSpots = 5;
        private const float SpotChain = 15f;
        private const float PickupRadius = 3f;
        private const float PlayerClearance = 3f;
        private const float StuckSeconds = 15f;

        private readonly StewardSteps _walk = new();
        private readonly List<ItemDrop> _spots = new();
        private HashSet<string> _kinds = new();
        private int _picked;

        public ChoreKind Kind => ChoreKind.Tidy;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        private static HashSet<string> Stored(StewardContext ctx) =>
            new(ctx.Chests.SelectMany(c => c.GetInventory().GetAllItems()).Where(i => i.m_dropPrefab != null).Select(i => i.m_dropPrefab.name));

        private static IEnumerable<ItemDrop> Litter(StewardContext ctx, HashSet<string> stored)
        {
            float minAge = VfhConfig.StewardTidyMinSeconds.Value;
            foreach (ItemDrop d in ItemDrop.s_instances)
            {
                if (d == null || d.m_nview == null || !d.m_nview.IsValid() || d.m_itemData?.m_dropPrefab == null)
                    continue;
                if (!stored.Contains(d.m_itemData.m_dropPrefab.name) || d.m_itemData.m_customData.ContainsKey(DropPile.Tag))
                    continue;
                ZDO z = d.m_nview.GetZDO();
                if (z == null || z.GetBool(AnimalsChore.FeedKey) || d.GetTimeSinceSpawned() < minAge)
                    continue;
                Vector3 p = d.transform.position;
                if (Utils.DistanceXZ(p, ctx.Home) > ctx.Radius || Player.GetClosestPlayer(p, PlayerClearance) != null)
                    continue;
                yield return d;
            }
        }

        public IEnumerable<ChoreJob> Candidates(StewardContext ctx)
        {
            Missing = null;
            if (ctx.FreeSlots <= 0)
                return Enumerable.Empty<ChoreJob>();
            ItemDrop? nearest = Litter(ctx, Stored(ctx)).Where(d => !Reservations.IsSkipped(d))
                .OrderBy(d => Vector3.Distance(ctx.Position, d.transform.position)).FirstOrDefault();
            if (nearest == null)
                return Enumerable.Empty<ChoreJob>();
            float u = ChoreUrgency.Tidy();
            return new[]
            {
                new ChoreJob
                {
                    Kind = Kind, Target = nearest, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, nearest.transform.position), ctx.Radius),
                    Label = "$vfh_steward_tidy",
                },
            };
        }

        public void Begin(ChoreJob job, StewardContext ctx)
        {
            RestAfter = 0f;
            _walk.Reset();
            _picked = 0;
            _kinds = Stored(ctx);
            _spots.Clear();
            // Up to five spots, each the nearest to the last and within 15 m of it.
            var left = Litter(ctx, _kinds).Where(d => !Reservations.IsSkipped(d)).ToList();
            Vector3 from = ctx.Position;
            while (_spots.Count < MaxSpots && left.Count > 0)
            {
                ItemDrop next = left.OrderBy(d => Vector3.Distance(from, d.transform.position)).First();
                if (_spots.Count > 0 && Vector3.Distance(from, next.transform.position) > SpotChain)
                    break;
                _spots.Add(next);
                from = next.transform.position;
                left.RemoveAll(d => Vector3.Distance(d.transform.position, from) <= PickupRadius);
            }
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            while (_spots.Count > 0 && (_spots[0] == null || _spots[0].m_nview == null || !_spots[0].m_nview.IsValid()))
                _spots.RemoveAt(0);
            if (_spots.Count == 0 || h.CargoInventory!.NrOfItems() >= h.CargoSlots)
                return Finish(h);
            ItemDrop spot = _spots[0];
            if (_walk.SinceProgress > StuckSeconds)
            {
                Reservations.Skip(spot, 300f);
                _spots.RemoveAt(0);
                _walk.Reset();
                return ChoreProgress.Running;
            }
            if (!_walk.Approach(ai, dt, spot, spot.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            int n = StewardSteps.PickUpDrops(h, spot.transform.position, _kinds, PickupRadius);
            _picked += n;
            VfhLog.D(LogCat.Smelter, "steward.tidy", ("hid", h.Hid), ("at", spot != null ? spot.transform.position : Vector3.zero), ("picked", n));
            if (_spots.Count > 0)
                _spots.RemoveAt(0);
            _walk.Reset();
            return ChoreProgress.Running;
        }

        // Put it all away now: the delivery rule takes it to chests that hold each item.
        private ChoreProgress Finish(Hireling h)
        {
            if (_picked > 0 && h.Zdo != null)
                h.Zdo.Set(HirelingZdo.DeliverPending, true);
            _spots.Clear();
            return ChoreProgress.Done;
        }

        public void Abort(Hireling h) => _spots.Clear();
    }
}
