namespace VikingsForHire.Core
{
    public enum JobType
    {
        Woodcutter,
        Miner,
        Smelter,
        GuardMelee,
        GuardRanged,
        Farmer,
        Cook,
    }

    public static class JobTypeExtensions
    {
        public static bool IsGuard(this JobType job) => job == JobType.GuardMelee || job == JobType.GuardRanged;

        /// <summary>Jobs a board may have only one of (they share the board's production orders).</summary>
        public static bool OnePerBoard(this JobType job) => job == JobType.Farmer || job == JobType.Cook;
    }
}
