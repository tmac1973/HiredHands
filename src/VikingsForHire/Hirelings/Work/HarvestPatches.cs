using System;
using HarmonyLib;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Hirelings harvest by applying damage directly in time with their swing (GatherBehaviour sets <see cref="Direct"/>),
    /// so it doesn't depend on an NPC's aim. Stray hits from their swings on trees, logs, rocks and destructibles are
    /// ignored, so harvesting is exactly what the behaviour decides and nothing else.
    /// </summary>
    internal static class HarvestPatches
    {
        public static bool Direct;

        private static bool Allow(HitData hit) => Direct || Hireling.Of(hit?.GetAttacker()) == null;

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        private static class TreePatch
        {
            private static bool Prefix(HitData hit)
            {
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.Tree", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        private static class LogPatch
        {
            private static bool Prefix(HitData hit)
            {
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.Log", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        private static class DestructiblePatch
        {
            private static bool Prefix(HitData hit)
            {
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.Destructible", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        private static class MineRockPatch
        {
            private static bool Prefix(HitData hit)
            {
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.MineRock", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        private static class MineRock5Patch
        {
            private static bool Prefix(HitData hit)
            {
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.MineRock5", e);
                    return true;
                }
            }
        }
    }
}
