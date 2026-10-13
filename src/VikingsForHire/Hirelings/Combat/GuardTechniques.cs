using System.Collections.Generic;
using VikingsForHire.Core;
using VikingsForHire.Core.Enhancements;
using UnityEngine;
using static VikingsForHire.Core.Enhancements.EnhancementId;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// The melee guard's enhancement techniques (plan/enhancements phase 05): Shield bash, Second wind, Bodyguard and
    /// Taunt run from here on the guard's owner; Shield training, Bloodthirst, Cleave, Last stand and Rallying cry hook
    /// into the block and damage patches through the static helpers below. Each one fires an enhancement event
    /// (enh_events in tests). Every technique checks Enh.Has, so a guard without the pick (or with Enhancements off)
    /// fights exactly as before.
    /// </summary>
    internal sealed class GuardTechniques
    {
        // A fight ends after this long with no combat target (Second wind is once per fight).
        private const float FightOverSeconds = 10f;
        private const float BodyguardHoldSeconds = 5f;
        private const float BashReach = 2.5f;
        private const float CleaveArc = 120f;

        private readonly HirelingAI _ai;
        private float _nextBash;
        private float _nextBodyguard;
        private float _nextTaunt;
        private float _lastFightAt = -999f;
        private bool _secondWindUsed;
        // Enemies made to go for this guard (Taunt, Bodyguard), until when: vanilla re-picks its target every few
        // seconds, so the target is set again every tick while the hold lasts.
        private readonly Dictionary<Character, float> _held = new();

        public GuardTechniques(HirelingAI ai) => _ai = ai;

        private Hireling H => _ai.Hireling;

        /// <summary>Last stand's grace: no damage until then.</summary>
        public float LastStandUntil { get; set; } = -1f;

        /// <summary>Every AI tick on the owner.</summary>
        public void Tick()
        {
            if (!H.Enh.Effects.AnyPicks)
                return;
            bool fighting = _ai.CombatTarget != null;
            float now = Time.time;
            if (fighting)
                _lastFightAt = now;
            else if (now - _lastFightAt > FightOverSeconds)
                _secondWindUsed = false;
            SecondWind(fighting);
            Bodyguard(now);
            if (fighting)
                Taunt(now);
            Hold(now);
        }

        /// <summary>Shield bash before a swing, when ready and in reach: staggers the target, small blunt damage.</summary>
        public bool TryBash(Character target, float dist)
        {
            if (!H.Enh.Has(ShieldBash) || Time.time < _nextBash || dist > BashReach || !_ai.Block.HasShield)
                return false;
            _nextBash = Time.time + H.Enh.Value(ShieldBash, "cooldown");
            Humanoid me = H.Humanoid;
            Vector3 dir = target.transform.position - me.transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : me.transform.forward;
            var hit = Technique(me, target.GetCenterPoint(), dir);
            hit.m_damage.m_blunt = H.Enh.Value(ShieldBash, "damage");
            hit.m_pushForce = 10f;
            target.Damage(hit);
            target.Stagger(dir);
            EnhancementEvents.Fire(H, "ShieldBash", Core.Diagnostics.LogCat.Combat, ("target", target.m_name));
            return true;
        }

        private void SecondWind(bool fighting)
        {
            if (!fighting || _secondWindUsed || !H.Enh.Has(EnhancementId.SecondWind))
                return;
            Humanoid me = H.Humanoid;
            if (me.GetHealthPercentage() >= H.Enh.Value(EnhancementId.SecondWind, "below"))
                return;
            _secondWindUsed = true;
            float heal = me.GetMaxHealth() * H.Enh.Value(EnhancementId.SecondWind, "heal");
            me.Heal(heal, true);
            EnhancementEvents.Fire(H, "SecondWind", Core.Diagnostics.LogCat.Combat, ("heal", heal));
        }

        private void Bodyguard(float now)
        {
            if (now < _nextBodyguard || !H.Enh.Has(EnhancementId.Bodyguard))
                return;
            float range = H.Enh.Value(EnhancementId.Bodyguard, "range");
            Character? attacker = AllyHits.RecentAttackerNear(H.transform.position, range);
            if (attacker == null && H.Mode == HirelingMode.Following && Player.GetPlayer(H.OwnerId) is Player owner)
                attacker = AllyHits.RecentAttackerNear(owner.transform.position, 2f);
            if (attacker == null || attacker.IsDead() || Targeting(attacker) == H.Humanoid)
                return;
            _nextBodyguard = now + H.Enh.Value(EnhancementId.Bodyguard, "cooldown");
            _held[attacker] = now + BodyguardHoldSeconds;
            EnhancementEvents.Fire(H, "Bodyguard", Core.Diagnostics.LogCat.Combat, ("enemy", attacker.m_name));
        }

        private void Taunt(float now)
        {
            if (now < _nextTaunt || !H.Enh.Has(EnhancementId.Taunt))
                return;
            float range = H.Enh.Value(EnhancementId.Taunt, "range");
            float until = now + H.Enh.Value(EnhancementId.Taunt, "seconds");
            int n = 0;
            foreach (Character c in Enemies(H.Humanoid, H.transform.position, range))
            {
                _held[c] = Mathf.Max(until, _held.TryGetValue(c, out float was) ? was : 0f);
                n++;
            }
            if (n == 0)
                return;
            _nextTaunt = now + H.Enh.Value(EnhancementId.Taunt, "cooldown");
            HirelingChatter.Shout(H, "taunt");
            EnhancementEvents.Fire(H, "Taunt", Core.Diagnostics.LogCat.Combat, ("enemies", n));
        }

        private void Hold(float now)
        {
            if (_held.Count == 0)
                return;
            List<Character>? done = null;
            foreach (KeyValuePair<Character, float> kv in _held)
            {
                Character c = kv.Key;
                if (c == null || c.IsDead() || now >= kv.Value || H.Humanoid.IsDead())
                {
                    (done ??= new List<Character>()).Add(c!);
                    continue;
                }
                // Only the creature's owner runs its AI: elsewhere the hold does nothing (in multiplayer the enemy is
                // usually owned by whoever is nearest, which is the guard's owner when they're together).
                if (c.GetBaseAI() is MonsterAI m && m.m_nview != null && m.m_nview.IsOwner())
                {
                    m.m_targetCreature = H.Humanoid;
                    m.m_targetStatic = null;
                    m.SetAlerted(true);
                }
            }
            if (done != null)
                foreach (Character c in done)
                    _held.Remove(c);
        }

        private static Character? Targeting(Character c) => c.GetBaseAI() is MonsterAI m ? m.GetTargetCreature() : null;

        /// <summary>Living enemies of <paramref name="me"/> within range of a point (not players, tames or hirelings).</summary>
        public static IEnumerable<Character> Enemies(Character me, Vector3 at, float range)
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == me || c.IsDead() || c is Player || c.IsTamed() || Hireling.Of(c) != null)
                    continue;
                if (!BaseAI.IsEnemy(me, c))
                    continue;
                if ((c.transform.position - at).sqrMagnitude <= range * range)
                    yield return c;
            }
        }

        /// <summary>
        /// A technique's own hit (bash, cleave): marked by having no skill, so it never sets off Cleave or Bloodthirst
        /// again, on whichever machine applies it.
        /// </summary>
        public static HitData Technique(Character attacker, Vector3 point, Vector3 dir)
        {
            var hit = new HitData
            {
                m_point = point,
                m_dir = dir,
                m_skill = Skills.SkillType.None,
            };
            hit.SetAttacker(attacker);
            return hit;
        }

        public static bool IsTechnique(HitData hit) => hit.m_skill == Skills.SkillType.None;

        // ---- Hooks from the patches (they run where the damage is applied) ----

        /// <summary>Shield training: block power multiplier for a hireling's shield.</summary>
        public static float BlockMult(Hireling h) => 1f + h.Enh.Value(ShieldTraining, "block");

        /// <summary>
        /// After a guard's hit on an enemy is scaled (DamagePatches): Bloodthirst heals it a share of the damage, and a
        /// landed sword swing cleaves into up to Cleave.extra other enemies in front, for a share of its damage.
        /// </summary>
        public static void AfterHit(Hireling from, Character target, HitData hit, float preMultDamage)
        {
            if (!from.Enh.Effects.AnyPicks)
                return;
            float dealt = hit.GetTotalDamage();
            if (from.Enh.Has(Bloodthirst) && dealt > 0f && !IsTechnique(hit))
            {
                float heal = dealt * from.Enh.Value(Bloodthirst, "share");
                from.Humanoid.Heal(heal, false);
                EnhancementEvents.Fire(from, "Bloodthirst", Core.Diagnostics.LogCat.Combat, ("heal", heal));
            }
            if (from.Enh.Has(Cleave) && !hit.m_ranged && !IsTechnique(hit) && from.Job == Core.JobType.GuardMelee)
                CleaveFrom(from, target, hit, preMultDamage);
        }

        private static void CleaveFrom(Hireling from, Character target, HitData swing, float preMultDamage)
        {
            Humanoid me = from.Humanoid;
            float range = from.Enh.Value(Cleave, "range");
            int max = Mathf.RoundToInt(from.Enh.Value(Cleave, "extra"));
            float share = from.Enh.Value(Cleave, "share");
            Vector3 fwd = me.transform.forward;
            fwd.y = 0f;
            var near = new List<(Character c, float d)>();
            foreach (Character c in Enemies(me, me.transform.position, range + 1f))
            {
                if (c == target)
                    continue;
                Vector3 to = c.transform.position - me.transform.position;
                to.y = 0f;
                float d = to.magnitude - c.GetRadius();
                if (d > range || Vector3.Angle(fwd, to) > CleaveArc / 2f)
                    continue;
                near.Add((c, d));
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));
            int n = 0;
            foreach ((Character c, float _) in near)
            {
                if (n >= max)
                    break;
                // The pre-multiplier damage: the hit goes through DamagePatches again and is scaled there.
                HitData hit = Technique(me, c.GetCenterPoint(), (c.transform.position - me.transform.position).normalized);
                hit.m_damage = swing.m_damage.Clone();
                float total = swing.GetTotalDamage();
                hit.m_damage.Modify(total > 0f ? share * preMultDamage / total : 0f);
                c.Damage(hit);
                EnhancementEvents.Fire(from, "CleaveHit", Core.Diagnostics.LogCat.Combat, ("enemy", c.m_name));
                n++;
            }
            if (n > 0)
                EnhancementEvents.Fire(from, "Cleave", Core.Diagnostics.LogCat.Combat, ("extra", n));
        }

        /// <summary>
        /// Before a hit lands on a guard (on its owner): during Last stand's grace nothing gets through (false); a hit
        /// that would kill it, with Last stand ready, leaves it on 1 health and starts the grace.
        /// </summary>
        public static bool BeforeHitTaken(Hireling target, HitData hit)
        {
            if (!target.Enh.Has(LastStand) || target.Ai == null)
                return true;
            GuardTechniques g = target.Ai.Guard;
            if (Time.time < g.LastStandUntil)
                return false;
            float health = target.Humanoid.GetHealth();
            // ApplyDamage scales the hit once more (world difficulty and damage rate) before taking it off.
            float scale = (Game.instance != null ? Game.instance.GetDifficultyDamageScaleEnemy(target.transform.position) : 1f) * Game.m_playerDamageRate;
            float total = hit.GetTotalDamage() * scale;
            if (total < health || ZNet.instance == null)
                return true;
            ZDO? z = target.Zdo;
            double now = ZNet.instance.GetTimeSeconds();
            if (z == null || now < z.GetFloat(HirelingZdo.LastStandReady, 0f))
                return true;
            z.Set(HirelingZdo.LastStandReady, (float)(now + target.Enh.Value(LastStand, "cooldown")));
            hit.ApplyModifier(Mathf.Max(0f, health - 1f) / total * 0.999f);
            g.LastStandUntil = Time.time + target.Enh.Value(LastStand, "seconds");
            EnhancementEvents.Fire(target, "LastStand", Core.Diagnostics.LogCat.Combat, ("would", total), ("health", health));
            return true;
        }

        /// <summary>Rallying cry: × (1 + damage) for a hireling near a Rallying guard that is in a fight (the best one counts).</summary>
        public static float RallyMult(Hireling h, Character? target)
        {
            float best = 1f;
            foreach (Hireling g in Hireling.Loaded)
            {
                if (g == null || g.Ai == null || g.Ai.CombatTarget == null || !g.Enh.Has(RallyingCry))
                    continue;
                float range = g.Enh.Value(RallyingCry, "range");
                if ((g.transform.position - h.transform.position).sqrMagnitude <= range * range)
                    best = Mathf.Max(best, 1f + g.Enh.Value(RallyingCry, "damage"));
            }
            return best;
        }
    }
}
