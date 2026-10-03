namespace VikingsForHire.Core
{
    public enum JobType
    {
        Woodcutter,
        Miner,
        Smelter,
        GuardMelee,
        GuardRanged,
    }

    public static class JobTypeExtensions
    {
        public static bool IsGuard(this JobType job) => job == JobType.GuardMelee || job == JobType.GuardRanged;
    }
}
