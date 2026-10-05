using System;

namespace VikingsForHire.Core.Chores
{
    /// <summary>
    /// How urgent each kind of job is, 0 (nothing to do) to 1, and the score the Steward picks by: urgency less a small
    /// penalty for distance, so a near job wins between two equally urgent ones but never over a much more urgent one.
    /// </summary>
    public static class ChoreUrgency
    {
        public const float DistanceWeight = 0.1f;

        /// <summary>A fire below the refill fraction: 0.5 just under it, 1 when empty.</summary>
        public static float Fire(float fuel, float max, float refill)
        {
            float line = refill * max;
            if (max <= 0f || line <= 0f || fuel >= line)
                return 0f;
            return 0.5f + 0.5f * (1f - Math.Max(0f, fuel) / line);
        }

        /// <summary>A station below the refill threshold: 0.4 just under, 0.9 when empty; output waiting is at least 0.6.</summary>
        public static float Station(float oreFrac, float fuelFrac, bool outputWaiting, float threshold)
        {
            float low = Math.Min(oreFrac, fuelFrac);
            float u = low < threshold ? 0.4f + 0.5f * (1f - Math.Max(0f, low)) : 0f;
            return outputWaiting ? Math.Max(u, 0.6f) : u;
        }

        /// <summary>A beehive or sap collector: nothing below half full, then 0.5 rising to 0.7 when full.</summary>
        public static float Producer(int level, int max)
        {
            if (max <= 0 || level * 2 < max)
                return 0f;
            return 0.3f + 0.4f * Math.Min(1f, (float)level / max);
        }

        public static float Animal(bool hungry) => hungry ? 0.7f : 0f;

        /// <summary>A fermenter: ready to tap 0.6; empty (with a mead base to put in) 0.45.</summary>
        public static float Fermenter(bool ready, bool empty) => ready ? 0.6f : empty ? 0.45f : 0f;

        /// <summary>A shield generator below the refill fraction, as a fire.</summary>
        public static float Shield(float fuel, float max, float refill) => Fire(fuel, max, refill);

        /// <summary>Tidying up: below every other chore's lowest urgency (0.3), so anything else comes first.</summary>
        public static float Tidy() => 0.15f;

        /// <summary>A damaged piece: nothing at or above the line, 0.3 + 0.6 × damage below it.</summary>
        public static float Repair(float health, float below)
        {
            if (health >= below)
                return 0f;
            return 0.3f + 0.6f * (1f - Math.Max(0f, health));
        }

        public static float Score(float urgency, float distance, float radius) =>
            urgency - DistanceWeight * Math.Min(radius > 0f ? distance / radius : 1f, 1f);
    }
}
