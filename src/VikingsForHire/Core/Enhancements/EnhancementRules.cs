using System;
using System.Collections.Generic;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>What a hireling owes, what may be chosen, and what a respec costs.</summary>
    public static class EnhancementRules
    {
        /// <summary>Levels from 2 up to the hireling's level with no choice made yet, lowest first.</summary>
        public static List<int> Owed(EnhancementPicks picks, int hirelingLevel)
        {
            var owed = new List<int>();
            for (int level = EnhancementCatalog.FirstLevel; level <= Math.Min(hirelingLevel, EnhancementCatalog.LastLevel); level++)
                if (!picks.Chosen(level))
                    owed.Add(level);
            return owed;
        }

        public static bool CanChoose(EnhancementPicks picks, int hirelingLevel, int level) =>
            level >= EnhancementCatalog.FirstLevel && level <= EnhancementCatalog.LastLevel && level <= hirelingLevel && !picks.Chosen(level);

        /// <summary>A share of the hire fee at its current level (each of food and coins rounded up).</summary>
        public static Cost RespecCost(CostCalculator costs, JobType job, int level, float fraction)
        {
            Cost fee = costs.HireCost(job, level);
            return new Cost((int)Math.Ceiling(fee.FoodPoints * fraction), (int)Math.Ceiling(fee.Coins * fraction));
        }
    }
}
