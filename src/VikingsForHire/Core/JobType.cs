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
        /// <summary>A caster: fights with a staff's main spell from range (plan/wizard).</summary>
        Wizard,
    }

    public static class JobTypeExtensions
    {
        /// <summary>The fighting jobs: guards, archers and wizards. Posts, attack orders and guard stances go by this.</summary>
        public static bool IsGuard(this JobType job) => job == JobType.GuardMelee || job == JobType.GuardRanged || job == JobType.Wizard;

        /// <summary>
        /// Counts against the board's combat cap (guards today; casters later), not its worker cap. What a job is, so it's
        /// decided here, not in the data file: a server can change the numbers, not turn a guard into a worker.
        /// </summary>
        public static bool IsCombat(this JobType job) => job.IsGuard();
    }
}
