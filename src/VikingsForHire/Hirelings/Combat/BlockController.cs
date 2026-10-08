using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// A guard with a shield blocking on time (BlockAndDodge). For each attack the reader saw coming: roll the parry
    /// chance once and raise the shield just before the hit (0.15 s: inside vanilla's timed-block window, a parry) or a
    /// little earlier (0.45 s: a plain block). Attacks it didn't see get the 0.5.0 late block: shield up once the swing is
    /// under way. Plan() runs before the dodge controller (which may take an attack over), Act() after it.
    /// </summary>
    internal sealed class BlockController
    {
        private const float LowerAfterHit = 0.3f;
        private const float LateBlockRange = 4f;
        private const float LateBlockSeconds = 0.8f;
        /// <summary>A ready swing waits this long for a planned block at most, so a guard among busy enemies still hits back.</summary>
        public const float HoldSwingAhead = 0.6f;
        /// <summary>A projectile from further round than this can't be faced in time.</summary>
        private const float MaxTurnForProjectile = 120f;
        private const float LateProjectileSeconds = 0.4f;
        private const float LateProjectileBlockSeconds = 0.6f;
        private const float LateProjectileAngle = 60f;

        private readonly HirelingAI _ai;
        private bool _raised;
        private float _lateUntil = -1f;
        private bool _wasOn;

        public BlockController(HirelingAI ai) => _ai = ai;

        private Humanoid Me => _ai.Hireling.Humanoid;

        public bool HasShield => Me.GetLeftItem() is ItemDrop.ItemData left && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;

        /// <summary>Whether the controller is in charge of the shield (setting on and a shield in hand).</summary>
        public bool On => VfhConfig.BlockAndDodge.Value && HasShield;

        public void Plan()
        {
            bool on = On;
            if (!on)
            {
                if (_wasOn)
                    Reset(); // the setting went off (or the shield came off): hand back to the 0.5.0 code
                _wasOn = false;
                return;
            }
            _wasOn = true;
            HirelingLevelData level = _ai.Hireling.LevelData;
            foreach (IncomingAttack a in _ai.Incoming)
            {
                if (a.BlockPlanned || a.Dodging)
                    continue;
                a.BlockPlanned = true;
                if (!a.Read || !a.Timed)
                    continue; // left to the late block
                if (a.Projectile != null && Vector3.Angle(Flat(Me.transform.forward), a.From) > MaxTurnForProjectile)
                {
                    VfhLog.D(LogCat.Combat, "defense.block_skipped", ("hid", _ai.Hireling.Hid), ("why", "turn"));
                    continue; // left to the late block (if it ends up in front)
                }
                bool parry = DefenseRules.Roll(level.ParryChance, Random.value);
                a.ParryWon = parry;
                _ai.Defense.ParryRolls++;
                if (parry)
                    _ai.Defense.ParryWins++;
                a.RaiseAt = a.HitTime - (parry ? DefenseRules.ParryLeadSeconds : DefenseRules.EarlyBlockLeadSeconds);
                a.LowerAt = a.HitTime + LowerAfterHit;
            }
        }

        public void Act()
        {
            if (!On)
                return;
            float now = Time.time;
            Humanoid me = Me;
            // Projectile hit times are refined as they near: keep the raise the same lead before the hit.
            foreach (IncomingAttack a in _ai.Incoming)
                if (a.HasRaise && a.ParryWon is bool won && now < a.RaiseAt)
                {
                    a.RaiseAt = a.HitTime - (won ? DefenseRules.ParryLeadSeconds : DefenseRules.EarlyBlockLeadSeconds);
                    a.LowerAt = a.HitTime + LowerAfterHit;
                }
            IncomingAttack? next = NextPlanned(now);
            bool want = false;
            if (next != null)
            {
                if (now >= next.RaiseAt - 0.5f) // vanilla only blocks hits from in front
                {
                    if (next.Projectile != null)
                        _ai.Face(me.transform.position + next.From * 5f + Vector3.up * 1.2f);
                    else if (next.Attacker != null)
                        _ai.Face(next.Attacker.GetCenterPoint());
                }
                if (now >= next.RaiseAt && (now <= next.HitTime || _raised))
                {
                    if (me.InAttack() || me.IsStaggering())
                    {
                        if (!next.SkipLogged)
                        {
                            next.SkipLogged = true;
                            VfhLog.D(LogCat.Combat, "defense.block_skipped", ("hid", _ai.Hireling.Hid), ("why", me.InAttack() ? "mid_swing" : "staggered"));
                        }
                    }
                    else
                        want = true;
                }
            }

            // The 0.5.0 late block for swings it didn't see coming (or couldn't plan for).
            Character? target = _ai.CombatTarget;
            if (target != null && !target.IsDead() && target.InAttack() && !me.InAttack() && !PlannedFor(target, now) &&
                Vector3.Distance(target.transform.position, me.transform.position) - target.GetRadius() < LateBlockRange)
                _lateUntil = now + LateBlockSeconds;
            // ...and for projectiles it didn't see (or couldn't turn for) that end up just in front.
            foreach (IncomingAttack a in _ai.Incoming)
                if (a.Projectile != null && !a.HasRaise && !a.Dodging && a.HitTime - now < LateProjectileSeconds && a.HitTime > now &&
                    Vector3.Angle(Flat(me.transform.forward), a.From) < LateProjectileAngle)
                    _lateUntil = Mathf.Max(_lateUntil, now + LateProjectileBlockSeconds);
            if (now < _lateUntil && !me.InAttack())
                want = true;

            if (want != me.m_blocking)
            {
                me.m_blocking = want;
                if (want && next != null && now >= next.RaiseAt)
                    VfhLog.D(LogCat.Combat, "defense.raise", ("hid", _ai.Hireling.Hid), ("attacker", next.Attacker != null ? next.Attacker.m_name : ""),
                        ("parry", next.ParryWon), ("toHit", next.HitTime - now));
            }
            _raised = want;
        }

        /// <summary>A planned block is due soon or the shield is up for one: a ready swing waits (it would drop the shield).</summary>
        public bool HoldSwing()
        {
            if (!On)
                return false;
            float now = Time.time;
            IncomingAttack? next = NextPlanned(now);
            return next != null && now >= next.RaiseAt - HoldSwingAhead;
        }

        /// <summary>Shield down, plans kept (a roll is starting: attacks after it are still blocked).</summary>
        public void Lower()
        {
            Me.m_blocking = false;
            _raised = false;
            _lateUntil = -1f;
        }

        /// <summary>Shield down and every plan dropped (the fight is over, or a retreat starts).</summary>
        public void Reset()
        {
            if (_raised || Me.m_blocking)
                Me.m_blocking = false;
            _raised = false;
            _lateUntil = -1f;
            foreach (IncomingAttack a in _ai.Incoming)
                a.RaiseAt = a.LowerAt = float.NaN;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        private IncomingAttack? NextPlanned(float now)
        {
            foreach (IncomingAttack a in _ai.Incoming) // soonest first
                if (a.HasRaise && !a.Dodging && now <= a.LowerAt)
                    return a;
            return null;
        }

        private bool PlannedFor(Character attacker, float now)
        {
            foreach (IncomingAttack a in _ai.Incoming)
                if (a.Attacker == attacker && a.HasRaise && !a.Dodging && now <= a.LowerAt)
                    return true;
            return false;
        }
    }
}
