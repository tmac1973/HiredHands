using System;
using HarmonyLib;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Vanilla only lets players crouch (Character.IsCrouching is false for everything else). A follower sneaking with
    /// its owner counts as crouching, which in vanilla means crouch speed, no running and no footstep noise, and it's
    /// harder to see, about what an unskilled player gets while sneaking.
    /// </summary>
    internal static class SneakPatches
    {
        private const float SneakStealth = 0.5f;

        [HarmonyPatch(typeof(Character), nameof(Character.IsCrouching))]
        private static class CrouchPatch
        {
            private static void Postfix(Character __instance, ref bool __result)
            {
                try
                {
                    if (!__result && Hireling.SneakingNow.Count > 0 && Hireling.SneakingNow.Contains(__instance))
                        __result = true;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("SneakPatches.IsCrouching", e);
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.GetStealthFactor))]
        private static class StealthPatch
        {
            private static void Postfix(Character __instance, ref float __result)
            {
                try
                {
                    if (Hireling.SneakingNow.Count > 0 && Hireling.SneakingNow.Contains(__instance))
                        __result = Math.Min(__result, SneakStealth);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("SneakPatches.GetStealthFactor", e);
                }
            }
        }
    }
}
