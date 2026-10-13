using System;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Who can hurt whom. Players can't hurt hirelings unless FriendlyFireOnHirelings is on; hirelings never hurt each
    /// other, tamed animals or buildings; hireling hits on creatures scale with the level table; hireling armor comes
    /// from the level table (plain Humanoids get none from worn gear, so there's no double counting).
    /// </summary>
    internal static class DamagePatches
    {
        // Runs on the target's owner, before vanilla applies the hit.
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class CharacterDamagePatch
        {
            // Fire and Frost arrows: the burning and chill vanilla just started get the data's duration and slow.
            private static void Postfix(Character __instance)
            {
                try
                {
                    Combat.ArcherTechniques.AfterHitApplied(__instance);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.RPC_Damage.Postfix", e);
                }
            }

            private static bool Prefix(Character __instance, HitData hit)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    Character? attacker = hit.GetAttacker();
                    Hireling? target = Hireling.Of(__instance);
                    Hireling? from = Hireling.Of(attacker);
                    if (target == null && from == null)
                        return true;

                    if (target != null)
                    {
                        if (from != null)
                            return Blocked("hireling_on_hireling", target, attacker);
                        if (attacker is Player && !VfhConfig.FriendlyFireOnHirelings.Value)
                            return Blocked("friendly_fire", target, attacker);
                        // Tames (pets, summons, Defend Your Base guardians) are on our side: a stray area attack in a
                        // shared fight mustn't kill a worker, just as hirelings can't hurt tames.
                        if (attacker != null && attacker.IsTamed())
                            return Blocked("tame_on_hireling", target, attacker);
                        // Passengers are below deck: nothing reaches them.
                        if (target.IsStowed || target.Mode == HirelingMode.Returning)
                            return Blocked("stowed", target, attacker);
                        // Vanilla only applies body armor to players; hirelings get the armor of what they wear.
                        hit.ApplyArmor(target.Armor);
                        // Enhancements: Warding (and later damage-taken picks).
                        float taken = target.Enh.Effects.DamageTakenMult;
                        if (taken < 1f)
                            hit.ApplyModifier(taken);
                        // Vanilla gives sneak attacks (x4 from monster weapons) on any creature whose AI isn't alerted, and the
                        // hireling brain never raises vanilla's alert: every enemy's first hit was a sneak attack. Players
                        // can't be sneak-attacked; neither can hirelings.
                        hit.m_backstabBonus = 1f;
                        // Vanilla doubles damage to a staggered creature (the player's "critical hit" on monsters).
                        // Players never take that double, and neither should a hireling: halve it here to cancel it.
                        if (__instance.IsStaggering())
                            hit.ApplyModifier(0.5f);
                        target.LastHitBy = attacker != null ? $"{Utils.GetPrefabName(attacker.gameObject)}{(attacker.IsTamed() ? "(tame)" : "")}" : "none";
                        target.LastHitDamage = hit.GetTotalDamage();
                        VfhLog.D(LogCat.Combat, "hireling.hit", ("hid", target.Hid), ("by", attacker != null ? attacker.m_name : "none"),
                            ("damage", hit.GetTotalDamage()), ("armor", target.Armor));
                        return true;
                    }

                    // The Rancher slaughters tames on purpose (combat never targets one): his blow lands in full.
                    if (__instance.IsTamed() && from!.Job == JobType.Rancher)
                    {
                        VfhLog.I(LogCat.Combat, "ranch.slaughter_hit", ("hid", from.Hid), ("animal", __instance.m_name), ("damage", hit.GetTotalDamage()));
                        return true;
                    }
                    if (__instance.IsTamed())
                        return Blocked("hireling_on_tame", from!, __instance);
                    // Nor players, whatever PvP says: a stray splash or a spell's blast landed on whoever stood by the target.
                    if (__instance is Player)
                        return Blocked("hireling_on_player", from!, __instance);

                    Combat.ArcherTechniques.BeforeHit(from!, __instance, hit);
                    float mult = DamageMultiplier(from!, __instance);
                    float before = hit.GetTotalDamage();
                    hit.m_damage.Modify(mult);
                    Combat.GuardTechniques.AfterHit(from!, __instance, hit, before);
                    Telemetry.BalanceFights.Dealt(from!, __instance, hit.GetTotalDamage());
                    VfhLog.T(LogCat.Combat, "hireling.attack", ("hid", from!.Hid), ("target", __instance.m_name), ("mult", mult),
                        ("damage", hit.GetTotalDamage()));
                    return true;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.RPC_Damage", e);
                    // Vanilla behaviour on failure: the hit lands, even if it would normally have been blocked.
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("DamagePatches.RPC_Damage", vfhStarted);
                }
            }
        }

        /// <summary>
        /// Last stand works on the damage as it finally lands (vanilla scales a hit several times after RPC_Damage's
        /// start): a killing blow leaves 1 health, and nothing gets through for a moment after.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        private static class LastStandPatch
        {
            private static bool Prefix(Character __instance, HitData hit)
            {
                try
                {
                    if (Hireling.Of(__instance) is not Hireling target || __instance.IsDead())
                        return true;
                    if (Combat.GuardTechniques.BeforeHitTaken(target, hit))
                        return true;
                    return Blocked("last_stand", target, hit.GetAttacker());
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.ApplyDamage", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.GetBodyArmor))]
        private static class ArmorPatch
        {
            private static void Postfix(Character __instance, ref float __result)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    Hireling? h = Hireling.Of(__instance);
                    if (h != null)
                        __result = h.Armor;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.GetBodyArmor", e);
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("DamagePatches.GetBodyArmor", vfhStarted);
                }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        private static class PiecePatch
        {
            private static bool Prefix(WearNTear __instance, HitData hit)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    Hireling? from = Hireling.Of(hit.GetAttacker());
                    if (from == null)
                        return true;
                    VfhLog.T(LogCat.Combat, "hireling.piece_hit_blocked", ("hid", from.Hid), ("piece", __instance.name));
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.WearNTearDamage", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("DamagePatches.WearNTearDamage", vfhStarted);
                }
            }
        }

        /// <summary>Hireling gear is cosmetic and never wears out.</summary>
        [HarmonyPatch(typeof(Humanoid), "DrainEquipedItemDurability")]
        private static class DurabilityPatch
        {
            private static bool Prefix(Humanoid __instance)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    return Hireling.Of(__instance) == null;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.DrainEquipedItemDurability", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("DamagePatches.DrainEquipedItemDurability", vfhStarted);
                }
            }
        }

        /// <summary>
        /// Hirelings cast for free: they have no eitr bar, and vanilla refuses any attack with an eitr cost from something
        /// whose max eitr is 0, so without this a wizard's staff never fires.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.TryUseEitr))]
        private static class EitrPatch
        {
            private static bool Prefix(Character __instance, ref bool __result)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    if (__instance is not Humanoid h || Hireling.Of(h) == null)
                        return true;
                    __result = true;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.TryUseEitr", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("DamagePatches.TryUseEitr", vfhStarted);
                }
            }
        }

        /// <summary>
        /// Weapons that need reloading (crossbows, Dundr) only fire when loaded, and only players ever load: a hireling's
        /// is always loaded, paced by its attack cooldown instead.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsWeaponLoaded))]
        private static class LoadedPatch
        {
            private static void Postfix(Humanoid __instance, ref bool __result)
            {
                try
                {
                    if (!__result && Hireling.Of(__instance) != null)
                        __result = true;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DamagePatches.IsWeaponLoaded", e);
                }
            }
        }

        /// <summary>
        /// A hireling's outgoing damage multiplier: the level table, the job's share for workers, the server's damage
        /// setting, then its enhancements (the unconditional boosts, and those that depend on the target).
        /// </summary>
        public static float DamageMultiplier(Hireling h, Character? target = null)
        {
            HirelingLevelData level = h.LevelData;
            float factor = h.Job.IsGuard() ? 1f
                : DataStore.Current.Jobs.TryGetValue(h.Job, out JobData? job) ? job.WorkerCombatFactor : 1f;
            return level.GuardDamageMult * factor * VfhConfig.JobDamage(h.Job) * h.Enh.Effects.DamageMult * TargetMult(h, target);
        }

        /// <summary>Enhancement boosts that depend on the target or the situation (filled in by the behaviour phases).</summary>
        public static float TargetMult(Hireling h, Character? target)
        {
            float m = 1f;
            foreach (Func<Hireling, Character?, float> f in TargetMults)
                m *= f(h, target);
            return m;
        }

        /// <summary>Behaviours register their own target-dependent multipliers here (Hunter, Marksman, Rallying cry…).</summary>
        public static readonly System.Collections.Generic.List<Func<Hireling, Character?, float>> TargetMults = new()
        {
            Combat.GuardTechniques.RallyMult,
            Combat.ArcherTechniques.TargetMult,
        };

        private static readonly System.Collections.Generic.Dictionary<string, int> BlockedCounts = new();

        /// <summary>How many hits were blocked for this reason since the game started (for tests).</summary>
        public static int BlockedCount(string reason) => BlockedCounts.TryGetValue(reason, out int n) ? n : 0;

        private static bool Blocked(string reason, Hireling h, Character? other)
        {
            BlockedCounts[reason] = BlockedCount(reason) + 1;
            VfhLog.T(LogCat.Combat, "damage.blocked", ("reason", reason), ("hid", h.Hid), ("other", other != null ? other.m_name : "none"));
            return false;
        }
    }
}
