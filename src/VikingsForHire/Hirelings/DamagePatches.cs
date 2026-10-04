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
                        target.LastHitBy = attacker != null ? $"{Utils.GetPrefabName(attacker.gameObject)}{(attacker.IsTamed() ? "(tame)" : "")}" : "none";
                        target.LastHitDamage = hit.GetTotalDamage();
                        VfhLog.D(LogCat.Combat, "hireling.hit", ("hid", target.Hid), ("by", attacker != null ? attacker.m_name : "none"),
                            ("damage", hit.GetTotalDamage()), ("armor", target.LevelData.Armor));
                        return true;
                    }

                    if (__instance.IsTamed())
                        return Blocked("hireling_on_tame", from!, __instance);

                    float mult = DamageMultiplier(from!);
                    hit.m_damage.Modify(mult);
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
                        __result = h.LevelData.Armor;
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

        public static float DamageMultiplier(Hireling h)
        {
            HirelingLevelData level = h.LevelData;
            float factor = h.Job.IsGuard() ? 1f
                : DataStore.Current.Jobs.TryGetValue(h.Job, out JobData? job) ? job.WorkerCombatFactor : 1f;
            return level.GuardDamageMult * factor;
        }

        private static bool Blocked(string reason, Hireling h, Character? other)
        {
            VfhLog.T(LogCat.Combat, "damage.blocked", ("reason", reason), ("hid", h.Hid), ("other", other != null ? other.m_name : "none"));
            return false;
        }
    }
}
