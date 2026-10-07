using System.Collections.Generic;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// A hireling watching for attacks coming at it (BlockAndDodge): enemies close by and facing it that have just started
    /// an attack. Each one is estimated (when it lands, how hard, area or not) and rolled against the level's read chance
    /// once, then kept in <see cref="Incoming"/> until shortly after it lands. Only runs while the hireling is fighting or
    /// was hit recently. Whom an enemy targets is only known on its owner's machine, so facing is the test.
    /// </summary>
    internal sealed class AttackReader
    {
        public const float WatchRange = 8f;
        public const float FacingAngle = 60f;
        private const float ScanInterval = 0.1f;
        private const float KeepAfterHit = 0.5f;
        private const float RecentHitSeconds = 10f;

        private readonly HirelingAI _ai;
        private readonly List<IncomingAttack> _incoming = new();
        // Per enemy: in an attack at the last scan, and which animator state, so a new swing (or the next in a combo) is one record.
        private readonly Dictionary<Character, int> _attackState = new();
        private float _nextScan;

        public AttackReader(HirelingAI ai) => _ai = ai;

        /// <summary>Attacks on their way, soonest first.</summary>
        public IReadOnlyList<IncomingAttack> Incoming => _incoming;

        public bool Active { get; private set; }

        public void Tick()
        {
            bool active = VfhConfig.BlockAndDodge.Value &&
                          (_ai.CombatTarget != null || Time.time - _ai.LastHitTime < RecentHitSeconds);
            if (!active)
            {
                if (Active)
                    Clear();
                Active = false;
                return;
            }
            Active = true;
            float now = Time.time;
            _incoming.RemoveAll(a => a.Attacker == null || now > a.HitTime + KeepAfterHit);
            if (now < _nextScan)
                return;
            _nextScan = now + ScanInterval;
            Scan(now);
        }

        public void Clear()
        {
            _incoming.Clear();
            _attackState.Clear();
        }

        private void Scan(float now)
        {
            Humanoid me = _ai.Hireling.Humanoid;
            Vector3 pos = me.transform.position;
            var seen = new HashSet<Character>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == me || c.IsDead() || c is Player || c.IsTamed() || Hireling.Of(c) != null)
                    continue;
                Vector3 toMe = pos - c.transform.position;
                if (toMe.sqrMagnitude > WatchRange * WatchRange || !BaseAI.IsEnemy(me, c))
                    continue;
                seen.Add(c);
                if (!c.InAttack())
                {
                    _attackState.Remove(c);
                    continue;
                }
                int state = StateHash(c);
                if (_attackState.TryGetValue(c, out int was) && was == state)
                    continue; // the same swing as last scan
                _attackState[c] = state;
                Vector3 flat = new Vector3(toMe.x, 0f, toMe.z);
                Vector3 fwd = c.transform.forward;
                fwd.y = 0f;
                if (flat.sqrMagnitude < 0.0001f || Vector3.Angle(fwd, flat) > FacingAngle)
                    continue; // swinging at someone else
                Add(c, now, -flat.normalized);
            }
            // Forget enemies that left the range.
            if (_attackState.Count > seen.Count)
            {
                var gone = new List<Character>();
                foreach (Character c in _attackState.Keys)
                    if (c == null || !seen.Contains(c))
                        gone.Add(c!);
                foreach (Character c in gone)
                    _attackState.Remove(c);
            }
        }

        private static int StateHash(Character c)
        {
            Animator a = c.m_animator;
            if (a == null)
                return 1;
            return a.IsInTransition(0) ? a.GetNextAnimatorStateInfo(0).fullPathHash : a.GetCurrentAnimatorStateInfo(0).fullPathHash;
        }

        private void Add(Character attacker, float now, Vector3 from)
        {
            Hireling h = _ai.Hireling;
            ItemDrop.ItemData.SharedData? weapon = AttackEstimate.Weapon(attacker);
            float? toHit = attacker.m_animator != null ? AttackEstimate.SecondsToHit(attacker.m_animator) : null;
            var a = new IncomingAttack
            {
                Attacker = attacker,
                HitTime = now + (toHit ?? AttackEstimate.UntimedSeconds),
                Timed = toHit != null,
                Damage = weapon != null ? AttackEstimate.Damage(attacker, weapon, h.Armor) : 0f,
                Area = weapon != null && AttackEstimate.Area(weapon),
                Read = DefenseRules.Roll(h.LevelData.ReadChance, Random.value),
                From = from,
                Weapon = weapon?.m_name ?? "",
            };
            int i = _incoming.FindIndex(x => x.HitTime > a.HitTime);
            _incoming.Insert(i < 0 ? _incoming.Count : i, a);
            if (a.Read)
                _ai.Defense.Reads++;
            else
                _ai.Defense.Misses++;
            VfhLog.D(LogCat.Combat, "defense.read", ("hid", h.Hid), ("attacker", attacker.m_name), ("weapon", a.Weapon), ("dmg", a.Damage),
                ("area", a.Area), ("in", a.HitTime - now), ("read", a.Read), ("timed", a.Timed), ("health", h.Humanoid.GetHealth()));
        }
    }
}
