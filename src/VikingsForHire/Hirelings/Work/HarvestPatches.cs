using System;
using HarmonyLib;
using VikingsForHire.Core;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Hirelings harvest by applying damage directly in time with their swing (GatherBehaviour sets <see cref="Direct"/>),
    /// so it doesn't depend on an NPC's aim. Stray hits from their swings on trees, logs, rocks and destructibles are
    /// ignored, so harvesting is exactly what the behaviour decides and nothing else; the one exception is a wizard's
    /// spell when WizardSpellsHit allows it (SpellRules.MayHitProp).
    /// </summary>
    internal static class HarvestPatches
    {
        public static bool Direct;

        private static bool Allow(HitData hit)
        {
            if (Direct)
                return true;
            Hireling? from = Hireling.Of(hit?.GetAttacker());
            return from == null || SpellRules.MayHitProp(from.Job, VikingsForHire.Config.VfhConfig.WizardSpellsHit.Value);
        }

        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        private static class TreePatch
        {
            private static bool Prefix(HitData hit)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.Tree", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("HarvestPatches.Tree", vfhStarted);
                }
            }
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        private static class LogPatch
        {
            private static bool Prefix(HitData hit)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.Log", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("HarvestPatches.Log", vfhStarted);
                }
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        private static class DestructiblePatch
        {
            private static bool Prefix(HitData hit)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.Destructible", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("HarvestPatches.Destructible", vfhStarted);
                }
            }
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        private static class MineRockPatch
        {
            private static bool Prefix(HitData hit)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.MineRock", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("HarvestPatches.MineRock", vfhStarted);
                }
            }
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        private static class MineRock5Patch
        {
            private static bool Prefix(HitData hit)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    return Allow(hit);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HarvestPatches.MineRock5", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("HarvestPatches.MineRock5", vfhStarted);
                }
            }
        }
    }
}
