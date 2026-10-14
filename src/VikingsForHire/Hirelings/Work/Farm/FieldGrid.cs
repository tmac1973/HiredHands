using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Compat;

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>
    /// Where a crop can be planted: a grid at the crop's spacing (PlantEasily's when it's loaded), lined up with the plants
    /// already in the field so rows continue the player's, over cultivated ground in the radius. A spot is free when the
    /// game would let the plant grow there: cultivated, the crop's biome, nothing within its grow radius, open sky, and a
    /// ward the board's owner may use. Checked spots are cached for 30 s.
    /// </summary>
    internal static class FieldGrid
    {
        private const float CacheSeconds = 30f;
        private const int MaxSpots = 400;
        private static readonly int SpaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        private static readonly int RoofMask = LayerMask.GetMask("Default", "static_solid", "piece");
        private static readonly Collider[] Hits = new Collider[16];
        private static readonly Dictionary<(string, Vector3, float, float), (float At, List<Vector3> Spots)> Cache = new();
        private static readonly List<Plant> Plants = new();
        private static readonly List<Pickable> Picks = new();

        public readonly struct Grid
        {
            public readonly Vector3 Origin, Row, Across;
            public readonly float Spacing;

            public Grid(Vector3 origin, Vector3 row, float spacing)
            {
                Origin = origin;
                Row = row;
                Across = Vector3.Cross(Vector3.up, row);
                Spacing = spacing;
            }

            public Vector3 At(int i, int j) => Origin + Row * (i * Spacing) + Across * (j * Spacing);

            /// <summary>How far a point is from the nearest grid node (on the ground plane).</summary>
            public float Offset(Vector3 p)
            {
                Vector3 d = p - Origin;
                float u = Vector3.Dot(d, Row) / Spacing, v = Vector3.Dot(d, Across) / Spacing;
                Vector3 node = At(Mathf.RoundToInt(u), Mathf.RoundToInt(v));
                return Utils.DistanceXZ(p, node);
            }
        }

        /// <summary>The grid for a crop: from the plant nearest home and the direction to its nearest neighbour, else the fallback axis.</summary>
        public static Grid For(Crop crop, Vector3 home, float radius, Vector3 fallbackRow, float spaceMult = 1f)
        {
            float spacing = PlantMods.CropSpacing(crop.GrowRadius * spaceMult);
            FarmScan.Nearby(home, radius, Plants, Picks);
            List<Vector3> placed = Plants.Select(p => p.transform.position)
                .Concat(Picks.Where(p => CropCatalog.ByGrown(Utils.GetPrefabName(p.gameObject)) is Crop c && !c.Info.Regrowing).Select(p => p.transform.position)).ToList();
            Vector3 row = new Vector3(fallbackRow.x, 0f, fallbackRow.z).normalized;
            if (placed.Count == 0)
                return new Grid(home, row.sqrMagnitude > 0.5f ? row : Vector3.right, spacing);
            Vector3 origin = placed.OrderBy(p => Utils.DistanceXZ(p, home)).First();
            // A neighbour straight along a row (about one spacing away), never a diagonal one: lining up with a diagonal would
            // turn the grid 45° and put new plants between the old rows.
            Vector3? neighbour = placed.Where(p => p != origin && Utils.DistanceXZ(p, origin) < spacing * 1.2f).OrderBy(p => Utils.DistanceXZ(p, origin)).Cast<Vector3?>().FirstOrDefault();
            if (neighbour is Vector3 n)
            {
                Vector3 d = n - origin;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    row = d.normalized;
            }
            return new Grid(origin, row.sqrMagnitude > 0.5f ? row : Vector3.right, spacing);
        }

        /// <summary>Free spots for a crop in the radius, nearest home first.</summary>
        /// <param name="spaceMult">The share of the crop's grow room to plant at (Close planting, an enhancement: under 1).</param>
        public static List<Vector3> Spots(Crop crop, Vector3 home, float radius, Vector3 fallbackRow, long owner, float spaceMult = 1f)
        {
            var key = (crop.Info.Plant, home, radius, spaceMult);
            if (Cache.TryGetValue(key, out var hit) && Time.time - hit.At < CacheSeconds)
                return hit.Spots;
            foreach (Vector3Int expired in Avoided.Where(kv => kv.Value < Time.time).Select(kv => kv.Key).ToList())
                Avoided.Remove(expired);
            Grid g = For(crop, home, radius, fallbackRow, spaceMult);
            List<Vector3> regrowing = Regrowing(Picks);
            var spots = new List<Vector3>();
            int n = Mathf.CeilToInt(radius * 2f / g.Spacing) + 2;
            Vector3 c = home - g.Origin;
            int ci = Mathf.RoundToInt(Vector3.Dot(c, g.Row) / g.Spacing), cj = Mathf.RoundToInt(Vector3.Dot(c, g.Across) / g.Spacing);
            for (int i = ci - n / 2; i <= ci + n / 2; i++)
                for (int j = cj - n / 2; j <= cj + n / 2; j++)
                {
                    Vector3 p = g.At(i, j);
                    if (Utils.DistanceXZ(p, home) > radius)
                        continue;
                    p.y = ZoneSystem.instance.GetGroundHeight(p);
                    if (IsFree(crop, p, owner, regrowing, spaceMult))
                        spots.Add(p);
                }
            spots = spots.OrderBy(p => Utils.DistanceXZ(p, home)).Take(MaxSpots).ToList();
            Cache[key] = (Time.time, spots);
            return spots;
        }

        public static void Forget() => Cache.Clear();

        private const float AvoidSeconds = 600f;
        private static readonly Dictionary<Vector3Int, float> Avoided = new();

        private static Vector3Int Cell(Vector3 p) => new(Mathf.RoundToInt(p.x * 4f), 0, Mathf.RoundToInt(p.z * 4f));

        /// <summary>A spot the Farmer couldn't get to: left out for 10 minutes.</summary>
        public static void Avoid(Vector3 p)
        {
            Avoided[Cell(p)] = Time.time + AvoidSeconds;
            Cache.Clear();
        }

        // Regrowing plants (mushrooms, berry bushes, dandelions…) among the pickables found: picked, they hide the part
        // that collides, so the physics check below sees open ground where the stalk stands.
        private static List<Vector3> Regrowing(List<Pickable> picks) =>
            picks.Where(p => p != null && CropCatalog.ByGrown(Utils.GetPrefabName(p.gameObject)) is Crop c && c.Info.Regrowing)
                .Select(p => p.transform.position).ToList();

        private static readonly List<Plant> CheckPlants = new();
        private static readonly List<Pickable> CheckPicks = new();

        /// <summary>
        /// Whether the game would let this crop grow here (checked again just before planting), and no regrowing plant
        /// stands there, picked or not (carrots went in among picked mushrooms). <paramref name="regrowing"/>: those
        /// already found nearby, or null to look.
        /// </summary>
        public static bool IsFree(Crop crop, Vector3 p, long owner, List<Vector3>? regrowing = null, float spaceMult = 1f)
        {
            float grow = crop.GrowRadius * spaceMult;
            if (regrowing == null)
            {
                FarmScan.Nearby(p, grow + 1f, CheckPlants, CheckPicks);
                regrowing = Regrowing(CheckPicks);
            }
            float clear = grow + 0.3f;
            if (regrowing.Any(r => Utils.DistanceXZ(r, p) < clear))
                return false;
            if (Avoided.TryGetValue(Cell(p), out float until) && until > Time.time)
                return false;
            Heightmap? hm = Heightmap.FindHeightmap(p);
            if (hm == null || (crop.NeedCultivated && !hm.IsCultivated(p)))
                return false;
            Heightmap.Biome biome = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(p) : crop.Biome;
            if ((biome & crop.Biome) == 0)
                return false;
            // As the plant's own health check: too hot in the Ashlands, too cold in the Mountains and Deep North, unless a shield covers it.
            bool hot = biome == Heightmap.Biome.AshLands && !crop.TolerateHeat;
            bool cold = (biome == Heightmap.Biome.Mountain || biome == Heightmap.Biome.DeepNorth) && !crop.TolerateCold;
            if ((hot || cold) && !ShieldGenerator.IsInsideShield(p))
                return false;
            // The plant's own checks: nothing solid within its grow radius, open sky above.
            int hits = Physics.OverlapSphereNonAlloc(p, grow, Hits, SpaceMask);
            // Any collider there blocks it (another plant included: two in one spot both wither).
            for (int k = 0; k < hits; k++)
                if (Hits[k] != null)
                    return false;
            if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.up, 100f, RoofMask))
                return false;
            return OwnerMayUse(p, owner);
        }

        private static bool OwnerMayUse(Vector3 at, long owner)
        {
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || !area.IsEnabled() || !area.IsInside(at, 0f))
                    continue;
                if (owner == 0L || (area.m_piece.GetCreator() != owner && !area.IsPermitted(owner)))
                    return false;
            }
            return true;
        }
    }
}
