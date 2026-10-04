using System;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// A pickaxe swing that hits the ground digs it through Attack.SpawnOnHitTerrain. With MinerProtectsTerrain on
    /// (the default), a hireling's swing never does, so miners can't leave holes in the base.
    /// </summary>
    internal static class TerrainProtectionPatches
    {
        private static float _lastLog;

        [HarmonyPatch(typeof(Attack), nameof(Attack.SpawnOnHitTerrain))]
        private static class SpawnOnHitTerrainPatch
        {
            private static bool Prefix(Character character, ref GameObject? __result)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    if (!VfhConfig.MinerProtectsTerrain.Value || Hireling.Of(character) == null)
                        return true;
                    __result = null;
                    if (Time.time - _lastLog > 10f)
                    {
                        _lastLog = Time.time;
                        VfhLog.D(LogCat.Work, "work.terrain_protected", ("hid", Hireling.Of(character)!.Hid));
                    }
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("TerrainProtection.SpawnOnHitTerrain", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("TerrainProtection.SpawnOnHitTerrain", vfhStarted);
                }
            }
        }
    }
}
