using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Counts a hireling's blocks and parries (whatever the CombatSkill preset, Off included, for comparing), and shows "Parry!" over
    /// it for everyone nearby. Vanilla's BlockAttack does the blocking itself; it runs inside Character.RPC_Damage on the
    /// hireling's owner, the machine running its AI.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
    internal static class BlockPatches
    {
        /// <summary>The hireling whose block is being worked out (for Shield training's block power).</summary>
        private static Hireling? _blocking;

        private static void Prefix(Humanoid __instance, out bool __state)
        {
            __state = false;
            _blocking = null;
            if (Hireling.Of(__instance) is not Hireling h || __instance.GetCurrentBlocker() is not ItemDrop.ItemData blocker)
                return;
            _blocking = h;
            // Vanilla's own test for a timed block.
            __state = blocker.m_shared.m_timedBlockBonus > 1f && __instance.m_blockTimer != -1f && __instance.m_blockTimer < 0.25f;
        }

        private static void Finalizer() => _blocking = null;

        /// <summary>Shield training: the shield blocks with more power (only inside a hireling's BlockAttack).</summary>
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetBlockPower), typeof(int), typeof(float))]
        private static class BlockPowerPatch
        {
            private static void Postfix(ref float __result)
            {
                if (_blocking == null)
                    return;
                __result *= GuardTechniques.BlockMult(_blocking);
                if (_blocking.Ai != null)
                    _blocking.Ai.Defense.LastBlockPower = __result;
            }
        }

        private static void Postfix(Humanoid __instance, HitData hit, Character attacker, bool __result, bool __state)
        {
            if (!__result || Hireling.Of(__instance) is not Hireling h || h.Ai == null)
                return;
            h.Ai.Defense.Blocks++;
            if (!hit.m_ranged)
                h.Ai.Reader.ObserveHit(attacker);
            if (hit.m_ranged)
                h.Ai.Defense.ProjBlocks++;
            bool parry = __state && !__instance.IsStaggering();
            if (parry)
            {
                h.Ai.Defense.Parries++;
                DamageText.instance?.ShowText(DamageText.TextType.Bonus, hit.m_point + Vector3.up, "$vfh_parry");
            }
            VfhLog.D(LogCat.Combat, "defense.block", ("hid", h.Hid), ("parry", parry), ("attacker", attacker != null ? attacker.m_name : ""),
                ("ranged", hit.m_ranged), ("left", hit.GetTotalDamage()));
        }
    }
}
