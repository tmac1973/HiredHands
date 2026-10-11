using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>
    /// While an egg order is short the Rancher picks up eggs lying in his area, nearest first; once it's met he leaves the
    /// rest to hatch (vanilla hatches an egg left warm and under a roof). The usual delivery takes them to the chests.
    /// </summary>
    internal sealed class EggsChore : IChore
    {
        private const float StuckSeconds = 20f;
        private const float SkipSeconds = 120f;

        private readonly WorkSteps _walk = new();
        private ItemDrop? _egg;
        private HiringBoard? _board;

        public ChoreKind Kind => ChoreKind.Eggs;
        public string? Missing => null; // the egg order's reasons are the ranch plan's
        public float RestAfter { get; private set; }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            if (ctx.FreeSlots <= 0 || BoardOrders.BoardOf(ctx.Hireling.BoardId) is not HiringBoard board)
                return Enumerable.Empty<ChoreJob>();
            HashSet<string> wanted = RanchState.For(board).CollectEggs;
            if (wanted.Count == 0)
                return Enumerable.Empty<ChoreJob>();
            ItemDrop? nearest = ItemDrop.s_instances
                .Where(d => d != null && d.m_nview != null && d.m_nview.IsValid() && d.m_itemData?.m_dropPrefab != null &&
                            wanted.Contains(d.m_itemData.m_dropPrefab.name) && !d.m_itemData.m_customData.ContainsKey(DropPile.Tag) &&
                            d.m_nview.GetZDO()?.GetBool(Steward.AnimalsChore.FeedKey) != true &&
                            Utils.DistanceXZ(d.transform.position, ctx.Home) <= ctx.Radius && ctx.BoardOwnerMayUse(d.transform.position) &&
                            !Reservations.IsSkipped(d) && !Reservations.IsReservedByOther(d, ctx.Hireling.Hid))
                .OrderBy(d => Vector3.Distance(d.transform.position, ctx.Position)).FirstOrDefault();
            if (nearest == null)
                return Enumerable.Empty<ChoreJob>();
            const float u = 0.4f;
            return new[]
            {
                new ChoreJob
                {
                    Kind = Kind, Target = nearest, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, nearest.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_ranch_eggs", WorkSteps.SharedName(nearest.m_itemData.m_dropPrefab.name)),
                },
            };
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            _egg = job.Target as ItemDrop;
            _board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            _walk.Reset();
            RestAfter = 0f;
            if (_egg != null)
                Reservations.TryReserve(_egg, ctx.Hireling.Hid);
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_egg == null || _egg.m_nview == null || !_egg.m_nview.IsValid())
                return End(h, ChoreProgress.Done); // gone (hatched, or someone took it)
            if (_walk.SinceProgress > StuckSeconds)
            {
                Reservations.Skip(_egg, SkipSeconds);
                return End(h, ChoreProgress.Failed);
            }
            if (!_walk.Approach(ai, dt, _egg, _egg.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            bool got = DropCollect.PickUp(h, _egg);
            VfhLog.D(LogCat.Work, "ranch.egg", ("hid", h.Hid), ("got", got));
            if (_board != null)
                RanchState.Invalidate(_board);
            return End(h, got ? ChoreProgress.Done : ChoreProgress.Failed);
        }

        private ChoreProgress End(Hireling h, ChoreProgress result)
        {
            if (_egg != null)
                Reservations.Release(_egg, h.Hid);
            _egg = null;
            RestAfter = result == ChoreProgress.Failed ? 3f : 0f;
            return result;
        }

        public void Abort(Hireling h) => End(h, ChoreProgress.Failed);
    }
}
