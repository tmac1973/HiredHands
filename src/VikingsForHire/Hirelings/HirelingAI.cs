using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// A MonsterAI subclass (so later phases can use its attack/follow helpers) whose brain is replaced: UpdateAI keeps
    /// BaseAI's housekeeping (owner check, timers, health regeneration) and then runs the highest-priority behaviour that
    /// wants control. The monster logic (targeting, aggression, despawning) never runs.
    /// </summary>
    internal sealed class HirelingAI : MonsterAI
    {
        private readonly List<IHirelingBehaviour> _behaviours = new();
        private IHirelingBehaviour? _current;

        public Hireling Hireling { get; private set; } = null!;

        public string CurrentBehaviour => _current?.Name ?? "none";

        // No Awake override: the reference assembly is publicized, so MonsterAI.Awake looks public at compile time but is
        // protected at runtime. Hireling.Awake calls Init instead.
        public void Init(Hireling hireling)
        {
            if (Hireling != null)
                return;
            Hireling = hireling;
            Add(new IdleBehaviour());
        }

        /// <summary>Later phases register job, combat and follow behaviours here.</summary>
        public void Add(IHirelingBehaviour behaviour)
        {
            _behaviours.Add(behaviour);
            _behaviours.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public override bool UpdateAI(float dt)
        {
            if (Hireling == null || !m_nview.IsValid() || !m_nview.IsOwner())
                return false;
            if (m_randomMoveUpdateTimer > 0f)
                m_randomMoveUpdateTimer -= dt;
            m_timeSinceHurt += dt;
            UpdateRegeneration(dt);

            VfhLog.Guard(LogCat.AI, "ai.tick_failed", () =>
            {
                IHirelingBehaviour next = _behaviours.First(b => b.Wants(this));
                if (next != _current)
                {
                    VfhLog.D(LogCat.AI, "ai.switch", ("hid", Hireling.Hid), ("from", CurrentBehaviour), ("to", next.Name));
                    _current = next;
                }
                next.Tick(this, dt);
            }, ("hid", Hireling.Hid));
            return true;
        }

        public bool WalkTo(float dt, Vector3 point, float stopDistance, bool run) => MoveTo(dt, point, stopDistance, run);

        public void Wander(float dt, Vector3 center) => RandomMovement(dt, center, snapToGround: true);

        public void Halt() => StopMoving();
    }
}
