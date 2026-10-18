using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Chores;
using static VikingsForHire.Core.Enhancements.EnhancementId;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>
    /// Herder (an enhancement): the Rancher fetches an animal of the herd that's out of its pen and leads it home. It walks
    /// up to it, the animal follows (as a tame follows a player), and it walks to the animal's pen (its Pen sign, or where it
    /// was last walled in), waiting when the animal lags and going back for it when it's lost; the animal is let go once
    /// it's in its pen. Tim's boar got out into the basement, and Herder only ever made an escapee tag along.
    /// </summary>
    internal sealed class LeadBackChore : IChore
    {
        private const float Reach = 2.5f;
        private const float LagWait = 10f;
        private const float Lost = 25f;
        private const float GiveUpSeconds = 240f;
        private const float SkipSeconds = 600f;
        private const float StuckSeconds = 30f;

        private readonly WorkSteps _walk = new();
        private Character? _animal;
        private Vector3 _home;
        private bool _leading;
        private float _started;
        private HiringBoard? _board;

        public ChoreKind Kind => ChoreKind.LeadBack;
        public string? Missing => null;
        public float RestAfter { get; private set; }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Hireling h = ctx.Hireling;
            if (!h.Enh.Has(Herder) || BoardOrders.BoardOf(h.BoardId) is not HiringBoard board)
                return Enumerable.Empty<ChoreJob>();
            HerdScan.Found? best = RanchState.Herd(board)
                .Where(f => f.Model.Escaped && f.Animal != null && !f.Animal.IsDead() && !Reservations.IsSkipped(f.Animal) &&
                            HerdScan.HomeOf(f.Animal.m_nview.GetZDO().m_uid) != null)
                .OrderBy(f => Vector3.Distance(f.Animal.transform.position, ctx.Position)).FirstOrDefault();
            if (best == null)
                return Enumerable.Empty<ChoreJob>();
            const float u = 0.5f;
            return new[]
            {
                new ChoreJob
                {
                    Kind = Kind, Target = best.Animal, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, best.Animal.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_ranch_lead_back", best.Animal.m_name),
                },
            };
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            _animal = job.Target as Character;
            _board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            _home = _animal != null && HerdScan.HomeOf(_animal.m_nview.GetZDO().m_uid) is Vector3 home ? home : ctx.Hireling.WorkCentre;
            _leading = false;
            _started = Time.time;
            _walk.Reset();
            RestAfter = 0f;
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_animal == null || _animal.IsDead() || _animal.m_nview == null || !_animal.m_nview.IsValid())
                return End(ai, ChoreProgress.Done);
            Vector3 at = _animal.transform.position;
            if (HerdScan.Penned(at) && Vector3.Distance(at, _home) <= 40f)
            {
                EnhancementEvents.Fire(h, "HerderHome", LogCat.Work, ("animal", _animal.m_name));
                return End(ai, ChoreProgress.Done);
            }
            if (Time.time - _started > GiveUpSeconds)
            {
                VfhLog.I(LogCat.Work, "ranch.lead_gave_up", ("hid", h.Hid), ("animal", _animal.m_name), ("at", at), ("home", _home));
                Reservations.Skip(_animal, SkipSeconds);
                return End(ai, ChoreProgress.Failed);
            }
            float apart = Vector3.Distance(at, ai.transform.position);
            if (!_leading || apart > Lost)
            {
                // Go and get it (again, if it was lost on the way).
                _leading = false;
                if (_walk.SinceProgress > StuckSeconds)
                {
                    Reservations.Skip(_animal, SkipSeconds);
                    return End(ai, ChoreProgress.Failed);
                }
                if (!_walk.Approach(ai, dt, _animal, at, Reach))
                    return ChoreProgress.Running;
                RancherTicker.Lead(_animal, h);
                ai.Leading = _animal;
                _leading = true;
                EnhancementEvents.Fire(h, "Herder", LogCat.Work, ("animal", _animal.m_name), ("home", _home));
                return ChoreProgress.Running;
            }
            if (apart > LagWait)
            {
                ai.Halt();
                ai.Face(at);
                return ChoreProgress.Running;
            }
            // Into the pen, a little past the sign, so the animal behind ends up inside.
            if (ai.WalkTo(dt, _home, 1.5f, run: false))
                ai.Halt();
            return ChoreProgress.Running;
        }

        private ChoreProgress End(HirelingAI ai, ChoreProgress result)
        {
            if (_animal != null)
                RancherTicker.Let(_animal);
            ai.Leading = null;
            _animal = null;
            if (_board != null)
                RanchState.Invalidate(_board);
            RestAfter = result == ChoreProgress.Failed ? 3f : 0f;
            return result;
        }

        public void Abort(Hireling h) => End(h.Ai, ChoreProgress.Failed);
    }
}
