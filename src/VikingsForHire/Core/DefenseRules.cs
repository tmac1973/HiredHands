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

        /// <summary>
        /// Which ways to try rolling, best first, as flat (x, z) unit vectors. <paramref name="fromX"/>/<paramref name="fromZ"/>
        /// point from the hireling towards the attack. Area attacks: straight away first. Projectiles: out of the line of
        /// flight first. Melee: back and to one side first (alternate <paramref name="leftFirst"/> so it isn't predictable).
        /// </summary>
        public static (float X, float Z)[] DodgeDirections(float fromX, float fromZ, bool area, bool projectile, bool leftFirst)
        {
            float len = (float)System.Math.Sqrt(fromX * fromX + fromZ * fromZ);
            if (len < 1e-4f)
            {
                fromX = 0f;
                fromZ = 1f;
            }
            else
            {
                fromX /= len;
                fromZ /= len;
            }
            (float, float) away = (-fromX, -fromZ);
            (float, float) left = (-fromZ, fromX); // facing the attack, left
            (float, float) right = (fromZ, -fromX);
            (float, float) backLeft = Norm(away.Item1 + left.Item1, away.Item2 + left.Item2);
            (float, float) backRight = Norm(away.Item1 + right.Item1, away.Item2 + right.Item2);
            (float, float) side1 = leftFirst ? left : right, side2 = leftFirst ? right : left;
            (float, float) diag1 = leftFirst ? backLeft : backRight, diag2 = leftFirst ? backRight : backLeft;
            if (area)
                return new[] { away, diag1, diag2, side1, side2 };
            if (projectile)
                return new[] { side1, side2, diag1, diag2 };
            return new[] { diag1, diag2, side1, side2, away };
        }

        private static (float, float) Norm(float x, float z)
        {
            float l = (float)System.Math.Sqrt(x * x + z * z);
            return (x / l, z / l);
        }

        /// <summary>
        /// Where something moving in a straight line passes closest to a point: the distance then, and the time from now
        /// (negative when it's moving away; then the distance is the current one).
        /// </summary>
        public static (float Distance, float Time) ClosestApproach(float px, float py, float pz, float vx, float vy, float vz,
            float tx, float ty, float tz)
        {
            float dx = tx - px, dy = ty - py, dz = tz - pz;
            float vv = vx * vx + vy * vy + vz * vz;
            float now = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (vv < 1e-6f)
                return (now, -1f);
            float t = (dx * vx + dy * vy + dz * vz) / vv;
            if (t < 0f)
                return (now, t);
            float cx = px + vx * t - tx, cy = py + vy * t - ty, cz = pz + vz * t - tz;
            return ((float)System.Math.Sqrt(cx * cx + cy * cy + cz * cz), t);
        }
    }
}
