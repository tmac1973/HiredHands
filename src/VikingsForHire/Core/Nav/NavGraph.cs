using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Nav
{
    /// <summary>A link end; ends within <see cref="NavGraph.MergeDistance"/> share one (a door opening onto a stair).</summary>
    public sealed class NavNode
    {
        public int Index { get; internal set; }
        public NavPoint Point { get; internal set; }
        public List<(NavLink Link, bool AtA)> Links { get; } = new();
    }

    /// <summary>One board area's links. Rebuilt whole on every scan; <see cref="Version"/> tells routes they're stale.</summary>
    public sealed class NavGraph
    {
        public const float MergeDistance = 0.5f;

        private readonly List<NavLink> _links = new();
        private readonly List<NavNode> _nodes = new();
        private readonly Dictionary<int, (int A, int B)> _ends = new();

        public int Version { get; private set; }
        public IReadOnlyList<NavLink> Links => _links;
        public IReadOnlyList<NavNode> Endpoints => _nodes;

        public void Rebuild(IEnumerable<NavLink> links)
        {
            _links.Clear();
            _nodes.Clear();
            _ends.Clear();
            foreach (NavLink l in links)
            {
                l.Id = _links.Count + 1;
                _links.Add(l);
                NavNode a = Node(l.A), b = Node(l.B);
                a.Links.Add((l, true));
                b.Links.Add((l, false));
                _ends[l.Id] = (a.Index, b.Index);
            }
            Version++;
        }

        /// <summary>The node index at one end of a link (its A end when <paramref name="atA"/>).</summary>
        public int NodeOf(NavLink link, bool atA) => _ends.TryGetValue(link.Id, out var e) ? (atA ? e.A : e.B) : -1;

        public NavLink? Link(int id) => id >= 1 && id <= _links.Count ? _links[id - 1] : null;

        public void Block(int linkId, double until)
        {
            if (Link(linkId) is NavLink l)
                l.BlockedUntil = until;
        }

        /// <summary>Up to <paramref name="max"/> ends nearest to the point (along the ground), within <paramref name="within"/> m.</summary>
        public List<NavNode> NearestEndpoints(NavPoint point, int max, float within) =>
            _nodes.Where(n => n.Point.DistanceXZ(point) <= within)
                .OrderBy(n => n.Point.Distance(point))
                .Take(max)
                .ToList();

        private NavNode Node(NavPoint p)
        {
            foreach (NavNode n in _nodes)
                if (n.Point.Distance(p) <= MergeDistance)
                    return n;
            var node = new NavNode { Index = _nodes.Count, Point = p };
            _nodes.Add(node);
            return node;
        }
    }
}
