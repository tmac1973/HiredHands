using System.Collections.Generic;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// A hireling watching for attacks coming at it (CombatSkill): enemies close by and facing it that have just started
    /// an attack. Each one is estimated (when it lands, how hard, area or not) and rolled against the level's read chance
    /// once, then kept in <see cref="Incoming"/> until shortly after it lands. Only runs while the hireling is fighting or
    /// was hit recently. Whom an enemy targets is only known on its owner's machine, so facing is the test.
    /// </summary>
    internal sealed class AttackReader
    {
        public const float WatchRange = 8f;
        public const float FacingAngle = 60f;
        public const float ProjectileRange = 30f;
        private const float ProjectileMinSeconds = 0.05f;
        private const float ProjectileMaxSeconds = 1.5f;
        private const float ScanInterval = 0.1f;
        private const float KeepAfterHit = 0.5f;
        private const float RecentHitSeconds = 10f;

        private readonly HirelingAI _ai;
        private readonly List<IncomingAttack> _incoming = new();
        // Per enemy: in an attack at the last scan, and which animator state, so a new swing (or the next in a combo) is one record.
        private readonly Dictionary<Character, int> _attackState = new();
        // Projectiles already read (each is read and rolled once; its hit time keeps updating).
        private readonly Dictionary<Projectile, IncomingAttack> _projectiles = new();
        private readonly HashSet<Projectile> _traced = new(); // debug lines once per projectile
        private float _nextScan;

        public AttackReader(HirelingAI ai) => _ai = ai;

        /// <summary>Attacks on their way, soonest first.</summary>
        public IReadOnlyList<IncomingAttack> Incoming => _incoming;

        public bool Active { get; private set; }

        public void Tick()
        {
            if (!VfhConfig.CombatSkillOn)
            {
                if (Active || _incoming.Count > 0)
                    Clear();
                Active = false;
                return;
            }
            float now = Time.time;
            _incoming.RemoveAll(a => (a.Projectile == null && a.Attacker == null) || now > a.HitTime + KeepAfterHit);
            _incoming.Sort((x, y) => x.HitTime.CompareTo(y.HitTime)); // projectile hit times move
            // Projectiles always (cheap: a few in flight, tracked once a frame for everyone), and every tick: an arrow
            // from close by arrives within a quarter of a second. An idle hireling sees arrows coming before it has
            // noticed the archer.
            ScanProjectiles(now);
            // Swings only while fighting or just hit, every 0.1 s.
            bool active = _ai.CombatTarget != null || now - _ai.LastHitTime < RecentHitSeconds || _ai.Threats.NearestDistance < WatchRange;
            if (!active)
            {
                if (Active)
                    _attackState.Clear();
                Active = false;
                return;
            }
            Active = true;
            if (now < _nextScan)
                return;
            _nextScan = now + ScanInterval;
            Scan(now);
        }

        public void Clear()
        {
            _incoming.Clear();
            _attackState.Clear();
            _projectiles.Clear();
            _traced.Clear();
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
                if (SwingPending(c, now) is IncomingAttack same)
                {
                    // The swing moved on to its next animation state before landing: still the same swing. A windup
                    // state has no hit event; the strike state does: now its hit time is known.
                    if (!same.Timed && c.m_animator != null && AttackEstimate.SecondsToHit(c.m_animator) is float toHit)
                    {
                        same.HitTime = now + toHit + HitTiming.Offset(same.TimingKey);
                        same.Timed = true;
                        same.BlockPlanned = false; // plan again with the real time
                        same.DodgeDecided = false;
                        same.RaiseAt = same.LowerAt = float.NaN;
                        VfhLog.D(LogCat.Combat, "defense.timed", ("hid", _ai.Hireling.Hid), ("attacker", c.m_name), ("in", toHit));
                    }
                    continue;
                }
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

        private void ScanProjectiles(float now)
        {
            Hireling h = _ai.Hireling;
            Vector3 chest = h.Humanoid.GetCenterPoint();
            float hitRadius = 1f + h.Humanoid.GetRadius();
            foreach (KeyValuePair<Projectile, ProjectileWatch.Track> kv in ProjectileWatch.Live())
            {
                Projectile p = kv.Key;
                ProjectileWatch.Track t = kv.Value;
                if (p == null || (t.LastPos - chest).sqrMagnitude > ProjectileRange * ProjectileRange)
                    continue;
                if (t.Friendly || !t.Moved)
                {
                    if (VfhLog.Enabled(LogLevel.Debug, LogCat.Combat) && _traced.Add(p))
                        VfhLog.D(LogCat.Combat, "defense.proj_skip", ("hid", h.Hid), ("proj", Utils.GetPrefabName(p.gameObject)), ("friendly", t.Friendly),
                            ("moved", t.Moved), ("shooter", t.Shooter != null ? t.Shooter.m_name : "?"));
                    continue;
                }
                (float miss, float time) = DefenseRules.ClosestApproach(t.LastPos.x, t.LastPos.y, t.LastPos.z,
                    t.Velocity.x, t.Velocity.y, t.Velocity.z, chest.x, chest.y, chest.z);
                bool coming = miss < hitRadius && time > ProjectileMinSeconds && time < ProjectileMaxSeconds;
                if (!_projectiles.ContainsKey(p) && VfhLog.Enabled(LogLevel.Debug, LogCat.Combat) && _traced.Add(p))
                    VfhLog.D(LogCat.Combat, "defense.proj_seen", ("hid", h.Hid), ("proj", Utils.GetPrefabName(p.gameObject)), ("miss", miss), ("in", time),
                        ("speed", t.Velocity.magnitude), ("shooter", t.Shooter != null ? t.Shooter.m_name : "?"));
                if (_projectiles.TryGetValue(p, out IncomingAttack known))
                {
                    if (coming)
                        known.HitTime = now + time; // arcs drop: the estimate improves as it nears
                    continue;
                }
                if (!coming)
                    continue;
                Vector3 back = -t.Velocity;
                back.y = 0f;
                var a = new IncomingAttack
                {
                    Attacker = t.Shooter!,
                    Projectile = p,
                    HitTime = now + time,
                    Timed = true,
                    Types = AttackEstimate.ProjectileTypes(p, t.Shooter, h.Armor, h.Humanoid.GetMaxHealth()),
                    Dodgeable = p.m_dodgeable,
                    Area = p.m_aoe > 0f,
                    Read = DefenseRules.Roll(VfhConfig.SkillChance(CombatSkillKind.Read, h.LevelData.ReadChance), Random.value),
                    From = back.sqrMagnitude > 0.0001f ? back.normalized : -h.transform.forward,
                    Weapon = Utils.GetPrefabName(p.gameObject),
                };
                _projectiles[p] = a;
                Insert(a);
                _ai.Defense.ProjReads++;
                Count(a);
                VfhLog.D(LogCat.Combat, "defense.read", ("hid", h.Hid), ("attacker", t.Shooter != null ? t.Shooter.m_name : "?"), ("proj", a.Weapon),
                    ("dmg", a.Damage), ("area", a.Area), ("in", time), ("read", a.Read), ("timed", true), ("health", h.Humanoid.GetHealth()));
            }
            if (_projectiles.Count > 0)
            {
                var gone = new List<Projectile>();
                foreach (KeyValuePair<Projectile, IncomingAttack> kv in _projectiles)
                    if (kv.Key == null || now > kv.Value.HitTime + KeepAfterHit)
                        gone.Add(kv.Key!);
                foreach (Projectile p in gone)
                    _projectiles.Remove(p);
            }
            if (_traced.Count > 64)
                _traced.RemoveWhere(p => p == null);
        }

        private void Insert(IncomingAttack a)
        {
            a.Damage = a.Types.GetTotalDamage();
            int i = _incoming.FindIndex(x => x.HitTime > a.HitTime);
            _incoming.Insert(i < 0 ? _incoming.Count : i, a);
        }

        private void Count(IncomingAttack a)
        {
            if (a.Read)
                _ai.Defense.Reads++;
            else
                _ai.Defense.Misses++;
        }

        /// <summary>A close hit from <paramref name="attacker"/> landed (or was blocked) now: learn how far off its read was.</summary>
        public void ObserveHit(Character? attacker)
        {
            if (attacker == null)
                return;
            float now = Time.time;
            IncomingAttack? best = null;
            foreach (IncomingAttack a in _incoming)
                if (a.Projectile == null && a.Attacker == attacker && a.Timed && !a.Observed && Mathf.Abs(now - a.HitTime) < 1.5f &&
                    (best == null || Mathf.Abs(now - a.HitTime) < Mathf.Abs(now - best.HitTime)))
                    best = a;
            if (best == null)
                return;
            best.Observed = true;
            HitTiming.Learn(best.TimingKey, now - best.HitTime);
        }

        private IncomingAttack? SwingPending(Character c, float now)
        {
            foreach (IncomingAttack a in _incoming)
                if (a.Projectile == null && a.Attacker == c && now < a.HitTime + 0.1f)
                    return a;
            return null;
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
            // A throw or a shot: what's dangerous is the projectile, read in flight (ScanProjectiles), not the windup.
            if (weapon?.m_attack != null && weapon.m_attack.m_attackType == Attack.AttackType.Projectile)
                return;
            float? toHit = attacker.m_animator != null ? AttackEstimate.SecondsToHit(attacker.m_animator) : null;
            var a = new IncomingAttack
            {
                Attacker = attacker,
                HitTime = now + (toHit ?? AttackEstimate.UntimedSeconds),
                Timed = toHit != null,
                Types = weapon != null ? AttackEstimate.Types(attacker, weapon, h.Armor) : default,
                Dodgeable = weapon?.m_dodgeable ?? true,
                Area = weapon != null && AttackEstimate.Area(weapon),
                Read = DefenseRules.Roll(VfhConfig.SkillChance(CombatSkillKind.Read, h.LevelData.ReadChance), Random.value),
                From = from,
                Weapon = weapon?.m_name ?? "",
            };
            a.TimingKey = HitTiming.Key(attacker, a.Weapon);
            if (a.Timed)
                a.HitTime += HitTiming.Offset(a.TimingKey);
            Insert(a);
            Count(a);
            VfhLog.D(LogCat.Combat, "defense.read", ("hid", h.Hid), ("attacker", attacker.m_name), ("weapon", a.Weapon), ("dmg", a.Damage),
                ("area", a.Area), ("in", a.HitTime - now), ("read", a.Read), ("timed", a.Timed), ("health", h.Humanoid.GetHealth()));
        }
    }
}
