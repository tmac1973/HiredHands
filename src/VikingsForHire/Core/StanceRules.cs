using System.Collections.Generic;

namespace VikingsForHire.Core
{
    public static class StanceRules
    {
        private static readonly Stance[] WorkerStances = { Stance.Flee, Stance.Defend };
        private static readonly Stance[] GuardStances = { Stance.Passive, Stance.Defensive, Stance.Aggressive };

        public static IReadOnlyList<Stance> Allowed(JobType job) => job.IsGuard() ? GuardStances : WorkerStances;

        public static Stance Default(JobType job) => job.IsGuard() ? Stance.Defensive : Stance.Defend;

        public static bool IsAllowed(JobType job, Stance stance)
        {
            foreach (Stance s in Allowed(job))
                if (s == stance)
                    return true;
            return false;
        }
    }
}
