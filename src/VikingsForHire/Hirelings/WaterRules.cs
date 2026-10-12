using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Hirelings keep to dry land: no standing about in the sea by a shoreline board, no swimming out to a rock, no
    /// wading after someone. One sea level everywhere (Valheim has no lakes above it).
    /// </summary>
    internal static class WaterRules
    {
        /// <summary>Whether the ground there is under water by more than <paramref name="depth"/> metres.</summary>
        public static bool Under(Vector3 p, float depth = 0.3f) =>
            ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p) < ZoneSystem.instance.m_waterLevel - depth;

        /// <summary>Out in the water: swimming, or its feet below the surface (a fish, a serpent, a player treading water).</summary>
        public static bool Out(Character c) =>
            c.IsSwimming() || (c.InWater() && Under(c.transform.position, 1f));
    }
}
