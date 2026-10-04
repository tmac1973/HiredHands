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
        private float _nextAttack;
        private Character? _ignored;
        private float _engagedAt = -1f;
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
            _engagedAt = Time.time;
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

        /// <summary>
        /// Vanilla paces monster attacks inside MonsterAI.UpdateAI, which hirelings don't run, so the pace is set here.
        /// </summary>
        private void Attacked(float cooldown)
        {
            if (_engagedAt > 0f)
            {
                VfhLog.D(LogCat.Combat, "combat.first_swing", ("target", _target != null ? _target.m_name : ""), ("secondsAfterEngage", Time.time - _engagedAt));
                _engagedAt = -1f;
            }
            _lastAction = Time.time;
            _nextAttack = Time.time + cooldown;
        }

        private void Melee(HirelingAI ai, Humanoid me, Character target, float dist, float dt)
        {
            if (dist > MeleeReach)
            {
                // Stop at the target's edge, not its centre: walking to within reach of a troll's centre means walking
                // into the troll, which never ends, so the hireling never got close enough to swing.
                ai.WalkTo(dt, target.transform.position, target.GetRadius() + MeleeReach * 0.7f, run: true);
                return;
            }
            ai.Halt();
            ai.Face(target.GetCenterPoint());
            // Swing whenever the swing is ready; raise the shield only in between. Blocking first meant a guard facing
            // a fast or busy enemy (nearly always mid-attack) held its shield up and never hit back.
            if (Time.time >= _nextAttack && ai.IsLookingAt(target.GetCenterPoint(), 35f))
            {
                me.m_blocking = false;
                if (ai.Attack(target))
                {
                    Attacked(Config.VfhConfig.MeleeAttackCooldown.Value);
                    return;
                }
            }
            bool guard = ai.Hireling.Job == JobType.GuardMelee;
            if (guard && me.GetLeftItem() != null && target.InAttack() && dist < 4f && !me.InAttack())
            {
                me.m_blocking = true;
                _blockUntil = Time.time + BlockSeconds;
            }
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
            if (Time.time >= _nextAttack && ai.IsLookingAt(target.GetCenterPoint(), 15f))
            {
                // Player bows only reach full power when drawn; NPCs never hold the button, so draw them fully.
                me.m_attackDrawTime = 10f;
                if (ai.Attack(target))
                {
                    float draw = me.GetCurrentWeapon()?.m_shared.m_attack.m_drawDurationMin ?? 0f;
                    Attacked(Mathf.Max(Config.VfhConfig.RangedAttackCooldown.Value, draw + 0.8f));
                }
            }
        }

        private Character? Choose(HirelingAI ai)
        {
            ThreatScanner scan = ai.Threats;
            Vector3 home = ai.LeashCenter;
            float radius = ai.LeashRadius;
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
            float limit = ai.LeashRadius + StanceRules.LeashBeyondRadius(ai.Stance);
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
