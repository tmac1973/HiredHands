namespace VikingsForHire.Core
{
    /// <summary>What happens when a hireling dies (setting DeathMode).</summary>
    public enum DeathMode
    {
        /// <summary>Gone for good: the contract ends.</summary>
        Permadeath,
        /// <summary>Back after RespawnCooldownSeconds, once the board pays RespawnCostFraction of the hire fee.</summary>
        PayToRespawn,
        /// <summary>Back by itself, free, after ReturnAfterDays in-game days.</summary>
        ReturnAfterDays,
    }

    public static class DeathRules
    {
        /// <summary>Whether a dead hireling's contract ends (otherwise it waits and the hireling comes back).</summary>
        public static bool EndsContract(DeathMode mode) => mode == DeathMode.Permadeath;

        /// <summary>Whether the board pays to bring it back.</summary>
        public static bool Pays(DeathMode mode) => mode == DeathMode.PayToRespawn;

        /// <summary>
        /// Seconds of world time before a dead hireling comes back: the cooldown when paying, the days (times the game's day
        /// length) when it returns by itself. Sleeping moves world time on, so a night skips that much of the wait.
        /// </summary>
        public static double ReturnDelay(DeathMode mode, double cooldownSeconds, double returnDays, double dayLengthSeconds) =>
            mode == DeathMode.ReturnAfterDays ? System.Math.Max(0.0, returnDays) * dayLengthSeconds : cooldownSeconds;

        /// <summary>
        /// The 0.6 setting PermadeathEnabled, for configs from before DeathMode: true was permadeath, false paying to respawn.
        /// Null when it isn't a bool.
        /// </summary>
        public static DeathMode? FromPermadeathSetting(string value) => value.Trim().ToLowerInvariant() switch
        {
            "true" => DeathMode.Permadeath,
            "false" => DeathMode.PayToRespawn,
            _ => null,
        };
    }
}
