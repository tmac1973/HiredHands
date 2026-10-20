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

        // How deep a hireling wades to get at something (a character swims from about 1.6 m).
        public const float WadeDepth = 1.2f;

        /// <summary>
        /// Out in water too deep to wade to: under every part of it, and the ground within reach round it, the water is
        /// deeper than <see cref="WadeDepth"/>. Not just under its middle: Thorvald left three logs lying in an
        /// ankle-deep stream (the bed in the middle of a stream at sea level is a metre down or more).
        /// </summary>
        public static bool TooDeep(Component c, float reach = 1.5f)
        {
            if (!Under(c.transform.position, WadeDepth))
                return false;
            Bounds? all = null;
            foreach (Collider k in c.GetComponentsInChildren<Collider>())
            {
                if (!k.enabled || k.isTrigger)
                    continue;
                if (all is Bounds a)
                {
                    a.Encapsulate(k.bounds);
                    all = a;
                }
                else
                    all = k.bounds;
            }
            Bounds b = all ?? new Bounds(c.transform.position, Vector3.zero);
            for (int i = 0; i <= 4; i++)
                for (int j = 0; j <= 4; j++)
                {
                    Vector3 p = new(Mathf.Lerp(b.min.x - reach, b.max.x + reach, i / 4f), b.center.y, Mathf.Lerp(b.min.z - reach, b.max.z + reach, j / 4f));
                    if (!Under(p, WadeDepth))
                        return false;
                }
            return true;
        }

        /// <summary>Out in the water: swimming, or its feet below the surface (a fish, a serpent, a player treading water).</summary>
        public static bool Out(Character c) =>
            c.IsSwimming() || (c.InWater() && Under(c.transform.position, 1f));
    }
}
