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
    /// The Rancher slaughters the animal the ranch plan picks (the lowest-star adult above its kind's breeding pool, for a
    /// short meat order): walks up, swings his knife, kills it with one blow so vanilla drops its loot, then picks up
    /// everything it dropped. Delivering it is the usual delivery.
    /// </summary>
    internal sealed class SlaughterChore : IChore
    {
        private const float StuckSeconds = 25f;
        private const float SwingSeconds = 0.5f;
        private const float CollectSeconds = 3f;
        private const float CollectRadius = 4f;
        private const float SkipSeconds = 120f;

        /// <summary>Animals slaughtered by Ranchers since the last ranch_stats_reset (tests).</summary>
        public static int Slaughtered;

        private enum Step { Walk, Swing, Collect }

        private readonly WorkSteps _walk = new();
        private Character? _animal;
        private Step _step;
        private float _stepAt;
        private Vector3 _spot;
        private HiringBoard? _board;

        public ChoreKind Kind => ChoreKind.Slaughter;
        public string? Missing => null; // the reasons are the ranch plan's (per order)
        public float RestAfter { get; private set; }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            if (ctx.FreeSlots <= 0 || BoardOrders.BoardOf(ctx.Hireling.BoardId) is not HiringBoard board)
                return Enumerable.Empty<ChoreJob>();
            Core.Orders.HerdAnimal? pick = RanchState.For(board).Slaughter;
            if (pick == null)
                return Enumerable.Empty<ChoreJob>();
            Character? animal = RanchState.Herd(board).FirstOrDefault(f => f.Model.Id == pick.Id)?.Animal;
            if (animal == null || animal.IsDead() || Reservations.IsSkipped(animal) || Reservations.IsReservedByOther(animal, ctx.Hireling.Hid))
                return Enumerable.Empty<ChoreJob>();
            const float u = 0.55f; // below feeding (0.7), above tidying
            return new[]
            {
                new ChoreJob
                {
                    Kind = Kind, Target = animal, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, animal.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_ranch_slaughter", animal.m_name),
                },
            };
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            _animal = job.Target as Character;
            _board = BoardOrders.BoardOf(ctx.Hireling.BoardId);
            _step = Step.Walk;
            _stepAt = Time.time;
            _walk.Reset();
            RestAfter = 0f;
            if (_animal != null)
                Reservations.TryReserve(_animal, ctx.Hireling.Hid);
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            switch (_step)
            {
                case Step.Walk:
                    if (_animal == null || _animal.IsDead())
                        return End(h, ChoreProgress.Failed);
                    if (_walk.SinceProgress > StuckSeconds)
                    {
                        VfhLog.I(LogCat.Work, "ranch.slaughter_stuck", ("hid", h.Hid), ("animal", _animal.m_name), ("pos", h.transform.position));
                        Reservations.Skip(_animal, SkipSeconds);
                        return End(h, ChoreProgress.Failed);
                    }
                    if (!_walk.Approach(ai, dt, _animal, _animal.transform.position))
                        return ChoreProgress.Running;
                    ai.Halt();
                    ai.Face(_animal.GetCenterPoint());
                    ai.Attack(_animal); // the knife's swing
                    _step = Step.Swing;
                    _stepAt = Time.time;
                    return ChoreProgress.Running;
                case Step.Swing:
                    if (_animal == null || _animal.IsDead())
                        return End(h, ChoreProgress.Failed);
                    ai.Halt();
                    ai.Face(_animal.GetCenterPoint());
                    if (Time.time - _stepAt < SwingSeconds * RancherEnhancements.StepMult(h))
                        return ChoreProgress.Running;
                    Kill(h, _animal);
                    _spot = _animal.transform.position;
                    _step = Step.Collect;
                    _stepAt = Time.time;
                    return ChoreProgress.Running;
                default:
                    ai.Halt();
                    if (Time.time - _stepAt < CollectSeconds * RancherEnhancements.StepMult(h))
                        return ChoreProgress.Running;
                    int got = 0, left = 0;
                    var picked = new List<string>();
                    foreach (ItemDrop d in DropCollect.Near(_spot, CollectRadius, CollectSeconds + 5f))
                    {
                        string name = d.m_itemData?.m_dropPrefab != null ? d.m_itemData.m_dropPrefab.name : "";
                        if (DropCollect.PickUp(h, d))
                        {
                            got++;
                            if (name.Length > 0)
                                picked.Add(name);
                        }
                        else
                            left++;
                    }
                    if (left > 0)
                        VfhLog.I(LogCat.Work, "ranch.left_on_ground", ("hid", h.Hid), ("items", left), ("pos", _spot));
                    RancherEnhancements.Slaughtered(h, picked);
                    VfhLog.I(LogCat.Work, "ranch.collected", ("hid", h.Hid), ("items", got));
                    // Straight to the chests: meat is an ingredient, not a product, so it would wait as a leftover.
                    if (got > 0)
                        h.Zdo?.Set(HirelingZdo.DeliverPending, true);
                    if (_board != null)
                        RanchState.Invalidate(_board);
                    return End(h, ChoreProgress.Done);
            }
        }

        // One blow from the Rancher worth more than it has left (true damage, past armour and resistances): vanilla then
        // kills it and drops its loot (each star doubling it). DamagePatches lets a Rancher's hits on tames through.
        private static void Kill(Hireling h, Character animal)
        {
            var hit = new HitData { m_point = animal.GetCenterPoint(), m_dir = (animal.transform.position - h.transform.position).normalized, m_toolTier = 100 };
            hit.m_damage.m_damage = animal.GetMaxHealth() * 2f + 100f;
            hit.SetAttacker(h.Humanoid);
            animal.Damage(hit);
            Slaughtered++;
            VfhLog.I(LogCat.Work, "ranch.slaughter", ("hid", h.Hid), ("animal", Utils.GetPrefabName(animal.gameObject)), ("stars", animal.GetLevel() - 1));
        }

        private ChoreProgress End(Hireling h, ChoreProgress result)
        {
            if (_animal != null)
                Reservations.Release(_animal, h.Hid);
            _animal = null;
            RestAfter = result == ChoreProgress.Failed ? 3f : 0f;
            return result;
        }

        public void Abort(Hireling h) => End(h, ChoreProgress.Failed);
    }
}
