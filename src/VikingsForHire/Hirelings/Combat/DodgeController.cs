using System.Collections.Generic;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Any hireling rolling out of the way of an attack it saw coming that would hurt a lot (BlockAndDodge): over a quarter
    /// of its health after armor (and after its shield, if it was going to block), or an area attack. Limited by the
    /// level's dodge chance and cooldown, and only towards safe ground (no drops, deep water or walls). The roll is the
    /// player's: animation, root motion and invincibility (DodgePatches). Decided right after the block controller plans,
    /// so a dodge replaces the planned block before any shield goes up.
    /// </summary>
    internal sealed class DodgeController
    {
        private const float RollDistance = 3f;
        private const float MaxStep = 1.2f;
        private const float MaxWater = 0.4f;
        private const float RollTimeout = 1.2f;
        private const float CountAfterHit = 0.3f;
        private static readonly int GroundMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
        private static EffectList? _effects;

        private readonly HirelingAI _ai;
        private float _lastDodge = -999f;
        private bool _leftFirst;
        private IncomingAttack? _pending;
        private float _startAt;
        private bool _rolling;
        private float _rollStarted;
        private bool _sawRoll;
        private float _lastDamage = -999f;
        private IncomingAttack? _counting;

        public DodgeController(HirelingAI ai) => _ai = ai;

        /// <summary>Hits miss while true (read by DodgePatches on this machine; the ZDO carries it to others).</summary>
        public bool Invincible { get; private set; }

        private Humanoid Me => _ai.Hireling.Humanoid;

        public void OnDamaged() => _lastDamage = Time.time;

        /// <summary>A roll is coming up: don't start a swing or a shot (vanilla won't roll mid-attack).</summary>
        public bool HoldAttack => _pending != null && !_rolling;

        public void Tick()
        {
            float now = Time.time;
            UpdateRoll(now);
            if (!VfhConfig.BlockAndDodge.Value)
            {
                _pending = null; // a roll under way finishes; no new ones
                return;
            }
            Decide(now);
            if (_pending != null && !_rolling && now >= _startAt)
                Start(now);
        }

        private void Decide(float now)
        {
            Hireling h = _ai.Hireling;
            HirelingLevelData level = h.LevelData;
            foreach (IncomingAttack a in _ai.Incoming)
            {
                if (a.DodgeDecided)
                    continue;
                a.DodgeDecided = true;
                if (!a.Read || !a.Timed || !a.Dodgeable)
                    continue;
                string? why = null;
                if (!DefenseRules.WouldHurtALot(DamageLeft(a), Me.GetHealth(), a.Area))
                    why = "small";
                else if (_pending != null || _rolling || !DefenseRules.DodgeReady(now, _lastDodge, level.DodgeCooldown))
                    why = "cooldown";
                else if (!Me.IsOnGround() || Me.IsAttached() || Me.IsSwimming())
                    why = "state";
                else if (a.HitTime - now < DefenseRules.DodgeLatestSeconds)
                    why = "late";
                else
                {
                    _ai.Defense.DodgeRolls++;
                    if (!DefenseRules.Roll(level.DodgeChance, Random.value))
                        why = "roll";
                    else
                    {
                        _ai.Defense.DodgeWins++;
                        if (Direction(a) is not Vector3 dir)
                        {
                            VfhLog.D(LogCat.Combat, "defense.dodge_blocked_by_ground", ("hid", h.Hid), ("attacker", Name(a)));
                            continue;
                        }
                        a.DodgeDir = dir;
                        a.Dodging = true;
                        a.RaiseAt = a.LowerAt = float.NaN; // no shield for this one: it rolls
                        _pending = a;
                        _startAt = Mathf.Max(now, a.HitTime - DefenseRules.DodgeLeadSeconds);
                        continue;
                    }
                }
                if (why != "small")
                    VfhLog.D(LogCat.Combat, "defense.no_dodge", ("hid", h.Hid), ("attacker", Name(a)), ("why", why), ("dmg", a.Damage));
            }
        }

        // What the hit would leave after the shield, if a block is planned (a likely parry counts its bonus).
        private float DamageLeft(IncomingAttack a)
        {
            if (!a.HasRaise || Me.GetCurrentBlocker() is not ItemDrop.ItemData shield)
                return a.Damage;
            float power = shield.GetBlockPower(0f) * (a.ParryWon == true ? Mathf.Max(1f, shield.m_shared.m_timedBlockBonus) : 1f);
            HitData.DamageTypes left = a.Types.Clone();
            left.ApplyArmor(power);
            return left.GetTotalDamage();
        }

        private Vector3? Direction(IncomingAttack a)
        {
            (float X, float Z)[] dirs = DefenseRules.DodgeDirections(a.From.x, a.From.z, a.Area, a.Projectile != null, _leftFirst);
            foreach ((float x, float z) in dirs)
            {
                var d = new Vector3(x, 0f, z);
                if (Safe(d))
                {
                    if (a.Projectile == null && !a.Area)
                        _leftFirst = !_leftFirst;
                    return d;
                }
            }
            return null;
        }

        // Ground about the same height 3 m out (no ledge), no deep water there, and nothing in the way.
        private bool Safe(Vector3 dir)
        {
            Vector3 pos = Me.transform.position;
            Vector3 end = pos + dir * RollDistance;
            if (!Physics.Raycast(end + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 4f, GroundMask) ||
                Mathf.Abs(ground.point.y - pos.y) > MaxStep)
                return false;
            if (Floating.GetLiquidLevel(ground.point) - ground.point.y > MaxWater)
                return false;
            return PathClear(dir);
        }

        private bool PathClear(Vector3 dir)
        {
            Vector3 pos = Me.transform.position;
            float r = Mathf.Max(0.3f, Me.GetRadius() * 0.8f);
            Vector3 p1 = pos + Vector3.up * (r + 0.3f), p2 = pos + Vector3.up * 1.5f;
            return !Physics.CapsuleCast(p1, p2, r, dir, RollDistance, GroundMask);
        }

        private void Start(float now)
        {
            IncomingAttack a = _pending!;
            Humanoid me = Me;
            // Mid-swing or drawing a bow when the time comes: wait for it to end while there's still time.
            if (me.InAttack() && a.HitTime - now >= DefenseRules.DodgeLatestSeconds)
                return;
            _pending = null;
            if (me.InAttack() || me.IsStaggering() || !me.IsOnGround() || !PathClear(a.DodgeDir))
            {
                a.Dodging = false; // back to the late block
                VfhLog.D(LogCat.Combat, "defense.no_dodge", ("hid", _ai.Hireling.Hid), ("attacker", Name(a)),
                    ("why", me.InAttack() ? "mid_swing" : me.IsStaggering() ? "staggered" : "path"), ("dmg", a.Damage));
                return;
            }
            _ai.Block.Lower();
            _ai.StopMoving();
            Quaternion rot = Quaternion.LookRotation(a.DodgeDir);
            me.transform.rotation = rot;
            if (me.m_body != null)
                me.m_body.rotation = rot;
            me.m_zanim.SetTrigger("dodge");
            SetInvincible(true);
            Effects()?.Create(me.transform.position, Quaternion.identity, me.transform);
            me.AddNoise(5f);
            _rolling = true;
            _sawRoll = false;
            _rollStarted = now;
            _lastDodge = now;
            _counting = a;
            _ai.Defense.Dodges++;
            VfhLog.D(LogCat.Combat, "defense.dodge", ("hid", _ai.Hireling.Hid), ("attacker", Name(a)), ("dmg", a.Damage), ("area", a.Area),
                ("proj", a.Projectile != null), ("dir", a.DodgeDir), ("toHit", a.HitTime - now));
        }

        private void UpdateRoll(float now)
        {
            if (_rolling)
            {
                bool inRoll = DodgePatches.AnimInDodge(Me);
                if (inRoll)
                    _sawRoll = true;
                if ((_sawRoll && !inRoll) || now - _rollStarted > RollTimeout)
                {
                    if (!_sawRoll)
                        VfhLog.W(LogCat.Combat, "defense.dodge_timeout", ("hid", _ai.Hireling.Hid));
                    _rolling = false;
                    SetInvincible(false);
                }
            }
            if (_counting != null && now > _counting.HitTime + CountAfterHit)
            {
                if (_lastDamage < _rollStarted)
                    _ai.Defense.DodgedHits++;
                _counting = null;
            }
        }

        private void SetInvincible(bool on)
        {
            Invincible = on;
            ZNetView nview = Me.m_nview;
            if (nview != null && nview.IsValid() && nview.IsOwner())
                nview.GetZDO().Set(ZDOVars.s_dodgeinv, on);
        }

        private static EffectList? Effects()
        {
            if (_effects == null && ZNetScene.instance?.GetPrefab("Player") is GameObject player && player.GetComponent<Player>() is Player p)
                _effects = p.m_dodgeEffects;
            return _effects;
        }

        private static string Name(IncomingAttack a) => a.Attacker != null ? a.Attacker.m_name : "?";
    }
}
