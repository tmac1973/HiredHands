using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Fight by stance. Melee closes in and swings, guards with shields block incoming swings; archers keep 8–20 m and
    /// shoot, switching to their club when something gets within 6 m. Gives up when the target dies, runs past the
    /// leash, or nothing has happened for 10 s.
    /// </summary>
    internal sealed class CombatBehaviour : IHirelingBehaviour
    {
        private const float MeleeReach = 2.2f;
        private const float RangedMin = 8f;
        private const float RangedMax = 20f;
        private const float SidearmRange = 6f;
        private const float QuietSeconds = 10f;
        private const float BlockSeconds = 0.8f;

        private Character? _target;
        private float _lastAction;
        private float _blockUntil;
        private Character? _ignored;
        private float _ignoredUntil;

        public string Name => "Combat";
        public int Priority => 900;
        public Character? Target => _target;

        public bool Wants(HirelingAI ai)
        {
            if (ai.Retreating)
                return Drop(ai, "retreat");
            if (ThreatScanner.Alive(_target) && !Leashed(ai, _target!) && Time.time - _lastAction < QuietSeconds)
                return true;
            if (_target != null)
                Drop(ai, ThreatScanner.Alive(_target) ? "quiet or leashed" : "target down");

            Character? pick = Choose(ai);
            if (pick == null || (pick == _ignored && Time.time < _ignoredUntil))
                return false;
            _target = pick;
            _lastAction = Time.time;
            VfhLog.D(LogCat.Combat, "combat.engage", ("hid", ai.Hireling.Hid), ("target", pick.m_name), ("stance", ai.Stance),
                ("dist", Vector3.Distance(pick.transform.position, ai.transform.position)));
            return true;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Character target = _target!;
            Humanoid me = ai.Hireling.Humanoid;
            float dist = Vector3.Distance(target.transform.position, ai.transform.position) - target.GetRadius();
            if (ai.Hireling.Job == JobType.GuardRanged)
                Ranged(ai, me, target, dist, dt);
            else
                Melee(ai, me, target, dist, dt);
            if (Time.time > _blockUntil && me.m_blocking)
                me.m_blocking = false;
        }

        public void OnHit() => _lastAction = Time.time;

        private void Melee(HirelingAI ai, Humanoid me, Character target, float dist, float dt)
        {
            if (dist > MeleeReach)
            {
                ai.WalkTo(dt, target.transform.position, MeleeReach * 0.8f, run: true);
                return;
            }
            ai.Halt();
            ai.Face(target.GetCenterPoint());
            bool guard = ai.Hireling.Job == JobType.GuardMelee;
            if (guard && me.GetLeftItem() != null && target.InAttack() && dist < 4f)
            {
                me.m_blocking = true;
                _blockUntil = Time.time + BlockSeconds;
                return;
            }
            if (!me.m_blocking && ai.IsLookingAt(target.GetCenterPoint(), 20f) && ai.Attack(target))
                _lastAction = Time.time;
        }

        private void Ranged(HirelingAI ai, Humanoid me, Character target, float dist, float dt)
        {
            bool close = dist < SidearmRange;
            ai.Hireling.UseSidearm(close);
            if (close)
            {
                Melee(ai, me, target, dist, dt);
                return;
            }
            if (dist < RangedMin)
            {
                Vector3 away = (ai.transform.position - target.transform.position).normalized;
                ai.WalkTo(dt, ai.transform.position + away * 5f, 1f, run: true);
                return;
            }
            if (dist > RangedMax)
            {
                ai.WalkTo(dt, target.transform.position, RangedMax * 0.8f, run: true);
                return;
            }
            ai.Halt();
            ai.Face(target.GetCenterPoint());
            if (ai.IsLookingAt(target.GetCenterPoint(), 10f))
            {
                // Player bows only reach full power when drawn; NPCs never hold the button, so draw them fully.
                me.m_attackDrawTime = 10f;
                if (ai.Attack(target))
                    _lastAction = Time.time;
            }
        }

        private Character? Choose(HirelingAI ai)
        {
            ThreatScanner scan = ai.Threats;
            Vector3 home = ai.Hireling.Home;
            float radius = ai.Hireling.Radius;
            Character? threat = scan.Nearest;
            bool inRadius = threat != null && Vector3.Distance(threat.transform.position, home) <= radius;
            bool inExtended = threat != null && Vector3.Distance(threat.transform.position, home) <= radius + StanceRules.AggressiveExtraRange;
            Character? allyAttacker = AllyHits.RecentAttackerNear(ai.transform.position, 15f);
            CombatAction action = StanceRules.Decide(ai.Stance, threat != null ? scan.NearestDistance : null, inRadius, inExtended,
                scan.RecentlyAttacked, allyAttacker != null);
            if (action != CombatAction.Engage)
                return null;
            if (scan.RecentlyAttacked)
                return scan.LastAttacker;
            return threat ?? allyAttacker;
        }

        private bool Leashed(HirelingAI ai, Character target)
        {
            float limit = ai.Hireling.Radius + StanceRules.LeashBeyondRadius(ai.Stance);
            return Vector3.Distance(target.transform.position, ai.LeashCenter) > limit;
        }

        private bool Drop(HirelingAI ai, string reason)
        {
            if (_target != null)
            {
                VfhLog.D(LogCat.Combat, "combat.disengage", ("hid", ai.Hireling.Hid), ("target", _target.m_name), ("reason", reason));
                if (reason == "quiet or leashed" && ThreatScanner.Alive(_target))
                {
                    _ignored = _target;
                    _ignoredUntil = Time.time + 10f;
                }
            }
            _target = null;
            ai.Hireling.Humanoid.m_blocking = false;
            ai.Hireling.UseSidearm(false);
            return false;
        }
    }
}
