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
    /// (the default), a hireling's swing never digs near your buildings (any player-built piece within
    /// MinerFieldDigClearance) or inside its board's work area, so miners can't leave holes in a base. Out in the field
    /// it digs like a player's, which is how the last low chunks of a copper deposit are reached.
    /// </summary>
    internal static class TerrainProtectionPatches
    {
        private static float _lastLog;
        private static readonly System.Collections.Generic.List<Piece> Pieces = new();

        // Away from any building and outside its own board's work area.
        private static bool InField(Hireling h, Vector3 point)
        {
            if (Utils.DistanceXZ(point, h.Home) <= h.Radius)
                return false;
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(point, VfhConfig.MinerFieldDigClearance.Value, Pieces);
            foreach (Piece p in Pieces)
            {
                if (p != null && p.GetCreator() != 0L)
                    return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.SpawnOnHitTerrain))]
        private static class SpawnOnHitTerrainPatch
        {
            private static bool Prefix(Vector3 hitPoint, Character character, ref GameObject? __result)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    Hireling? h = Hireling.Of(character);
                    if (!VfhConfig.MinerProtectsTerrain.Value || h == null || InField(h, hitPoint))
                        return true;
                    __result = null;
                    if (Time.time - _lastLog > 10f)
                    {
                        _lastLog = Time.time;
                        VfhLog.D(LogCat.Work, "work.terrain_protected", ("hid", h.Hid), ("at", hitPoint));
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
