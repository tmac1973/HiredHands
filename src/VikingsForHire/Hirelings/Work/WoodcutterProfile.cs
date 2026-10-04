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
        private static readonly Collider[] Hits = new Collider[2048];
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
            // Logs aren't on the layers above, so they come from the registry.
            foreach (TreeLog log in LogRegistry.Within(center, radius))
                if (seen.Add(log))
                    yield return log;
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
            return true;
        }

        public bool Plan(Component target, out Vector3? fellDir, out string reason) => PlanFelling(target, out fellDir, out reason);

        /// <summary>
        /// A felled tree falls the way it was hit, so pick a direction whose fall line (the tree's height, plus a
        /// corridor either side for the crown) has no player-built pieces in it, preferring straight away from them.
        /// Nothing may be within TreeSafetyDistanceFromPieces of the trunk at all. Logs and stumps only need 2 m.
        /// </summary>
        public static bool PlanFelling(Component target, out Vector3? fellDir, out string reason)
        {
            fellDir = null;
            reason = "";
            Vector3 at = target.transform.position;
            if (target is not TreeBase)
            {
                Piece? close = ClosestPiece(at, 2f);
                if (close != null)
                {
                    reason = $"near buildings ({Describe(close, at)} < 2m)";
                    return false;
                }
                return true;
            }

            float height = Height(target);
            float clear = VfhConfig.TreeSafetyDistanceFromPieces.Value;
            Piece? closest = ClosestPiece(at, clear);
            if (closest != null)
            {
                reason = $"near buildings ({Describe(closest, at)}, needs {clear:0.#}m clear around the trunk)";
                return false;
            }
            float half = VfhConfig.TreeFallCorridorHalfWidth.Value;
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(at, height + half + 1f, Pieces);
            var offsets = Pieces.Where(IsPlayerPiece).Select(p => Flat(p.transform.position - at)).ToList();
            if (offsets.Count == 0)
                return true; // nothing it could land on: fall any way

            Vector3 away = -offsets.Aggregate(Vector3.zero, (sum, v) => sum + v.normalized);
            away = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.forward;
            Vector3? best = null;
            float bestDot = float.MinValue;
            for (int i = 0; i < 24; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
                if (offsets.Any(v => Blocks(v, dir, height, half)))
                    continue;
                float dot = Vector3.Dot(dir, away);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = dir;
                }
            }
            if (best == null)
            {
                reason = $"near buildings (no clear direction to fell a {height:0}m tree)";
                return false;
            }
            fellDir = best;
            return true;
        }

        // Is a piece at offset v (flat, from the trunk) under a tree of this height falling along dir?
        private static bool Blocks(Vector3 v, Vector3 dir, float height, float half)
        {
            float along = Vector3.Dot(v, dir);
            if (along < -1f || along > height + 1f)
                return false;
            return (v - dir * along).magnitude < half;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static bool IsPlayerPiece(Piece p) => p != null && p.GetCreator() != 0L && p.GetComponent<Hireling>() == null;

        private static Piece? ClosestPiece(Vector3 at, float radius)
        {
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(at, radius, Pieces);
            return Pieces.Where(IsPlayerPiece).OrderBy(p => Vector3.Distance(p.transform.position, at)).FirstOrDefault();
        }

        private static string Describe(Piece p, Vector3 at) =>
            $"{p.name.Replace("(Clone)", "")} {Vector3.Distance(p.transform.position, at):0.#}m";

        // Fallen logs first (they're the wood, and they block paths), then stumps, then standing trees.
        public int Rank(Component target) => target switch
        {
            TreeLog => 0,
            TreeBase => 2,
            _ => 1,
        };

        // A felled tree's log can land up to a tree's height away and then roll: follow it out of the area.
        public float ExtraReach(Component target) => target is TreeLog ? MaxTreeHeight : 0f;

        public float WorkReach => 3f;

        public bool GiveUpOnPart(Component target, Collider part) => false;

        public bool NeedsDigging(Collider part) => false;

        // A standing tree yields what its logs yield; a log, its own drops and its smaller logs'; a small tree or stump,
        // its destroy drops.
        public IEnumerable<string> Yield(Component target)
        {
            var items = new List<string>();
            void Add(DropTable? t)
            {
                if (t?.m_drops == null)
                    return;
                foreach (DropTable.DropData d in t.m_drops)
                    if (d.m_item != null)
                        items.Add(d.m_item.name);
            }
            void AddLogs(GameObject? log)
            {
                for (int depth = 0; log != null && depth < 5; depth++)
                {
                    TreeLog? tl = log.GetComponent<TreeLog>();
                    if (tl == null)
                        break;
                    Add(tl.m_dropWhenDestroyed);
                    log = tl.m_subLogPrefab;
                }
            }
            switch (target)
            {
                case TreeBase tree:
                    Add(tree.m_dropWhenDestroyed);
                    AddLogs(tree.m_logPrefab);
                    break;
                case TreeLog log:
                    Add(log.m_dropWhenDestroyed);
                    AddLogs(log.m_subLogPrefab);
                    break;
                default:
                    Add(target.GetComponent<DropOnDestroyed>()?.m_dropWhenDestroyed);
                    break;
            }
            return items;
        }

        public Collider? Aim(Component target, Vector3 from, out Vector3 point)
        {
            if (target is TreeLog)
            {
                // A log is long: aim at the nearest point of its surface, not its middle, or a woodcutter at one end
                // would never be "in reach".
                Collider? best = null;
                float bestSq = float.MaxValue;
                foreach (Collider c in target.GetComponentsInChildren<Collider>())
                {
                    if (!c.enabled || c.isTrigger)
                        continue;
                    float sq = (c.bounds.ClosestPoint(from) - from).sqrMagnitude;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        best = c;
                    }
                }
                point = best != null ? MinerProfile.Surface(best, from) : target.transform.position;
                return best;
            }
            point = target.transform.position;
            return target.GetComponentInChildren<Collider>();
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
