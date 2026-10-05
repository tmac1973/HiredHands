using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core.Nav;
using Xunit;

namespace VikingsForHire.Tests
{
    public class StairProfileTests
    {
        // n samples 0.25 m apart along the axis, heights from a function of the distance.
        private static List<(float, float?)> Samples(int n, System.Func<float, float?> height, float spacing = 0.25f) =>
            Enumerable.Range(0, n).Select(i => (i * spacing, height(i * spacing))).ToList();

        private static float Steps(float along, float stepRun, float stepRise) => (float)System.Math.Floor(along / stepRun + 1e-4) * stepRise;

        [Fact]
        public void WoodStairIsAStair()
        {
            // 2 m up over 2 m in 0.25 m steps.
            StairResult r = StairProfile.Classify(Samples(9, a => Steps(a, 0.25f, 0.25f)), nameHint: false);
            Assert.True(r.Accepted, r.Reason);
            Assert.False(r.IsLadder);
            Assert.Equal(0, r.BottomIndex);
            Assert.Equal(8, r.TopIndex);
            Assert.InRange(r.Rise, 1.99f, 2.01f);
        }

        [Fact]
        public void SampledTheOtherWayRoundGivesBottomToTop()
        {
            var s = Samples(9, a => Steps(a, 0.25f, 0.25f));
            s.Reverse();
            StairResult r = StairProfile.Classify(s, false);
            Assert.True(r.Accepted, r.Reason);
            Assert.Equal(8, r.BottomIndex);
            Assert.Equal(0, r.TopIndex);
        }

        [Fact]
        public void FlatFloorIsTooLow() => Assert.Equal("too_low", StairProfile.Classify(Samples(9, _ => 0.5f), false).Reason);

        [Fact]
        public void RoofSlopePassesTheShapeTest()
        {
            // 26 degrees: 2 m over 4 m with no steps. The scanner rejects roofs by the floors at their ends instead.
            StairResult r = StairProfile.Classify(Samples(9, a => a * 0.5f, 0.5f), false);
            Assert.True(r.Accepted, r.Reason);
        }

        [Fact]
        public void WallHasNoSurface()
        {
            var s = Samples(9, a => (float?)null);
            s[4] = (1f, 2f);
            Assert.Equal("no_surface", StairProfile.Classify(s, false).Reason);
        }

        [Fact]
        public void StepladderNeedsItsName()
        {
            // 2 m over 1 m in 0.33 m steps: about 63 degrees.
            var s = Samples(7, a => Steps(a, 0.1667f, 0.3333f), 0.1667f);
            StairResult unnamed = StairProfile.Classify(s, false);
            Assert.False(unnamed.Accepted);
            Assert.Equal("too_steep", unnamed.Reason);
            StairResult named = StairProfile.Classify(s, true);
            Assert.True(named.Accepted, named.Reason);
            Assert.True(named.IsLadder);
        }

        [Fact]
        public void OneMissingSampleIsFineTwoAreNot()
        {
            var one = Samples(9, a => Steps(a, 0.25f, 0.25f));
            one[4] = (1f, null);
            Assert.True(StairProfile.Classify(one, false).Accepted);
            var two = Samples(9, a => Steps(a, 0.25f, 0.25f));
            two[3] = (0.75f, null);
            two[5] = (1.25f, null);
            Assert.Equal("gaps", StairProfile.Classify(two, false).Reason);
        }

        [Fact]
        public void ADipIsNotAStair()
        {
            var s = Samples(9, a => Steps(a, 0.25f, 0.25f));
            s[5] = (1.25f, 0.8f); // 0.2 m below the step before
            Assert.Equal("not_monotonic", StairProfile.Classify(s, false).Reason);
        }

        [Fact]
        public void OneTallStepIsTooSteep() =>
            Assert.Equal("too_steep", StairProfile.Classify(Samples(9, a => a < 1f ? 0f : 1.2f), false).Reason);

        [Fact]
        public void ANameAllowsALowerRise()
        {
            var s = Samples(9, a => Steps(a, 0.25f, 0.1f)); // 0.8 m
            Assert.Equal("too_low", StairProfile.Classify(s, false).Reason);
            Assert.True(StairProfile.Classify(s, true).Accepted);
        }
    }
}
