using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// The ground at a height. The game's own solid-height check casts down from a kilometre up, so under a roof it
    /// lands on the roof, and in a dug-out cave (HearthBelow) on the hill above it. Here, when something is over the
    /// spot and there's open space under it (a ceiling: the ray up from the reference height hits it), the floor under
    /// the reference height wins instead. In the open, inside a hill or inside a rock, it's the game's answer as before.
    /// </summary>
    internal static class GroundRules
    {
        private static readonly int SolidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        // How far over the reference height a floor still counts (a step up), and how far down one is looked for.
        private const float StepUp = 1.5f;
        private const float Depth = 50f;

        /// <summary>
        /// The solid surface under <paramref name="p"/> at about the height <paramref name="near"/> (a board's, a
        /// hireling's): the floor of a hall or cave it's in, else the top, as <see cref="ZoneSystem.GetSolidHeight(Vector3, out float, int)"/>.
        /// </summary>
        public static bool Below(Vector3 p, float near, out float height)
        {
            bool top = ZoneSystem.instance.GetSolidHeight(p, out height);
            var from = new Vector3(p.x, near + StepUp, p.z);
            if (top && height <= from.y)
                return true;
            // Rays from inside a hill or a rock pass out of it without a hit: only a real ceiling stops the one going up.
            float up = top ? height - from.y : 1000f;
            if (Physics.Raycast(from, Vector3.up, up, SolidMask, QueryTriggerInteraction.Ignore) &&
                Physics.Raycast(from, Vector3.down, out RaycastHit hit, Depth, SolidMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.attachedRigidbody)
            {
                height = hit.point.y;
                return true;
            }
            return top;
        }

        /// <summary>Something solid over the point with room under it: a roof, an upper floor, a cave's rock.</summary>
        public static bool UnderCeiling(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * StepUp, Vector3.up, 1000f, SolidMask, QueryTriggerInteraction.Ignore);
    }
}
