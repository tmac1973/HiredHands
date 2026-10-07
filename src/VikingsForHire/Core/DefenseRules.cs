namespace VikingsForHire.Core
{
    /// <summary>
    /// The rules behind hirelings blocking and dodging (0.6.0), kept free of Unity so they can be unit tested. The chances
    /// and the dodge cooldown come from the levels table; these are the fixed parts.
    /// </summary>
    public static class DefenseRules
    {
        /// <summary>A hit taking more than this share of current health is worth rolling away from.</summary>
        public const float BigHitFraction = 0.25f;

        /// <summary>
        /// How long before the hit a parrying guard raises its shield: inside vanilla's 0.25 s timed-block window, with
        /// room for a slow frame.
        /// </summary>
        public const float ParryLeadSeconds = 0.15f;

        /// <summary>How long before the hit a guard that saw it coming but won't parry raises its shield (outside the window).</summary>
        public const float EarlyBlockLeadSeconds = 0.45f;

        /// <summary>How long before the hit a roll starts.</summary>
        public const float DodgeLeadSeconds = 0.30f;

        /// <summary>The least time before a hit that a roll can still start in.</summary>
        public const float DodgeLatestSeconds = 0.15f;

        public static bool WouldHurtALot(float damageAfterArmor, float currentHealth, bool area) =>
            area || damageAfterArmor > currentHealth * BigHitFraction;

        /// <summary>A chance roll; the random number (0-1) is passed in so tests can choose it.</summary>
        public static bool Roll(float chance, float random01) => random01 < chance;

        public static bool DodgeReady(float now, float lastDodge, float cooldown) => now - lastDodge >= cooldown;
    }
}
