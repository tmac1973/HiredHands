using System;

namespace VikingsForHire.Core
{
    /// <summary>A price in food points and coins. Never negative.</summary>
    public readonly record struct Cost
    {
        public int FoodPoints { get; }
        public int Coins { get; }

        public Cost(int foodPoints, int coins)
        {
            FoodPoints = Math.Max(0, foodPoints);
            Coins = Math.Max(0, coins);
        }

        public static readonly Cost Zero = new(0, 0);

        public static Cost operator +(Cost a, Cost b) => new(a.FoodPoints + b.FoodPoints, a.Coins + b.Coins);
        public static Cost operator -(Cost a, Cost b) => new(a.FoodPoints - b.FoodPoints, a.Coins - b.Coins);

        public Cost Scale(float factor) =>
            new((int)Math.Round(FoodPoints * factor, MidpointRounding.AwayFromZero),
                (int)Math.Round(Coins * factor, MidpointRounding.AwayFromZero));

        public bool CoveredBy(Cost available) => available.FoodPoints >= FoodPoints && available.Coins >= Coins;

        public override string ToString() => $"{FoodPoints}fp+{Coins}c";
    }
}

namespace VikingsForHire.Core
{
    /// <summary>How long a board's funds last at its daily upkeep.</summary>
    public static class FundsForecast
    {
        /// <summary>Whole days the funds pay for, food and coins separately; int.MaxValue for a part that costs nothing a day.</summary>
        public static (int FoodDays, int CoinDays) DaysLeft(Cost funds, Cost daily) =>
            (daily.FoodPoints <= 0 ? int.MaxValue : funds.FoodPoints / daily.FoodPoints,
             daily.Coins <= 0 ? int.MaxValue : funds.Coins / daily.Coins);

        /// <summary>The fewer of the two (int.MaxValue when nothing costs anything).</summary>
        public static int Days(Cost funds, Cost daily)
        {
            (int food, int coins) = DaysLeft(funds, daily);
            return Math.Min(food, coins);
        }
    }
}
