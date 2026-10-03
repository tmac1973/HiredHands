using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Standing trees, then the logs and stumps they leave. Trees near player buildings are left alone (within
    /// TreeSafetyDistanceFromPieces, or within the tree's own height), so a falling trunk never lands on the base.
    /// </summary>
    internal sealed class WoodcutterProfile : IGatherProfile
    {
        private static readonly int Mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece_nonsolid", "terrain");
        private static readonly Collider[] Hits = new Collider[512];
        private static readonly List<Piece> Pieces = new();

        public string Status => "$vfh_status_chopping";

        public HashSet<string> PickupItems => new(DataStore.Current.Jobs.TryGetValue(JobType.Woodcutter, out var j) ? j.PickupItems : new List<string>());

        public IEnumerable<Component> Candidates(Vector3 center, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(center, radius, Hits, Mask);
            var seen = new HashSet<Component>();
            for (int i = 0; i < n; i++)
            {
                Component? c = (Component?)Hits[i].GetComponentInParent<TreeBase>() ?? Hits[i].GetComponentInParent<TreeLog>();
                if (c == null)
                {
                    Destructible d = Hits[i].GetComponentInParent<Destructible>();
                    if (d != null && d.m_destructibleType == DestructibleType.Tree)
                        c = d;
                }
                if (c != null && seen.Add(c))
                    yield return c;
            }
        }

        public bool IsValid(Component target, Hireling hireling, out string reason)
        {
            reason = "";
            if (target == null)
            {
                reason = "gone";
                return false;
            }
            int minTier = target switch
            {
                TreeBase t => t.m_minToolTier,
                TreeLog l => l.m_minToolTier,
                Destructible d => d.m_minToolTier,
                _ => 99,
            };
            int tier = hireling.ToolTier;
            if (tier < minTier)
            {
                reason = $"tier {tier}<{minTier}";
                return false;
            }
            return IsSafe(target, out reason);
        }

        /// <summary>Whether felling this is safe for nearby player pieces; reason names the closest piece when not.</summary>
        public static bool IsSafe(Component target, out string reason)
        {
            reason = "";
            float safety = VfhConfig.TreeSafetyDistanceFromPieces.Value;
            float radius = target is TreeBase ? Mathf.Max(safety, Height(target)) : 2f;
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(target.transform.position, radius, Pieces);
            Piece? closest = Pieces.Where(p => p != null && p.GetCreator() != 0L && p.GetComponent<Hireling>() == null)
                .OrderBy(p => Vector3.Distance(p.transform.position, target.transform.position)).FirstOrDefault();
            if (closest != null)
            {
                reason = $"near buildings ({closest.name.Replace("(Clone)", "")} {Vector3.Distance(closest.transform.position, target.transform.position):0.#}m, safe distance {radius:0.#}m)";
                return false;
            }
            return true;
        }

        public float StandOff(Component target) => target is TreeBase ? 1.6f : 1.4f;

        public HitData.DamageTypes SwingDamage(HitData.DamageTypes tool, float gatherMult) =>
            new() { m_chop = Mathf.Max(1f, tool.m_chop) * gatherMult };

        // Tallest a tree is assumed to be when deciding how far it can fall; guards against odd renderer bounds.
        private const float MaxTreeHeight = 30f;

        /// <summary>
        /// The tree's height above its base, from the meshes of its most detailed LOD only: whole-hierarchy renderer
        /// bounds include particle systems and far LOD billboards and come out around 80 m.
        /// </summary>
        public static float Height(Component tree)
        {
            IEnumerable<Renderer> renderers;
            LODGroup lod = tree.GetComponentInChildren<LODGroup>();
            if (lod != null && lod.GetLODs().Length > 0)
                renderers = lod.GetLODs()[0].renderers;
            else
                renderers = tree.GetComponentsInChildren<MeshRenderer>();
            float top = tree.transform.position.y;
            foreach (Renderer r in renderers)
                if (r is MeshRenderer && r.enabled)
                    top = Mathf.Max(top, r.bounds.max.y);
            return Mathf.Min(top - tree.transform.position.y, MaxTreeHeight);
        }
    }
}
