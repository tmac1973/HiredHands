using System;

namespace VikingsForHire.Core
{
    public enum OrphanReason
    {
        None,
        /// <summary>Following, but too far from its owner for too long (left on a shore, outrun on a cliff…).</summary>
        TooFar,
        /// <summary>Left in Stay (or Gather Here) while its owner went far away for a long time.</summary>
        LeftStaying,
        /// <summary>Its owner logged out (or dropped) away from home and hasn't come back.</summary>
        OwnerOffline,
    }

    /// <summary>Thresholds for sending lost followers home (from config).</summary>
    public sealed class OrphanLimits
    {
        public float FollowDistance { get; set; } = 60f;
        public float FollowSeconds { get; set; } = 30f;
        public float StayDistance { get; set; } = 150f;
        public float StaySeconds { get; set; } = 120f;
        public float OfflineSeconds { get; set; } = 30f;
    }

    public static class OrphanRules
    {
        /// <summary>Return-home trip time: roughly walking pace over the straight-line distance, clamped.</summary>
        public static float ReturnSeconds(float distanceMeters, float secondsPer100m, float minSeconds, float maxSeconds)
        {
            float raw = Math.Max(0f, distanceMeters) / 100f * secondsPer100m;
            return Math.Max(minSeconds, Math.Min(maxSeconds, raw));
        }

        /// <summary>How far from its owner a follower in this mode may be before the clock starts.</summary>
        public static float DistanceLimit(FollowMode mode, OrphanLimits limits) =>
            mode == FollowMode.Follow ? limits.FollowDistance : limits.StayDistance;

        /// <summary>
        /// Whether a follower should head home. <paramref name="secondsBeyond"/> is how long it has been continuously
        /// farther than <see cref="DistanceLimit"/> from its (online) owner; <paramref name="secondsOffline"/> how long its
        /// owner has been gone. Followers inside their board's area never head home this way (they go back to work),
        /// and passengers aboard a ship stay with their (online) owner.
        /// </summary>
        public static OrphanReason Check(FollowMode mode, bool insideHome, bool stowed, bool ownerOnline, float secondsBeyond,
            float secondsOffline, OrphanLimits limits)
        {
            if (insideHome)
                return OrphanReason.None;
            if (!ownerOnline)
                return secondsOffline >= limits.OfflineSeconds ? OrphanReason.OwnerOffline : OrphanReason.None;
            if (stowed)
                return OrphanReason.None;
            if (mode == FollowMode.Follow)
                return secondsBeyond >= limits.FollowSeconds ? OrphanReason.TooFar : OrphanReason.None;
            return secondsBeyond >= limits.StaySeconds ? OrphanReason.LeftStaying : OrphanReason.None;
        }
    }
}
