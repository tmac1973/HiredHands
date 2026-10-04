using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class FundsForecastTests
    {
        [Fact]
        public void FoodAndCoinsCountSeparately()
        {
            (int food, int coins) = FundsForecast.DaysLeft(new Cost(250, 30), new Cost(100, 20));
            Assert.Equal(2, food);
            Assert.Equal(1, coins);
            Assert.Equal(1, FundsForecast.Days(new Cost(250, 30), new Cost(100, 20)));
        }

        [Fact]
        public void NothingToPayLastsForever()
        {
            Assert.Equal(int.MaxValue, FundsForecast.Days(new Cost(0, 0), Cost.Zero));
            (int food, int coins) = FundsForecast.DaysLeft(new Cost(80, 0), new Cost(40, 0));
            Assert.Equal(2, food);
            Assert.Equal(int.MaxValue, coins);
        }

        [Fact]
        public void EmptyFundsAreZeroDays() => Assert.Equal(0, FundsForecast.Days(Cost.Zero, new Cost(40, 5)));
    }
}
