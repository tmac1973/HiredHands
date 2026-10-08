using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Counts a hireling's blocks and parries (both with BlockAndDodge on and off, for comparing), and shows "Parry!" over
    /// it for everyone nearby. Vanilla's BlockAttack does the blocking itself; it runs inside Character.RPC_Damage on the
    /// hireling's owner, the machine running its AI.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
    internal static class BlockPatches
    {
        private static void Prefix(Humanoid __instance, out bool __state)
        {
            __state = false;
            if (Hireling.Of(__instance) == null || __instance.GetCurrentBlocker() is not ItemDrop.ItemData blocker)
                return;
            // Vanilla's own test for a timed block.
            __state = blocker.m_shared.m_timedBlockBonus > 1f && __instance.m_blockTimer != -1f && __instance.m_blockTimer < 0.25f;
        }

        private static void Postfix(Humanoid __instance, HitData hit, Character attacker, bool __result, bool __state)
        {
            if (!__result || Hireling.Of(__instance) is not Hireling h || h.Ai == null)
                return;
            h.Ai.Defense.Blocks++;
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
