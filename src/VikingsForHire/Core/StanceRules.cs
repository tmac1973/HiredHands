using System.Collections.Generic;

namespace VikingsForHire.Core
{
    public enum CombatAction
    {
        None,
        Flee,
        Engage,
    }

    public static class StanceRules
    {
        private static readonly Stance[] WorkerStances = { Stance.Flee, Stance.Defend };
        private static readonly Stance[] GuardStances = { Stance.Passive, Stance.Defensive, Stance.Aggressive };

        public static IReadOnlyList<Stance> Allowed(JobType job) => job.IsGuard() ? GuardStances : WorkerStances;

        public static Stance Default(JobType job) => job.IsGuard() ? Stance.Defensive : Stance.Defend;

        public const float FleeTriggerRange = 12f;
        public const float DefendTriggerRange = 4f;
        public const float AggressiveExtraRange = 10f;

        /// <summary>
        /// What a hireling does about a threat, by stance. threatDistance is null when nothing hostile is in sight;
        /// threatInRadius means it's inside the work radius around home.
        /// </summary>
        public static CombatAction Decide(Stance stance, float? threatDistance, bool threatInRadius, bool threatInExtendedRadius,
            bool wasAttacked, bool allyAttacked)
        {
            switch (stance)
            {
                case Stance.Flee:
                    return wasAttacked || threatDistance <= FleeTriggerRange ? CombatAction.Flee : CombatAction.None;
                case Stance.Defend:
                    return wasAttacked || threatDistance <= DefendTriggerRange ? CombatAction.Engage : CombatAction.None;
                case Stance.Passive:
                    return wasAttacked ? CombatAction.Engage : CombatAction.None;
                case Stance.Defensive:
                    return wasAttacked || allyAttacked || (threatDistance.HasValue && threatInRadius) ? CombatAction.Engage : CombatAction.None;
                case Stance.Aggressive:
                    return wasAttacked || allyAttacked || (threatDistance.HasValue && (threatInRadius || threatInExtendedRadius))
                        ? CombatAction.Engage : CombatAction.None;
                default:
                    return CombatAction.None;
            }
        }

        /// <summary>How far past the work radius a guard chases before giving up and going back.</summary>
        public static float LeashBeyondRadius(Stance stance) => stance == Stance.Aggressive ? 25f : 15f;

        /// <summary>Badly hurt hirelings retreat (except aggressive guards) until they've recovered some health.</summary>
        public static bool ShouldRetreat(Stance stance, bool isGuard, float healthFraction, bool retreating) =>
            !(isGuard && stance == Stance.Aggressive) && (retreating ? healthFraction < 0.4f : healthFraction < 0.25f);

        public static bool IsAllowed(JobType job, Stance stance)
        {
            foreach (Stance s in Allowed(job))
                if (s == stance)
                    return true;
            return false;
        }
    }
}
