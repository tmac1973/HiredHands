using System;
using VikingsForHire.Core.Data;

namespace VikingsForHire.Core
{
    public sealed class CostCalculator
    {
        private readonly VfhData _data;
        private readonly float _respawnFraction;

        public CostCalculator(VfhData data, float respawnFraction)
        {
            _data = data;
            _respawnFraction = respawnFraction;
        }

        public Cost HireCost(JobType job, int level)
        {
            HirelingLevelData l = Level(level);
            return Priced(job, level, l.HireFood, l.HireCoins);
        }

        public Cost DailyUpkeep(JobType job, int level)
        {
            HirelingLevelData l = Level(level);
            return Priced(job, level, l.UpkeepFood, l.UpkeepCoins);
        }

        /// <summary>What it costs to raise a hireling from one level to a higher one: the difference in hire fees.</summary>
        public Cost PromotionCost(JobType job, int fromLevel, int toLevel) => HireCost(job, toLevel) - HireCost(job, fromLevel);

        public Cost RespawnCost(JobType job, int level) => HireCost(job, level).Scale(_respawnFraction);

        private Cost Priced(JobType job, int level, int food, int coins)
        {
            float mult = _data.Jobs.TryGetValue(job, out JobData? j) ? j.CostMult : 1f;
            // Gold isn't available in the Meadows: level 1 is food-only whatever the table says.
            int c = level == 1 ? 0 : (int)Math.Round(coins * mult, MidpointRounding.AwayFromZero);
            return new Cost((int)Math.Round(food * mult, MidpointRounding.AwayFromZero), c);
        }

        private HirelingLevelData Level(int level)
        {
            if (level < 1 || level > _data.HirelingLevels.Count)
                throw new ArgumentOutOfRangeException(nameof(level), level, "no such hireling level");
            return _data.HirelingLevels[level - 1];
        }
    }
}
