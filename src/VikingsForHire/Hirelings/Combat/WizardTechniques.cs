using System;
using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Enhancements;
using VikingsForHire.Diagnostics;
using UnityEngine;
using static VikingsForHire.Core.Enhancements.EnhancementId;
using Object = UnityEngine.Object;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// The wizard's enhancement techniques (plan/enhancements phase 07), built on the game's own staffs and effects:
    /// Blink, Secondary spell, Mender, Protection, Entangling roots and Raise dead run on the wizard's owner from here;
    /// Long reach changes CombatBehaviour.Cast's range; Elemental mastery and Chain spell work on the hit, in
    /// DamagePatches. Every technique checks Enh.Has.
    /// </summary>
    internal sealed class WizardTechniques
    {
        private const float NovaRadius = 5f;
        private const string ShieldEffect = "Staff_shield";
        private const string RootsStaff = "StaffGreenRoots";
        private const string Skeleton = "Skeleton_Friendly";
        // The game's own effects, where they exist (each is skipped if a game update renames it).
        private static readonly string[] BlinkFx = { "fx_DvergerMage_Mistile_attack", "vfx_spawn_small" };

        private readonly HirelingAI _ai;
        private float _nextBlink;
        private float _nextSecondary;
        private float _nextMend;
        private float _nextProtect;
        private float _nextRoots;
        private float _nextRaise;

        public WizardTechniques(HirelingAI ai) => _ai = ai;

        private Hireling H => _ai.Hireling;

        /// <summary>Long reach: metres added to the casting range.</summary>
        public static float ExtraRange(Hireling h) => h.Enh.Value(LongReach, "range");

        /// <summary>Every AI tick on the owner (target: its combat target, if any).</summary>
        public void Tick(Character? target)
        {
            if (H.Job != JobType.Wizard || !H.Enh.Effects.AnyPicks || H.Humanoid.IsDead())
                return;
            float now = Time.time;
            Mend(now);
            Protect(now, target != null);
            if (target == null)
                return;
            Roots(now, target);
            Raise(now);
        }

        /// <summary>
        /// Blink: with an enemy within reach and Blink ready, the wizard is somewhere else, Blink.distance away from it,
        /// on standable ground it can see the enemy from. True when it blinked (the back-off is then done).
        /// </summary>
        public bool TryBlink(Character threat)
        {
            if (!H.Enh.Has(Blink) || Time.time < _nextBlink)
                return false;
            Humanoid me = H.Humanoid;
            float dist = Vector3.Distance(me.transform.position, threat.transform.position) - threat.GetRadius();
            if (dist > H.Enh.Value(Blink, "trigger"))
                return false;
            float far = H.Enh.Value(Blink, "distance");
            Vector3 away = me.transform.position - threat.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -me.transform.forward;
            foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f })
            {
                Vector3 dir = Quaternion.AngleAxis(turn, Vector3.up) * away;
                Vector3 to = me.transform.position + dir * far;
                if (!ZoneSystem.instance.GetSolidHeight(to, out float y, 1000))
                    continue;
                to.y = y + 0.1f;
                if (Mathf.Abs(to.y - me.transform.position.y) > 3f || WaterRules.Under(to))
                    continue;
                if (Physics.Linecast(to + Vector3.up * 1.6f, threat.GetCenterPoint(), LayerMask.GetMask("Default", "static_solid", "terrain", "piece")))
                    continue;
                Fx(BlinkFx, me.transform.position);
                me.transform.position = to;
                if (me.m_body != null)
                {
                    me.m_body.position = to;
                    me.m_body.velocity = Vector3.zero;
                }
                Fx(BlinkFx, to);
                _nextBlink = Time.time + H.Enh.Value(Blink, "cooldown");
                EnhancementEvents.Fire(H, "Blink", LogCat.Combat, ("from", threat.m_name), ("to", to));
                return true;
            }
            return false;
        }

        /// <summary>Secondary spell: the staff's own secondary attack when it has one, else a frost nova round the wizard.</summary>
        public bool TrySecondary(Character target)
        {
            if (!H.Enh.Has(SecondarySpell) || Time.time < _nextSecondary)
                return false;
            Humanoid me = H.Humanoid;
            ItemDrop.ItemData? staff = me.GetCurrentWeapon();
            if (staff != null && staff.HaveSecondaryAttack())
            {
                if (!me.StartAttack(target, true))
                    return false;
                _nextSecondary = Time.time + H.Enh.Value(SecondarySpell, "cooldown");
                EnhancementEvents.Fire(H, "SecondarySpell", LogCat.Combat, ("how", "staff"), ("staff", GearApplier.Name(staff)));
                return true;
            }
            _nextSecondary = Time.time + H.Enh.Value(SecondarySpell, "cooldown");
            int n = 0;
            foreach (Character c in GuardTechniques.Enemies(me, me.transform.position, NovaRadius).ToList())
            {
                HitData hit = GuardTechniques.Technique(me, c.GetCenterPoint(), (c.transform.position - me.transform.position).normalized);
                hit.m_damage.m_frost = H.Enh.Value(SecondarySpell, "nova");
                c.Damage(hit);
                n++;
            }
            EnhancementEvents.Fire(H, "SecondarySpell", LogCat.Combat, ("how", "nova"), ("enemies", n));
            return true;
        }

        private void Mend(float now)
        {
            if (now < _nextMend || !H.Enh.Has(Mender))
                return;
            float range = H.Enh.Value(Mender, "range");
            Character? worst = null;
            float worstPct = 0.999f;
            IEnumerable<Character> friends = Player.GetAllPlayers().Cast<Character>()
                .Concat(Hireling.Loaded.Where(x => x != null && x != H).Select(x => (Character)x.Humanoid));
            foreach (Character c in friends)
            {
                if (c == null || c.IsDead() || Vector3.Distance(c.transform.position, H.transform.position) > range)
                    continue;
                float pct = c.GetHealthPercentage();
                if (pct < worstPct)
                {
                    worst = c;
                    worstPct = pct;
                }
            }
            if (worst == null)
                return;
            _nextMend = now + H.Enh.Value(Mender, "cooldown");
            float heal = worst.GetMaxHealth() * H.Enh.Value(Mender, "heal");
            worst.Heal(heal, true);
            EnhancementEvents.Fire(H, "Mender", LogCat.Combat, ("who", worst.m_name), ("heal", heal));
        }

        private void Protect(float now, bool fighting)
        {
            if (now < _nextProtect || !H.Enh.Has(Protection))
                return;
            // Its owner when following (only when that's this game's player, who owns their own status effects), else itself.
            Character who = H.Humanoid;
            if (H.Mode == HirelingMode.Following && Player.m_localPlayer is Player me && me.GetPlayerID() == H.OwnerId &&
                Vector3.Distance(me.transform.position, H.transform.position) < 20f)
                who = me;
            else if (!fighting)
                return; // on its own, only in a fight
            StatusEffect? se = who.GetSEMan().AddStatusEffect(ShieldEffect.GetStableHashCode(), resetTime: true);
            if (se is SE_Shield shield)
            {
                shield.m_totalAbsorbDamage = H.Enh.Value(Protection, "absorb");
                // Added without the staff's item level it has no time limit: it lasts until the next one is due.
                if (shield.m_ttl <= 0f)
                    shield.m_ttl = H.Enh.Value(Protection, "cooldown");
            }
            _nextProtect = now + H.Enh.Value(Protection, "cooldown");
            EnhancementEvents.Fire(H, "Protection", LogCat.Combat, ("on", who.m_name), ("found", se != null));
        }

        private void Roots(float now, Character target)
        {
            if (now < _nextRoots || !H.Enh.Has(EntanglingRoots))
                return;
            _nextRoots = now + H.Enh.Value(EntanglingRoots, "cooldown");
            ItemDrop.ItemData? staff = ObjectDB.instance?.GetItemPrefab(RootsStaff)?.GetComponent<ItemDrop>()?.m_itemData;
            GameObject? projectile = staff?.m_shared.m_attack.m_attackProjectile;
            if (projectile == null)
            {
                VfhLog.W(LogCat.Combat, "enh.roots_missing", ("staff", RootsStaff));
                return;
            }
            // The staff's projectile dropped straight onto the target: where it lands, the roots grow.
            Vector3 from = target.transform.position + Vector3.up * 3f;
            Vector3 vel = Vector3.down * 20f;
            HitData hit = GuardTechniques.Technique(H.Humanoid, target.transform.position, Vector3.down);
            hit.m_damage = staff!.m_shared.m_damages.Clone();
            GameObject go = Object.Instantiate(projectile, from, Quaternion.LookRotation(vel));
            go.GetComponent<IProjectile>()?.Setup(H.Humanoid, vel, -1f, hit, null, null);
            EnhancementEvents.Fire(H, "EntanglingRoots", LogCat.Combat, ("target", target.m_name));
        }

        private void Raise(float now)
        {
            if (now < _nextRaise || !H.Enh.Has(RaiseDead))
                return;
            _nextRaise = now + H.Enh.Value(RaiseDead, "cooldown");
            if (SummonedAllies.Raise(H, Skeleton, H.Enh.Value(RaiseDead, "seconds")))
                EnhancementEvents.Fire(H, "RaiseDead", LogCat.Combat);
        }

        private static void Fx(string[] names, Vector3 at)
        {
            foreach (string name in names)
                if (ZNetScene.instance?.GetPrefab(name) is GameObject fx)
                {
                    Object.Instantiate(fx, at, Quaternion.identity);
                    return;
                }
        }

        // ---- Hooks from DamagePatches (on the machine applying the hit) ----

        [ThreadStatic] private static float _lastingMult;

        /// <summary>Before the multiplier: Elemental mastery strengthens the spell's elemental damage (and so its burn, chill or shock).</summary>
        public static void BeforeHit(Hireling from, HitData hit)
        {
            _lastingMult = 0f;
            if (from.Job != JobType.Wizard || !from.Enh.Has(ElementalMastery) || GuardTechniques.IsTechnique(hit))
                return;
            float m = 1f + from.Enh.Value(ElementalMastery, "mult");
            hit.m_damage.m_fire *= m;
            hit.m_damage.m_frost *= m;
            hit.m_damage.m_lightning *= m;
            hit.m_damage.m_poison *= m;
            hit.m_damage.m_spirit *= m;
            _lastingMult = m;
        }

        /// <summary>After vanilla applied the hit: Elemental mastery's burning and chill last longer too.</summary>
        public static void AfterHitApplied(Character target)
        {
            if (_lastingMult <= 0f)
                return;
            SEMan se = target.GetSEMan();
            foreach (int hash in new[] { SEMan.s_statusEffectBurning, SEMan.s_statusEffectFrost, SEMan.s_statusEffectPoison, SEMan.s_statusEffectLightning })
                if (se.GetStatusEffect(hash) is StatusEffect s && ObjectDB.instance?.GetStatusEffect(hash) is StatusEffect proto && proto.m_ttl > 0f &&
                    s.m_ttl < proto.m_ttl * _lastingMult)
                {
                    s.m_ttl = proto.m_ttl * _lastingMult;
                    EnhancementEvents.Fire(null, "ElementalMastery", LogCat.Combat, ("target", target.m_name), ("effect", s.name), ("seconds", s.m_ttl));
                }
            _lastingMult = 0f;
        }

        /// <summary>After a wizard's spell hit is scaled: Chain spell strikes the nearest other enemy for a share.</summary>
        public static void AfterHit(Hireling from, Character target, HitData hit, float preMultDamage)
        {
            if (from.Job != JobType.Wizard || !from.Enh.Has(ChainSpell) || GuardTechniques.IsTechnique(hit))
                return;
            float range = from.Enh.Value(ChainSpell, "range");
            Character? next = GuardTechniques.Enemies(from.Humanoid, target.transform.position, range)
                .Where(c => c != target)
                .OrderBy(c => (c.transform.position - target.transform.position).sqrMagnitude)
                .FirstOrDefault();
            if (next == null)
                return;
            HitData chain = GuardTechniques.Technique(from.Humanoid, next.GetCenterPoint(), (next.transform.position - target.transform.position).normalized);
            chain.m_damage = hit.m_damage.Clone();
            float total = hit.GetTotalDamage();
            chain.m_damage.Modify(total > 0f ? from.Enh.Value(ChainSpell, "share") * preMultDamage / total : 0f);
            next.Damage(chain);
            EnhancementEvents.Fire(from, "ChainSpell", LogCat.Combat, ("from", target.m_name), ("to", next.m_name));
        }
    }
}
