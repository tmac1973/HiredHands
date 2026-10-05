using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VikingsForHire.Core.Nav;
using Xunit;

namespace VikingsForHire.Tests
{
    public class LinkPlannerTests
    {
        // A house split by a wall at x = 0 (door through it), with an upper floor above y = 1 on the right.
        private static int Region(NavPoint p) => (p.X < 0 ? 0 : 1) + (p.Y > 1 ? 2 : 0);

        private static LegAnswer SameRoom(NavPoint a, NavPoint b) => Region(a) == Region(b) ? LegAnswer.Yes(a.Distance(b)) : LegAnswer.No;

        private static NavLink Door() => new(NavLinkKind.Door, new NavPoint(-1.2f, 0, 0), new NavPoint(1.2f, 0, 0), null, "door", "wood_door");

        private static NavLink Stair(float z) =>
            new(NavLinkKind.Stair, new NavPoint(4, 0, z), new NavPoint(4, 2, z + 2), new[] { new NavPoint(4, 0, z), new NavPoint(4, 2, z + 2) }, $"stair{z}", "wood_stair");

        private static NavGraph Graph(params NavLink[] links)
        {
            var g = new NavGraph();
            g.Rebuild(links);
            return g;
        }

        private static string Kinds(PlanResult r) => string.Join(",", r.Steps.Select(s => s.Kind));

        [Fact]
        public void ThroughOneDoor()
        {
            PlanResult r = LinkPlanner.Plan(Graph(Door()), new NavPoint(-5, 0, 0), new NavPoint(5, 0, 1), SameRoom, 0);
            Assert.Equal(PlanKind.Route, r.Kind);
            Assert.Equal("Walk,Door,Walk", Kinds(r));
            Assert.True(r.Steps[1].FromA);
            Assert.Equal(new NavPoint(1.2f, 0, 0), r.Steps[1].To);
        }

        [Fact]
        public void DoorThenStairsUpstairs()
        {
            PlanResult r = LinkPlanner.Plan(Graph(Door(), Stair(3)), new NavPoint(-5, 0, 0), new NavPoint(6, 2, 0), SameRoom, 0);
            Assert.Equal("Walk,Door,Walk,Stair,Walk", Kinds(r));
            Assert.Equal(new NavPoint(4, 2, 5), r.Steps[3].To);
        }

        [Fact]
        public void DownstairsTakesTheStairFromTheTop()
        {
            PlanResult r = LinkPlanner.Plan(Graph(Door(), Stair(3)), new NavPoint(6, 2, 0), new NavPoint(-5, 0, 0), SameRoom, 0);
            Assert.Equal("Walk,Stair,Walk,Door,Walk", Kinds(r));
            Assert.False(r.Steps[1].FromA);
            Assert.False(r.Steps[3].FromA);
        }

        [Fact]
        public void ABlockedStairIsAvoided()
        {
            NavGraph g = Graph(Door(), Stair(3), Stair(-8));
            g.Block(2, 100);
            PlanResult r = LinkPlanner.Plan(g, new NavPoint(6, 0, 0), new NavPoint(6, 2, 0), SameRoom, now: 50);
            Assert.Equal(PlanKind.Route, r.Kind);
            Assert.Equal(3, r.Steps.Single(s => s.Kind == RouteStepKind.Stair).Link!.Id);
            PlanResult later = LinkPlanner.Plan(g, new NavPoint(6, 0, 0), new NavPoint(6, 2, 0), SameRoom, now: 150);
            Assert.Equal(2, later.Steps.Single(s => s.Kind == RouteStepKind.Stair).Link!.Id); // the nearer one again
        }

        [Fact]
        public void NoWayThroughIsNoRoute() =>
            Assert.Equal(PlanKind.NoRoute, LinkPlanner.Plan(Graph(Door()), new NavPoint(-5, 0, 0), new NavPoint(5, 0, 0), (_, _) => LegAnswer.No, 0).Kind);

        [Fact]
        public void UnknownLegsAreAskedForThenRouted()
        {
            var known = new Dictionary<(NavPoint, NavPoint), LegAnswer>();
            LegAnswer Oracle(NavPoint a, NavPoint b) => known.TryGetValue((a, b), out LegAnswer x) ? x : LegAnswer.Unknown;
            NavGraph g = Graph(Door(), Stair(3));
            var start = new NavPoint(-5, 0, 0);
            var goal = new NavPoint(6, 2, 0);
            PlanResult r = LinkPlanner.Plan(g, start, goal, Oracle, 0);
            Assert.Equal(PlanKind.NeedLegs, r.Kind);
            for (int round = 0; round < 6 && r.Kind == PlanKind.NeedLegs; round++)
            {
                Assert.InRange(r.Unknown.Count, 1, LinkPlanner.MaxUnknownReturned);
                foreach ((NavPoint a, NavPoint b) in r.Unknown)
                    known[(a, b)] = SameRoom(a, b);
                r = LinkPlanner.Plan(g, start, goal, Oracle, 0);
            }
            Assert.Equal("Walk,Door,Walk,Stair,Walk", Kinds(r));
        }

        [Fact]
        public void BigBaseStaysCheap()
        {
            // 100 doors (200 ends) on a 10 x 10 grid, 5 m apart; the map lets you walk 8 m.
            var links = new List<NavLink>();
            for (int i = 0; i < 10; i++)
            for (int j = 0; j < 10; j++)
                links.Add(new NavLink(NavLinkKind.Door, new NavPoint(i * 5f, 0, j * 5f), new NavPoint(i * 5f + 2.4f, 0, j * 5f), null, $"d{i}{j}", "wood_door"));
            NavGraph g = Graph(links.ToArray());
            LegAnswer Near(NavPoint a, NavPoint b) => a.Distance(b) <= 8f ? LegAnswer.Yes(a.Distance(b)) : LegAnswer.No;
            var start = new NavPoint(-3, 0, -3);
            var goal = new NavPoint(50, 0, 48);
            LinkPlanner.Plan(g, start, goal, Near, 0); // warm up
            var sw = Stopwatch.StartNew();
            PlanResult r = LinkPlanner.Plan(g, start, goal, Near, 0);
            sw.Stop();
            Assert.True(r.Kind == PlanKind.Route, $"{r.Kind} after {r.OracleCalls} calls");
            Assert.True(r.OracleCalls <= LinkPlanner.MaxOracleCalls, $"{r.OracleCalls} calls");
            Assert.True(sw.Elapsed.TotalMilliseconds < 5.0, $"{sw.Elapsed.TotalMilliseconds:0.00} ms");
        }
    }
}
