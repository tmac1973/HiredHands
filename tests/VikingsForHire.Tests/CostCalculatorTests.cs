using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class CostCalculatorTests
    {
        private static CostCalculator Calc(VfhData? data = null) => new(data ?? DefaultData.Create(), 0.5f);

        [Fact]
        public void Level1IsFoodOnlyEvenIfTableHasCoins()
        {
            VfhData data = DefaultData.Create();
            data.HirelingLevels[0].HireCoins = 99;
            data.HirelingLevels[0].UpkeepCoins = 7;
            Assert.Equal(new Cost(150, 0), Calc(data).HireCost(JobType.Woodcutter, 1));
            Assert.Equal(new Cost(40, 0), Calc(data).DailyUpkeep(JobType.Woodcutter, 1));
        }

        [Fact]
        public void JobMultiplierApplies()
        {
            // L2 guard: 250 * 1.3 = 325 food, 50 * 1.3 = 65 coins.
            Assert.Equal(new Cost(325, 65), Calc().HireCost(JobType.GuardMelee, 2));
            // L3 miner upkeep: 90 * 1.1 = 99, 10 * 1.1 = 11.
            Assert.Equal(new Cost(99, 11), Calc().DailyUpkeep(JobType.Miner, 3));
        }

        [Fact]
        public void PromotionIsHireFeeDifference()
        {
            CostCalculator c = Calc();
            Assert.Equal(c.HireCost(JobType.Smelter, 4) - c.HireCost(JobType.Smelter, 2), c.PromotionCost(JobType.Smelter, 2, 4));
            Assert.Equal(new Cost(100, 50), c.PromotionCost(JobType.Woodcutter, 1, 2));
        }

        [Fact]
        public void RespawnIsFractionOfHire() =>
            Assert.Equal(new Cost(200, 75), Calc().RespawnCost(JobType.Woodcutter, 3));

        [Fact]
        public void CostNeverNegative()
        {
            Assert.Equal(Cost.Zero, new Cost(10, 5) - new Cost(20, 10));
            Assert.Equal(new Cost(0, 0), new Cost(-3, -1));
        }

        [Fact]
        public void BadLevelThrows() =>
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Calc().HireCost(JobType.Miner, 9));
    }
}
