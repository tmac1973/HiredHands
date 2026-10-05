using VikingsForHire.Core.Nav;
using Xunit;

namespace VikingsForHire.Tests
{
    public class NavGraphTests
    {
        private static NavLink Door(float x) => new(NavLinkKind.Door, new NavPoint(x - 1.2f, 0, 0), new NavPoint(x + 1.2f, 0, 0), null, "d", "wood_door");

        [Fact]
        public void EndsCloseTogetherMerge()
        {
            var g = new NavGraph();
            var stair = new NavLink(NavLinkKind.Stair, new NavPoint(1.4f, 0, 0), new NavPoint(1.4f, 2, 2), null, "s", "wood_stair");
            g.Rebuild(new[] { Door(0f), stair });
            Assert.Equal(3, g.Endpoints.Count); // the door's far side and the stair's foot are 0.2 m apart
            Assert.Equal(g.NodeOf(g.Links[0], atA: false), g.NodeOf(g.Links[1], atA: true));
        }

        [Fact]
        public void RebuildBumpsTheVersionAndRenumbers()
        {
            var g = new NavGraph();
            g.Rebuild(new[] { Door(0f) });
            int v = g.Version;
            g.Rebuild(new[] { Door(10f), Door(20f) });
            Assert.Equal(v + 1, g.Version);
            Assert.Equal(2, g.Links.Count);
            Assert.Equal(2, g.Links[1].Id);
        }

        [Fact]
        public void BlockedUntilItsTime()
        {
            var g = new NavGraph();
            g.Rebuild(new[] { Door(0f) });
            g.Block(1, 100.0);
            Assert.True(g.Link(1)!.Blocked(99.0));
            Assert.False(g.Link(1)!.Blocked(100.0));
        }
    }
}
