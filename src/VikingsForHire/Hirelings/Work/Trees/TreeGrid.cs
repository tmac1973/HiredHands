using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;

namespace VikingsForHire.Hirelings.Work.Trees
{
    /// <summary>
    /// Free planting spots in a patch for a tree kind: a grid at the sapling's spacing from the sign, where the game lets it
    /// grow (biome, not too hot or cold, room around it, open sky, a usable ward) and far enough from buildings that the
    /// grown tree can be felled.
    /// </summary>
    internal static class TreeGrid
    {
        private static readonly int SpaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        private static readonly int RoofMask = LayerMask.GetMask("Default", "static_solid", "piece");
        private static readonly Collider[] Hits = new Collider[16];
        private static readonly List<Piece> Pieces = new();
        private static readonly Dictionary<Vector3Int, float> Avoided = new();

        private static Vector3Int Cell(Vector3 p) => new(Mathf.RoundToInt(p.x * 2f), 0, Mathf.RoundToInt(p.z * 2f));

        /// <summary>A spot the woodcutter couldn't get to: left out for 10 minutes.</summary>
        public static void Avoid(Vector3 p) => Avoided[Cell(p)] = Time.time + 600f;

        public static List<Vector3> Spots(TreePatch patch, TreeKind kind, long owner, int max)
        {
            float spacing = Mathf.Max(1f, kind.GrowRadius * 2f);
            Vector3 c = patch.transform.position;
            float r = patch.Radius;
            int n = Mathf.CeilToInt(r / spacing);
            var spots = new List<Vector3>();
            for (int i = -n; i <= n; i++)
                for (int j = -n; j <= n; j++)
                {
                    Vector3 p = c + new Vector3(i * spacing, 0f, j * spacing);
                    if ((i == 0 && j == 0) || Utils.DistanceXZ(p, c) > r)
                        continue;
                    p.y = ZoneSystem.instance.GetGroundHeight(p);
                    if (IsFree(kind, p, owner))
                        spots.Add(p);
                }
            return spots.OrderBy(p => Utils.DistanceXZ(p, c)).Take(max).ToList();
        }

        public static bool IsFree(TreeKind kind, Vector3 p, long owner)
        {
            if (Avoided.TryGetValue(Cell(p), out float until) && until > Time.time)
                return false;
            Heightmap? hm = Heightmap.FindHeightmap(p);
            if (hm == null || (kind.NeedCultivated && !hm.IsCultivated(p)))
                return false;
            Heightmap.Biome biome = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(p) : kind.Biome;
            if ((biome & kind.Biome) == 0)
                return false;
            bool hot = biome == Heightmap.Biome.AshLands && !kind.TolerateHeat;
            bool cold = (biome == Heightmap.Biome.Mountain || biome == Heightmap.Biome.DeepNorth) && !kind.TolerateCold;
            if ((hot || cold) && !ShieldGenerator.IsInsideShield(p))
                return false;
            if (Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.1f, kind.GrowRadius, Hits, SpaceMask) > 0)
                return false;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, 100f, RoofMask))
                return false;
            // Far enough from buildings (the sign aside) that the woodcutter will fell it when it's grown.
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(p, VfhConfig.TreeSafetyDistanceFromPieces.Value, Pieces);
            if (Pieces.Any(x => x != null && x.GetCreator() != 0L && x.GetComponent<TreePatch>() == null && x.GetComponent<Hireling>() == null && x.GetComponent<Plant>() == null))
                return false;
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || !area.IsEnabled() || !area.IsInside(p, 0f))
                    continue;
                if (owner == 0L || (area.m_piece.GetCreator() != owner && !area.IsPermitted(owner)))
                    return false;
            }
            return true;
        }
    }
}
