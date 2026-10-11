namespace VikingsForHire.Core
{
    /// <summary>
    /// What a hireling's hits may damage among props (trees, logs, rocks, destructibles). Players, hirelings, tames and
    /// buildings are never hit by any hireling (DamagePatches), so only props are decided here.
    /// </summary>
    public static class SpellRules
    {
        /// <summary>
        /// Wizards only with EnemiesAndTerrain; every other job always (woodcutters and miners work by hitting props, and
        /// a guard's swing was never limited).
        /// </summary>
        public static bool MayHitProp(JobType job, SpellsHit setting) => job != JobType.Wizard || setting == SpellsHit.EnemiesAndTerrain;
    }
}
