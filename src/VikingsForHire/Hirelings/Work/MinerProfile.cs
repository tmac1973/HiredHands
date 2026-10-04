using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Rocks and ore deposits: MineRock5 (copper, large rocks, scrap piles), MineRock (tin, obsidian and other small
    /// deposits) and plain destructibles that drop something the miner collects (small rocks, boulders). Ore comes
    /// before plain stone. Chunks buried deep in the ground are left alone; low ones are reached by digging, which miners
    /// only do in the field (TerrainProtectionPatches). Rocks right next to player pieces, which may be holding them up,
    /// are left alone too.
    /// </summary>
    internal sealed class MinerProfile : IGatherProfile
    {
        private static readonly int Mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
        private static readonly Collider[] Hits = new Collider[1024];
        private static readonly List<Piece> Pieces = new();

        // A chunk whose top is this far below the ground is buried; one whose bottom is this far above it is out of reach.
        private const float BuriedBelow = 0.3f;
        private const float ReachAbove = 3.5f;
        // Buried chunks down to this depth (top below the ground) are dug out where digging is allowed (the field).
        private const float DigDepth = 1.5f;

        private readonly Hireling _hireling;

        public MinerProfile(Hireling hireling) => _hireling = hireling;

        public string Status => "$vfh_status_mining";

        public HashSet<string> PickupItems => new(DataStore.Current.Jobs.TryGetValue(JobType.Miner, out var j) ? j.PickupItems : new List<string>());

        public IEnumerable<Component> Candidates(Vector3 center, float radius)
        {
            HashSet<string> wanted = PickupItems;
            int n = Physics.OverlapSphereNonAlloc(center, radius, Hits, Mask);
            var seen = new HashSet<Component>();
            for (int i = 0; i < n; i++)
            {
                Component? c = (Component?)Hits[i].GetComponentInParent<MineRock5>() ?? Hits[i].GetComponentInParent<MineRock>();
                if (c == null)
                {
                    Destructible d = Hits[i].GetComponentInParent<Destructible>();
                    if (d != null && d.m_destructibleType == DestructibleType.Default)
                        c = d;
                }
                if (c != null && seen.Add(c) && Drops(c).Any(wanted.Contains))
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
                MineRock5 r => r.m_minToolTier,
                MineRock r => r.m_minToolTier,
                Destructible d => d.m_minToolTier,
                _ => 99,
            };
            if (hireling.ToolTier < minTier)
            {
                reason = $"tier {hireling.ToolTier}<{minTier}";
                return false;
            }
            return true;
        }

        public bool Plan(Component target, out Vector3? fellDir, out string reason)
        {
            fellDir = null;
            reason = "";
            if (!Areas(target).Any(c => (Workable(c) || Diggable(c)) && !IsBad(c)))
            {
                reason = "only buried or out-of-reach chunks left";
                return false;
            }
            // Never dig up a deposit nobody has found: silver veins are hidden underground on purpose (the Wishbone
            // finds them). Digging only goes on at a deposit that shows something above ground or that someone has
            // already broken into (a stone order is checked the same way; you can't click a vein through the dirt anyway).
            if (!Areas(target).Any(Workable) && !BrokenInto(target))
            {
                reason = "buried and undiscovered";
                return false;
            }
            float clear = VfhConfig.MinerSafetyDistanceFromPieces.Value;
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(target.transform.position, clear + Extent(target), Pieces);
            Piece? close = Pieces.FirstOrDefault(p => p != null && p.GetCreator() != 0L && p.GetComponent<Hireling>() == null);
            if (close != null)
            {
                reason = $"near buildings ({close.name.Replace("(Clone)", "")} {Vector3.Distance(close.transform.position, target.transform.position):0.#}m)";
                return false;
            }
            return true;
        }

        // Ore first, then stone.
        public int Rank(Component target) => Drops(target).Any(d => d != "Stone") ? 0 : 1;

        public float ExtraReach(Component target) => 0f;

        // Copper deposits are big and lumpy: the last chunks often sit in a hollow or under an overhang of the rest, where
        // a miner can't get within a pickaxe's length. A long reach beats leaving the ore behind.
        public float WorkReach => 4.5f;

        // Chunks of a deposit this miner couldn't reach or break, until when. A deposit is one target made of many
        // chunks; giving up on the whole deposit for one buried chunk left plenty of surface copper unmined.
        private readonly Dictionary<Collider, float> _badParts = new();
        private const float BadPartSeconds = 120f;

        public bool GiveUpOnPart(Component target, Collider part)
        {
            _badParts[part] = Time.time + BadPartSeconds;
            return Areas(target).Any(c => (Workable(c) || Diggable(c)) && !IsBad(c));
        }

        private bool IsBad(Collider c)
        {
            if (!_badParts.TryGetValue(c, out float until))
                return false;
            if (Time.time < until)
                return true;
            _badParts.Remove(c);
            return false;
        }

        public Collider? Aim(Component target, Vector3 from, out Vector3 point)
        {
            Collider? best = null;
            float bestSq = float.MaxValue;
            foreach (Collider c in Areas(target).Where(c => Workable(c) && !IsBad(c)))
            {
                float sq = (c.bounds.ClosestPoint(from) - from).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = c;
                }
            }
            if (best != null)
            {
                point = Surface(best, from);
                return best;
            }
            // Nothing to hit above ground: dig towards the shallowest buried chunk, aiming at the dirt right above it.
            Collider? dig = Areas(target).Where(c => Diggable(c) && !IsBad(c)).OrderByDescending(c => c.bounds.max.y).FirstOrDefault();
            if (dig != null)
            {
                Vector3 above = dig.bounds.center;
                above.y = ZoneSystem.instance.GetGroundHeight(above);
                point = above;
                return dig;
            }
            point = target.transform.position;
            return null;
        }

        private static bool BrokenInto(Component target) =>
            target is MineRock5 r5 && r5.m_hitAreas != null && r5.m_hitAreas.Any(a => a.m_health < r5.m_health);

        public bool NeedsDigging(Collider part) => part != null && !Workable(part) && Diggable(part);

        // Buried, but shallow enough to dig out, and where this miner may dig.
        private bool Diggable(Collider c)
        {
            Bounds b = c.bounds;
            float ground = ZoneSystem.instance.GetGroundHeight(b.center);
            return b.max.y <= ground - BuriedBelow && b.max.y > ground - DigDepth && TerrainProtectionPatches.MayDig(_hireling, b.center);
        }

        /// <summary>
        /// The point on the chunk's actual surface facing <paramref name="from"/>: a ray from chest height towards the
        /// chunk's centre. Its bounding box can stick out a metre or more past the visible rock, which made miners
        /// swing at thin air and still break the ore. Falls back to the box when the ray misses.
        /// </summary>
        public static Vector3 Surface(Collider c, Vector3 from)
        {
            Vector3 eye = from + Vector3.up * 1.2f;
            Vector3 centre = c.bounds.center;
            var ray = new Ray(eye, (centre - eye).normalized);
            return c.Raycast(ray, out RaycastHit hit, Vector3.Distance(eye, centre) + 1f) ? hit.point : c.bounds.ClosestPoint(from);
        }

        public float StandOff(Component target) => 1.1f;

        public HitData.DamageTypes SwingDamage(HitData.DamageTypes tool, float gatherMult) =>
            new() { m_pickaxe = Mathf.Max(1f, tool.m_pickaxe) * gatherMult };

        /// <summary>The intact chunks of a rock: for a MineRock5 its areas with health left, for the others their active colliders.</summary>
        private static IEnumerable<Collider> Areas(Component target)
        {
            switch (target)
            {
                case MineRock5 r5:
                    if (r5.m_hitAreas != null)
                        foreach (var a in r5.m_hitAreas)
                            if (a.m_health > 0f && a.m_collider != null)
                                yield return a.m_collider;
                    break;
                case MineRock r:
                    if (r.m_hitAreas != null)
                        foreach (Collider c in r.m_hitAreas)
                            if (c != null && c.gameObject.activeInHierarchy)
                                yield return c;
                    break;
                default:
                    foreach (Collider c in target.GetComponentsInChildren<Collider>())
                        if (c.enabled && !c.isTrigger)
                            yield return c;
                    break;
            }
        }

        private static bool Workable(Collider c)
        {
            Bounds b = c.bounds;
            float ground = ZoneSystem.instance.GetGroundHeight(b.center);
            return b.max.y > ground - BuriedBelow && b.min.y < ground + ReachAbove;
        }

        private static float Extent(Component target)
        {
            Bounds? all = null;
            foreach (Collider c in Areas(target))
            {
                if (all is Bounds b)
                {
                    b.Encapsulate(c.bounds);
                    all = b;
                }
                else
                {
                    all = c.bounds;
                }
            }
            return all is Bounds x ? Mathf.Max(x.extents.x, x.extents.z) : 1f;
        }

        /// <summary>
        /// What mining it yields. An unbroken deposit (rock4_copper, Rock_3, silvervein…) is a Destructible that turns
        /// into its chunked "_frac" MineRock5 on the first hit, so its yield is that prefab's.
        /// </summary>
        private static IEnumerable<string> Drops(Component target)
        {
            DropTable? table = target switch
            {
                MineRock5 r5 => r5.m_dropItems,
                MineRock r => r.m_dropItems,
                Destructible d => d.GetComponent<DropOnDestroyed>()?.m_dropWhenDestroyed,
                _ => null,
            };
            if ((table?.m_drops == null || table.m_drops.Count == 0) && target is Destructible { m_spawnWhenDestroyed: GameObject spawn })
            {
                Component? inner = (Component?)spawn.GetComponent<MineRock5>() ?? spawn.GetComponent<MineRock>();
                if (inner != null)
                {
                    foreach (string item in Drops(inner))
                        yield return item;
                }
                yield break;
            }
            if (table?.m_drops == null)
                yield break;
            foreach (DropTable.DropData d in table.m_drops)
                if (d.m_item != null)
                    yield return d.m_item.name;
        }
    }
}
