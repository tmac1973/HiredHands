using System;

namespace VikingsForHire.Core
{
    public static class OrphanRules
    {
        /// <summary>Return-home trip time: roughly walking pace over the straight-line distance, clamped.</summary>
        public static float ReturnSeconds(float distanceMeters, float secondsPer100m, float minSeconds, float maxSeconds)
        {
            float raw = Math.Max(0f, distanceMeters) / 100f * secondsPer100m;
            return Math.Max(minSeconds, Math.Min(maxSeconds, raw));
        }
    }
}
