using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class LevelRulesTests
    {
        private readonly LevelRules _rules = new(DefaultData.Create());

        [Theory]
        [InlineData(1, 1)]
        [InlineData(5, 5)]
        [InlineData(8, 8)]
        [InlineData(12, 8)]
        [InlineData(0, 1)]
        public void MaxHirelingLevelFollowsBoard(int board, int expected) => Assert.Equal(expected, _rules.MaxHirelingLevel(board));

        [Fact]
        public void CapsAndRadius()
        {
            Assert.Equal(2, _rules.HirelingCap(1));
            Assert.Equal(10, _rules.HirelingCap(8));
            Assert.Equal(60f, _rules.MaxWorkRadius(8));
        }

        [Fact]
        public void RadiusClamped()
        {
            Assert.Equal(20f, _rules.ClampRadius(1, 50f));
            Assert.Equal(LevelRules.MinWorkRadius, _rules.ClampRadius(1, 2f));
            Assert.Equal(25f, _rules.ClampRadius(3, 25f));
        }

        [Fact]
        public void GatherersGetALargerRadius()
        {
            Assert.Equal(40f, _rules.MaxWorkRadius(1, JobType.Woodcutter));
            Assert.Equal(20f, _rules.MaxWorkRadius(1, JobType.GuardMelee));
            Assert.Equal(60f, _rules.ClampRadius(3, JobType.Miner, 200f));
            Assert.Equal(30f, _rules.ClampRadius(3, JobType.GuardRanged, 200f));
        }

        [Fact]
        public void MinersNeedBoardLevel2()
        {
            Assert.False(_rules.JobUnlocked(1, JobType.Miner));
            Assert.True(_rules.JobUnlocked(2, JobType.Miner));
            Assert.True(_rules.JobUnlocked(1, JobType.Woodcutter));
        }

        [Fact]
        public void StoneTable()
        {
            Assert.Equal(1, _rules.StoneFollowerCap(1));
            Assert.Equal(4, _rules.StoneFollowerCap(4));
            Assert.Equal(2, _rules.RequiredBoardLevelForStone(1));
            Assert.Equal(8, _rules.RequiredBoardLevelForStone(4));
        }
    }
}
