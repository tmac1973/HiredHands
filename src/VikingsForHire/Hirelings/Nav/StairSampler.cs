using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>Casts rays at one piece to read its walkable surface, and checks the floors at a link's ends.</summary>
    internal static class StairSampler
    {
        public const int SampleCount = 9;
        public static readonly int FloorMask = LayerMask.GetMask("terrain", "piece", "Default", "static_solid");

        /// <summary>The piece's solid colliders and their combined bounds (false when it has none).</summary>
        public static bool Colliders(Piece piece, List<Collider> into, out Bounds bounds)
        {
            into.Clear();
            bounds = default;
            foreach (Collider c in piece.GetComponentsInChildren<Collider>(false))
            {
                if (c == null || c.isTrigger || !c.enabled)
                    continue;
                if (into.Count == 0)
                    bounds = c.bounds;
                else
                    bounds.Encapsulate(c.bounds);
                into.Add(c);
            }
            return into.Count > 0;
        }

        /// <summary>
        /// The surface height at <see cref="SampleCount"/> points along a horizontal axis through the bounds' centre,
        /// against this piece's own colliders only (highest upward-facing hit). Points are where each ray landed.
        /// </summary>
        public static List<(float Along, float? Height)> Sample(List<Collider> colliders, Bounds b, Vector3 axis, List<Vector3> points)
        {
            axis.y = 0f;
            axis.Normalize();
            float half = Mathf.Abs(axis.x) * b.extents.x + Mathf.Abs(axis.z) * b.extents.z;
            var samples = new List<(float, float?)>(SampleCount);
            points.Clear();
            float length = b.size.y + 1f;
            for (int i = 0; i < SampleCount; i++)
            {
                float t = -half + 2f * half * i / (SampleCount - 1);
                Vector3 origin = b.center + axis * t;
                origin.y = b.max.y + 0.5f;
                var ray = new Ray(origin, Vector3.down);
                float? best = null;
                foreach (Collider c in colliders)
                    if (c.Raycast(ray, out RaycastHit hit, length) && hit.normal.y >= 0.5f && (best == null || hit.point.y > best))
                        best = hit.point.y;
                samples.Add((t, best));
                points.Add(new Vector3(origin.x, best ?? b.min.y, origin.z));
            }
            return samples;
        }

        /// <summary>The floor's height under a point, if there's a walkable floor within <paramref name="within"/> m of the expected height.</summary>
        public static float? FloorAt(Vector3 p, float expected, float within)
        {
            var from = new Vector3(p.x, expected + 1f, p.z);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 2.5f, FloorMask, QueryTriggerInteraction.Ignore))
                return null;
            if (hit.normal.y < 0.7f || Mathf.Abs(hit.point.y - expected) > within)
                return null;
            return hit.point.y;
        }

        /// <summary>For logs: what the floor check's ray hit (height and how flat), or "nothing".</summary>
        public static string SurfaceBelow(Vector3 p, float expected) =>
            Physics.Raycast(new Vector3(p.x, expected + 1f, p.z), Vector3.down, out RaycastHit hit, 2.5f, FloorMask, QueryTriggerInteraction.Ignore)
                ? $"{hit.point.y:0.00} {hit.collider.name} n.y={hit.normal.y:0.00}" : "nothing";

        /// <summary>Room to stand: nothing solid in the 1.5 m above the point.</summary>
        public static bool Headroom(Vector3 p) =>
            !Physics.Raycast(p + Vector3.up * 0.2f, Vector3.up, 1.5f, FloorMask, QueryTriggerInteraction.Ignore);
    }
}
