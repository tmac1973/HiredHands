using System;
using HarmonyLib;
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
    /// The archer's enhancement techniques (plan/enhancements phase 06). Eagle eye and Skirmisher change how
    /// CombatBehaviour.Ranged and the threat scan measure; Volley and Rain of arrows spawn more of the archer's own arrow
    /// (captured as the bow fires it, so ammo is used once); Fire and Frost arrows, Headshot, Hunter and Marksman work on
    /// the hit, in DamagePatches. Every technique checks Enh.Has.
    /// </summary>
    internal sealed class ArcherTechniques
    {
        private const float VolleySpread = 6f;
        private const float RainHeight = 14f;
        private const float RainSpeed = 30f;

        private readonly HirelingAI _ai;
        private float _nextVolley;
        private float _nextRain;
        private bool _volleyArmed;
        // The last arrow the bow fired: what Volley and Rain of arrows copy.
        private GameObject? _arrowPrefab;
        private HitData? _arrowHit;
        private ItemDrop.ItemData? _arrowItem;
        private ItemDrop.ItemData? _arrowAmmo;

        public ArcherTechniques(HirelingAI ai) => _ai = ai;

        private Hireling H => _ai.Hireling;

        /// <summary>Arrows launched (each volley and rain arrow counts), since enh_events_reset.</summary>
        public int ShotsFired { get; set; }

        /// <summary>Eagle eye: metres added to sight and shooting range.</summary>
        public static float ExtraRange(Hireling h) => h.Enh.Value(EagleEye, "range");

        /// <summary>Skirmisher: keeps its bow up close (no sidearm), backs off from further and for longer.</summary>
        public static bool Skirmishes(Hireling h) => h.Enh.Has(Skirmisher);

        /// <summary>Just before the bow fires: a volley that's ready goes with this shot.</summary>
        public void BeforeShot()
        {
            if (!H.Enh.Has(Volley) || Time.time < _nextVolley)
                return;
            _volleyArmed = true;
            _nextVolley = Time.time + H.Enh.Value(Volley, "cooldown");
        }

        // Hunter: where its wildlife targets fell, and when (their drops are its to pick up).
        private Character? _tracked;
        private Vector3 _trackedPos;
        private Vector3 _killPos;
        private float _killAt = -999f;
        private const float KillLootSeconds = 60f;

        /// <summary>Wildlife (Hunter's bonus and pickup): the animal faction, the passive animal AI (deer, hares), or anything tameable (boar, wolf, lox).</summary>
        public static bool Wildlife(Character c) =>
            c.GetFaction() == Character.Faction.AnimalsVeg || c.GetBaseAI() is AnimalAI || c.GetComponent<Tameable>() != null;

        /// <summary>A recent kill of its own near enough to go and pick up the drops (Hunter, while following).</summary>
        public bool HuntSpot(out Vector3 at)
        {
            at = _killPos;
            return H.Enh.Has(Hunter) && Time.time - _killAt < KillLootSeconds &&
                   Vector3.Distance(H.transform.position, _killPos) <= H.Enh.Value(Hunter, "range");
        }

        /// <summary>Every AI tick on the owner (target: its combat target, if any).</summary>
        public void Tick(Character? target)
        {
            if (!H.Enh.Effects.AnyPicks)
                return;
            if (H.Enh.Has(Hunter))
                TrackKill(target);
            Rain(target);
        }

        private void TrackKill(Character? target)
        {
            if (_tracked != null && (_tracked.IsDead() || _tracked.GetHealth() <= 0f) || (_tracked == null && !ReferenceEquals(_tracked, null)))
            {
                _killPos = _trackedPos;
                _killAt = Time.time;
                EnhancementEvents.Fire(H, "HunterKill", LogCat.Combat, ("pos", _killPos));
                _tracked = null;
            }
            if (target != null && !target.IsDead() && Wildlife(target))
            {
                _tracked = target;
                _trackedPos = target.transform.position;
            }
            else if (_tracked != null && _tracked != target)
                _tracked = null;
        }

        private void Rain(Character? target)
        {
            if (target == null || !H.Enh.Has(RainOfArrows) || Time.time < _nextRain || _arrowPrefab == null || _arrowHit == null)
                return;
            _nextRain = Time.time + H.Enh.Value(RainOfArrows, "cooldown");
            int n = Mathf.RoundToInt(H.Enh.Value(RainOfArrows, "arrows"));
            float r = H.Enh.Value(RainOfArrows, "range");
            float share = H.Enh.Value(RainOfArrows, "share");
            Vector3 at = target.transform.position;
            for (int i = 0; i < n; i++)
            {
                Vector2 off = UnityEngine.Random.insideUnitCircle * r;
                Vector3 land = at + new Vector3(off.x, 0.5f, off.y);
                Vector3 from = land + Vector3.up * RainHeight + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f));
                HitData hit = _arrowHit.Clone();
                hit.m_damage.Modify(share);
                Spawn(_arrowPrefab, from, (land - from).normalized * RainSpeed, hit, _arrowItem, _arrowAmmo);
            }
            ShotsFired += n;
            EnhancementEvents.Fire(H, "RainOfArrows", LogCat.Combat, ("arrows", n), ("target", target.m_name));
        }

        // An arrow left an archer's bow: count it, remember it, and add the volley's extra arrows.
        private void Launched(Projectile p, Vector3 vel, HitData? hit, ItemDrop.ItemData? item, ItemDrop.ItemData? ammo)
        {
            ShotsFired++;
            GameObject? prefab = ZNetScene.instance?.GetPrefab(Utils.GetPrefabName(p.gameObject));
            if (prefab == null || hit == null)
                return;
            _arrowPrefab = prefab;
            _arrowHit = hit.Clone();
            _arrowItem = item;
            _arrowAmmo = ammo;
            if (!_volleyArmed)
                return;
            _volleyArmed = false;
            int extra = Mathf.RoundToInt(H.Enh.Value(Volley, "arrows")) - 1;
            for (int i = 1; i <= extra; i++)
            {
                float angle = VolleySpread * ((i + 1) / 2) * (i % 2 == 1 ? 1f : -1f);
                Vector3 v = Quaternion.AngleAxis(angle, Vector3.up) * vel;
                Spawn(prefab, p.transform.position, v, hit.Clone(), item, ammo);
            }
            ShotsFired += extra;
            EnhancementEvents.Fire(H, "Volley", LogCat.Combat, ("arrows", extra + 1));
        }

        [ThreadStatic] private static bool _spawning;

        private void Spawn(GameObject prefab, Vector3 from, Vector3 vel, HitData hit, ItemDrop.ItemData? item, ItemDrop.ItemData? ammo)
        {
            _spawning = true;
            try
            {
                GameObject go = Object.Instantiate(prefab, from, Quaternion.LookRotation(vel));
                go.GetComponent<IProjectile>()?.Setup(H.Humanoid, vel, -1f, hit, item, ammo);
            }
            finally
            {
                _spawning = false;
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
        private static class ProjectileSetupPatch
        {
            private static void Postfix(Projectile __instance, Character owner, Vector3 velocity, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo)
            {
                try
                {
                    if (_spawning || Hireling.Of(owner) is not Hireling h || h.Job != JobType.GuardRanged || h.Ai?.Archer == null)
                        return;
                    h.Ai.Archer.Launched(__instance, velocity, hitData, item, ammo);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("ArcherTechniques.Setup", e);
                }
            }
        }

        // ---- Hooks from DamagePatches (on the machine applying the hit) ----

        [ThreadStatic] private static float _burnSeconds;
        [ThreadStatic] private static float _frostSeconds;
        [ThreadStatic] private static float _frostSlow;

        private static bool ArrowHit(Hireling from, HitData hit) => from.Job == JobType.GuardRanged && hit.m_ranged && from.Enh.Effects.AnyPicks;

        /// <summary>Before the multiplier: Fire and Frost arrows add their damage, Headshot doubles now and then.</summary>
        public static void BeforeHit(Hireling from, Character target, HitData hit)
        {
            _burnSeconds = _frostSeconds = 0f;
            if (!ArrowHit(from, hit))
                return;
            if (from.Enh.Has(FireArrows))
            {
                hit.m_damage.m_fire += from.Enh.Value(FireArrows, "damage");
                _burnSeconds = from.Enh.Value(FireArrows, "seconds");
            }
            if (from.Enh.Has(FrostArrows))
            {
                hit.m_damage.m_frost += from.Enh.Value(FrostArrows, "damage");
                _frostSeconds = from.Enh.Value(FrostArrows, "seconds");
                _frostSlow = from.Enh.Value(FrostArrows, "slow");
            }
            if (from.Enh.Has(Headshot) && UnityEngine.Random.value < from.Enh.Value(Headshot, "chance"))
            {
                hit.m_damage.Modify(from.Enh.Value(Headshot, "mult"));
                DamageText.instance?.ShowText(DamageText.TextType.Bonus, hit.m_point + Vector3.up, "$vfh_headshot");
                EnhancementEvents.Fire(from, "Headshot", LogCat.Combat, ("target", target.m_name));
            }
        }

        /// <summary>
        /// After vanilla applied the hit: the burning and the chill it started last as long as the data says, and the
        /// chill slows by the data's share at first (fading out, as the game's frost does).
        /// </summary>
        public static void AfterHitApplied(Character target)
        {
            if (_burnSeconds <= 0f && _frostSeconds <= 0f)
                return;
            SEMan se = target.GetSEMan();
            if (_burnSeconds > 0f && se.GetStatusEffect(SEMan.s_statusEffectBurning) is SE_Burning burn && burn.m_damageInterval > 0f)
            {
                burn.m_ttl = _burnSeconds;
                burn.m_fireDamagePerHit = burn.m_fireDamageLeft / Mathf.Max(1, (int)(_burnSeconds / burn.m_damageInterval));
                EnhancementEvents.Fire(null, "Burning", LogCat.Combat, ("target", target.m_name), ("seconds", _burnSeconds));
            }
            if (_frostSeconds > 0f && se.GetStatusEffect(SEMan.s_statusEffectFrost) is SE_Frost frost)
            {
                frost.m_ttl = _frostSeconds;
                frost.m_minSpeedFactor = 1f - _frostSlow;
                frost.ResetTime();
                EnhancementEvents.Fire(null, "Chilled", LogCat.Combat, ("target", target.m_name), ("seconds", _frostSeconds));
            }
            _burnSeconds = _frostSeconds = 0f;
        }

        /// <summary>Hunter (wildlife) and Marksman (far targets): target-dependent damage multipliers.</summary>
        public static float TargetMult(Hireling h, Character? target)
        {
            if (target == null || h.Job != JobType.GuardRanged || !h.Enh.Effects.AnyPicks)
                return 1f;
            float m = 1f;
            if (h.Enh.Has(Hunter) && Wildlife(target))
                m *= 1f + h.Enh.Value(Hunter, "damage");
            if (h.Enh.Has(Marksman) && Vector3.Distance(h.transform.position, target.transform.position) > h.Enh.Value(Marksman, "range"))
                m *= 1f + h.Enh.Value(Marksman, "damage");
            return m;
        }
    }
}
