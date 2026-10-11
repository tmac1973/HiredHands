namespace VikingsForHire.Core
{
    /// <summary>
    /// What a hireling's stray hits may damage among props (trees, logs, rocks, destructibles). Players, hirelings, tames
    /// and buildings are never hit by any hireling (DamagePatches); gatherers' harvesting is applied directly
    /// (HarvestPatches.Direct) and isn't a stray hit.
    /// </summary>
    public static class SpellRules
    {
        /// <summary>
        /// Only a wizard's spells, and only with EnemiesAndTerrain. Other jobs' stray swings and arrows never have (a guard
        /// fighting by a tree doesn't fell it).
        /// </summary>
        public static bool MayHitProp(JobType job, SpellsHit setting) => job == JobType.Wizard && setting == SpellsHit.EnemiesAndTerrain;
    }
}
