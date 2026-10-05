using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Nav
{
    /// <summary>The game map's answer about walking from one point to another: yes (with the walk's length), no, or not asked yet.</summary>
    public readonly struct LegAnswer
    {
        public bool Known { get; }
        public bool Walkable { get; }
        public float Cost { get; }

        private LegAnswer(bool known, bool walkable, float cost)
        {
            Known = known;
            Walkable = walkable;
            Cost = cost;
        }

        public static LegAnswer Yes(float cost) => new(true, true, cost);
        public static readonly LegAnswer No = new(true, false, 0f);
        public static readonly LegAnswer Unknown = new(false, false, 0f);
    }

    public enum RouteStepKind
    {
        /// <summary>Walk on the game's map to <see cref="RouteStep.To"/>.</summary>
        Walk,
        /// <summary>Go through a door: from its near side to <see cref="RouteStep.To"/>, its far side.</summary>
        Door,
        /// <summary>Take a stair or ladder from one end to <see cref="RouteStep.To"/>, the other.</summary>
        Stair,
    }

    public sealed class RouteStep
    {
        public RouteStepKind Kind { get; }
        public NavPoint To { get; }
        public NavLink? Link { get; }
        /// <summary>For a link: true when entering at its A end.</summary>
        public bool FromA { get; }

        public RouteStep(RouteStepKind kind, NavPoint to, NavLink? link = null, bool fromA = true)
        {
            Kind = kind;
            To = to;
            Link = link;
            FromA = fromA;
        }

        public override string ToString() => Kind == RouteStepKind.Walk ? "walk" : Link!.ToString();
    }

    public enum PlanKind
    {
        Route,
        NoRoute,
        /// <summary>No route with what's known yet: ask the game's map about <see cref="PlanResult.Unknown"/> and plan again.</summary>
        NeedLegs,
    }

    public sealed class PlanResult
    {
        public PlanKind Kind { get; init; }
        public List<RouteStep> Steps { get; init; } = new();
        public List<(NavPoint From, NavPoint To)> Unknown { get; init; } = new();
        public int OracleCalls { get; init; }

        public string Describe() => Kind == PlanKind.Route ? string.Join(">", Steps) : Kind.ToString();
    }

    /// <summary>
    /// Plans a route from start to goal over a board's links (A*): walking legs on the game's map between link ends,
    /// and the links themselves (doors, stairs). The game's map is asked about each leg through an oracle, which may not
    /// know yet; the planner then returns the legs to ask about instead of waiting.
    /// </summary>
    public static class LinkPlanner
    {
        /// <summary>How far (along the ground) a leg may reach to a link end.</summary>
        public const float LegRange = 30f;
        public const int StartGoalCandidates = 8;
        public const int NeighbourCandidates = 6;
        /// <summary>Most oracle questions in one plan (CPU bound; answers come from a cache); past that, legs count as unknown.</summary>
        public const int MaxOracleCalls = 2000;
        public const int MaxUnknownReturned = 16;

        private const int Start = -1;
        private const int Goal = -2;

        private readonly struct Edge
        {
            public readonly int To;
            public readonly float Cost;
            public readonly NavLink? Link;
            public readonly bool FromA;

            public Edge(int to, float cost, NavLink? link, bool fromA)
            {
                To = to;
                Cost = cost;
                Link = link;
                FromA = fromA;
            }
        }

        public static PlanResult Plan(NavGraph g, NavPoint start, NavPoint goal, Func<NavPoint, NavPoint, LegAnswer> oracle, double now)
        {
            int calls = 0;
            var unknown = new List<(NavPoint From, NavPoint To, float Guess)>();
            var nearGoal = new HashSet<int>(g.NearestEndpoints(goal, StartGoalCandidates, LegRange).Select(n => n.Index));

            NavPoint PointOf(int n) => n == Start ? start : n == Goal ? goal : g.Endpoints[n].Point;

            var best = new Dictionary<int, float> { [Start] = 0f };
            var cameFrom = new Dictionary<int, (int From, Edge Edge)>();
            var open = new SortedSet<(float F, int Seq, int Node)>();
            int seq = 0;
            open.Add((start.Distance(goal), seq++, Start));
            var closed = new HashSet<int>();

            while (open.Count > 0)
            {
                (float _, int _, int u) = open.Min;
                open.Remove(open.Min);
                if (!closed.Add(u))
                    continue;
                if (u == Goal)
                    return new PlanResult { Kind = PlanKind.Route, Steps = Steps(u, cameFrom, PointOf), OracleCalls = calls };

                float gu = best[u];
                foreach (Edge e in Edges(u))
                {
                    if (closed.Contains(e.To))
                        continue;
                    float gv = gu + e.Cost;
                    if (best.TryGetValue(e.To, out float old) && old <= gv)
                        continue;
                    best[e.To] = gv;
                    cameFrom[e.To] = (u, e);
                    open.Add((gv + PointOf(e.To).Distance(goal), seq++, e.To));
                }
            }

            if (unknown.Count > 0)
                return new PlanResult
                {
                    Kind = PlanKind.NeedLegs, OracleCalls = calls,
                    Unknown = unknown.OrderBy(x => x.Guess).Take(MaxUnknownReturned).Select(x => (x.From, x.To)).ToList(),
                };
            return new PlanResult { Kind = PlanKind.NoRoute, OracleCalls = calls };

            IEnumerable<Edge> Edges(int u)
            {
                NavPoint p = PointOf(u);
                // The links at this end.
                if (u >= 0)
                {
                    foreach ((NavLink link, bool atA) in g.Endpoints[u].Links)
                    {
                        if (link.Blocked(now))
                            continue;
                        int to = g.NodeOf(link, !atA);
                        if (to >= 0 && to != u)
                            yield return new Edge(to, link.Cost, link, atA);
                    }
                }
                // Walking legs: to the goal, and to nearby link ends.
                var targets = new List<int>();
                if (u == Start || nearGoal.Contains(u))
                    targets.Add(Goal);
                int max = u == Start ? StartGoalCandidates : NeighbourCandidates + 1;
                foreach (NavNode n in g.NearestEndpoints(p, max, LegRange))
                    if (n.Index != u)
                        targets.Add(n.Index);
                foreach (int t in targets)
                {
                    if (closed.Contains(t))
                        continue;
                    NavPoint q = PointOf(t);
                    LegAnswer a;
                    if (calls >= MaxOracleCalls)
                        a = LegAnswer.Unknown;
                    else
                    {
                        calls++;
                        a = oracle(p, q);
                    }
                    if (!a.Known)
                    {
                        unknown.Add((p, q, best[u] + p.Distance(q) + q.Distance(goal)));
                        continue;
                    }
                    if (a.Walkable)
                        yield return new Edge(t, Math.Max(a.Cost, 0.01f), null, true);
                }
            }
        }

        private static List<RouteStep> Steps(int goal, Dictionary<int, (int From, Edge Edge)> cameFrom, Func<int, NavPoint> pointOf)
        {
            var steps = new List<RouteStep>();
            int n = goal;
            while (cameFrom.TryGetValue(n, out var prev))
            {
                Edge e = prev.Edge;
                NavPoint to = pointOf(n);
                if (e.Link == null)
                    steps.Add(new RouteStep(RouteStepKind.Walk, to));
                else
                    steps.Add(new RouteStep(e.Link.Kind == NavLinkKind.Door ? RouteStepKind.Door : RouteStepKind.Stair,
                        e.Link.End(e.FromA), e.Link, e.FromA));
                n = prev.From;
            }
            steps.Reverse();
            return steps;
        }
    }
}
