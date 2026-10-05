using System;
using System.Collections.Generic;

namespace VikingsForHire.Core.Nav
{
    /// <summary>The outcome of <see cref="StairProfile.Classify"/>: accepted with its bottom and top, or why not.</summary>
    public readonly struct StairResult
    {
        public bool Accepted { get; init; }
        public string Reason { get; init; }
        /// <summary>Indices into the samples given (bottom first, whichever way round they were sampled).</summary>
        public int BottomIndex { get; init; }
        public int TopIndex { get; init; }
        public bool IsLadder { get; init; }
        public float Rise { get; init; }
        public float SlopeDeg { get; init; }

        public static StairResult Reject(string reason, float rise = 0f, float slope = 0f) =>
            new() { Accepted = false, Reason = reason, BottomIndex = -1, TopIndex = -1, Rise = rise, SlopeDeg = slope };
    }

    /// <summary>
    /// Whether a piece joins two floors, judged from the height of its walkable surface sampled along one of its axes (the
    /// game side casts the rays). Any modded staircase is found this way, by its shape, whatever it's called.
    /// </summary>
    public static class StairProfile
    {
        /// <summary>Least height change that counts as going to another floor.</summary>
        public const float MinRise = 1.0f;
        /// <summary>The same with a name like "stair"/"ladder"/"step" (or the data file's include list).</summary>
        public const float MinRiseNamed = 0.6f;
        /// <summary>A stair never goes down along the way by more than this (sampling noise on step edges).</summary>
        public const float DropTolerance = 0.15f;
        /// <summary>Highest single step a walker manages between two samples.</summary>
        public const float MaxStep = 0.7f;
        /// <summary>Steeper than this is a ladder (if it's named like one) or a wall.</summary>
        public const float MaxStairSlopeDeg = 60f;

        public static StairResult Classify(IReadOnlyList<(float Along, float? Height)> samples, bool nameHint)
        {
            // Drop the empty ends; allow one gap inside (a missed step edge).
            int first = 0, last = samples.Count - 1;
            while (first <= last && samples[first].Height == null)
                first++;
            while (last >= first && samples[last].Height == null)
                last--;
            var kept = new List<(float Along, float Height, int Index)>();
            int gaps = 0;
            for (int i = first; i <= last; i++)
            {
                if (samples[i].Height is float h)
                    kept.Add((samples[i].Along, h, i));
                else
                    gaps++;
            }
            if (kept.Count < 3)
                return StairResult.Reject("no_surface");
            if (gaps > 1)
                return StairResult.Reject("gaps");

            // Always bottom to top.
            if (kept[kept.Count - 1].Height < kept[0].Height)
                kept.Reverse();
            float rise = kept[kept.Count - 1].Height - kept[0].Height;
            float run = Math.Abs(kept[kept.Count - 1].Along - kept[0].Along);
            float slope = (float)(Math.Atan2(rise, Math.Max(run, 0.001f)) * 180.0 / Math.PI);
            if (rise < (nameHint ? MinRiseNamed : MinRise))
                return StairResult.Reject("too_low", rise, slope);

            float maxStep = 0f;
            for (int i = 1; i < kept.Count; i++)
            {
                float d = kept[i].Height - kept[i - 1].Height;
                if (d < -DropTolerance)
                    return StairResult.Reject("not_monotonic", rise, slope);
                maxStep = Math.Max(maxStep, d);
            }

            bool stair = maxStep <= MaxStep && slope <= MaxStairSlopeDeg;
            bool ladder = !stair && slope > MaxStairSlopeDeg && nameHint;
            if (!stair && !ladder)
                return StairResult.Reject("too_steep", rise, slope);
            return new StairResult
            {
                Accepted = true, Reason = "", BottomIndex = kept[0].Index, TopIndex = kept[kept.Count - 1].Index,
                IsLadder = ladder, Rise = rise, SlopeDeg = slope,
            };
        }
    }
}
