using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class OrphanRulesTests
    {
        [Theory]
        [InlineData(800f, 200f)]
        [InlineData(100f, 60f)]
        [InlineData(0f, 60f)]
        [InlineData(10000f, 1200f)]
        [InlineData(-50f, 60f)]
        public void ReturnSecondsClamped(float distance, float expected) =>
            Assert.Equal(expected, OrphanRules.ReturnSeconds(distance, 25f, 60f, 1200f), 3);

        private static readonly OrphanLimits L = new();

        [Theory]
        // mode, insideHome, stowed, online, beyond, offline, expected
        [InlineData(FollowMode.Follow, false, false, true, 29f, 0f, OrphanReason.None)]
        [InlineData(FollowMode.Follow, false, false, true, 30f, 0f, OrphanReason.TooFar)]
        [InlineData(FollowMode.GatherHere, false, false, true, 60f, 0f, OrphanReason.None)]
        [InlineData(FollowMode.GatherHere, false, false, true, 120f, 0f, OrphanReason.LeftStaying)]
        [InlineData(FollowMode.Stay, false, false, true, 119f, 0f, OrphanReason.None)]
        [InlineData(FollowMode.Stay, false, false, true, 120f, 0f, OrphanReason.LeftStaying)]
        [InlineData(FollowMode.Follow, true, false, true, 999f, 0f, OrphanReason.None)]
        [InlineData(FollowMode.Follow, true, false, false, 0f, 999f, OrphanReason.None)]
        [InlineData(FollowMode.Stay, false, false, false, 0f, 29f, OrphanReason.None)]
        [InlineData(FollowMode.Stay, false, false, false, 0f, 30f, OrphanReason.OwnerOffline)]
        [InlineData(FollowMode.Follow, false, true, true, 999f, 0f, OrphanReason.None)]
        [InlineData(FollowMode.Follow, false, true, false, 0f, 30f, OrphanReason.OwnerOffline)]
        public void Triggers(FollowMode mode, bool insideHome, bool stowed, bool online, float beyond, float offline, OrphanReason expected) =>
            Assert.Equal(expected, OrphanRules.Check(mode, insideHome, stowed, online, beyond, offline, L));

        [Fact]
        public void DistanceLimitByMode()
        {
            Assert.Equal(60f, OrphanRules.DistanceLimit(FollowMode.Follow, L));
            Assert.Equal(150f, OrphanRules.DistanceLimit(FollowMode.Stay, L));
            Assert.Equal(150f, OrphanRules.DistanceLimit(FollowMode.GatherHere, L));
        }
    }
}
