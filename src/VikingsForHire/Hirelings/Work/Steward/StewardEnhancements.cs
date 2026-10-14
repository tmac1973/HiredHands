using System.Collections.Generic;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using UnityEngine;
using static VikingsForHire.Core.Enhancements.EnhancementId;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// The Steward's enhancements (plan/enhancements phase 09), as the numbers its chores use instead of the server's
    /// settings. Without the pick each one returns the setting, so other Stewards are unchanged.
    /// </summary>
    internal static class StewardEnhancements
    {
        /// <summary>Keeper of flames: tops fires (and shield generators) up from this share of full.</summary>
        public static float RefillFraction(Hireling h) =>
            h.Enh.Has(KeeperOfFlames) ? h.Enh.Value(KeeperOfFlames, "refill") : VfhConfig.StewardFireRefillFraction.Value;

        /// <summary>Handyman: how far it repairs from.</summary>
        public static float RepairReach(Hireling h, float normal) => h.Enh.Has(Handyman) ? Mathf.Max(normal, h.Enh.Value(Handyman, "range")) : normal;

        /// <summary>Handyman: seconds with no enemies about before it repairs.</summary>
        public static float RepairQuiet(Hireling h) =>
            h.Enh.Has(Handyman) ? Mathf.Min(VfhConfig.StewardRepairQuietSeconds.Value, h.Enh.Value(Handyman, "quiet")) : VfhConfig.StewardRepairQuietSeconds.Value;

        /// <summary>Quartermaster: days of upkeep it stocks the board to.</summary>
        public static int FillDays(Hireling h) => VfhConfig.StewardBoardFillDays.Value + Mathf.RoundToInt(h.Enh.Value(Quartermaster, "days"));

        /// <summary>Tidy mind: seconds an item lies before it's tidied.</summary>
        public static float TidyMinAge(Hireling h) =>
            h.Enh.Has(TidyMind) ? Mathf.Min(VfhConfig.StewardTidyMinSeconds.Value, h.Enh.Value(TidyMind, "seconds")) : VfhConfig.StewardTidyMinSeconds.Value;

        /// <summary>Tidy mind: how far round a spot it sweeps up.</summary>
        public static float TidyReach(Hireling h, float normal) => h.Enh.Has(TidyMind) ? normal * h.Enh.Value(TidyMind, "rangeMult") : normal;

        /// <summary>Smelter's hand: tops stations up whenever they aren't full (from the threshold otherwise).</summary>
        public static float StationThreshold(Hireling h) => h.Enh.Has(SmeltersHand) ? 0.999f : VfhConfig.SmelterRefillThreshold.Value;

        /// <summary>Seconds per item put in or taken out: Smelter's hand (stations) and Master steward make it quicker.</summary>
        public static float ItemSeconds(Hireling h, float normal, bool station = false)
        {
            float s = normal * (1f - h.Enh.Value(MasterSteward, "speed"));
            if (station && h.Enh.Has(SmeltersHand))
                s /= Mathf.Max(1f, h.Enh.Value(SmeltersHand, "mult"));
            return s;
        }

        /// <summary>Master steward: the pause after a chore.</summary>
        public static float Rest(Hireling h, float rest) => rest * (1f - h.Enh.Value(MasterSteward, "speed"));

        /// <summary>Fermenter's touch: the share of a fermenter's time it takes off when it loads one.</summary>
        public static float FermentShare(Hireling h) => h.Enh.Value(FermentersTouch, "share");

        /// <summary>Shield keeper: how far from home it looks after shield generators (a multiple of its radius).</summary>
        public static float ShieldReach(Hireling h, float radius) => h.Enh.Has(ShieldKeeper) ? radius * h.Enh.Value(ShieldKeeper, "rangeMult") : radius;

        /// <summary>Watchman: a Steward with it hits harder (DamagePatches' target multipliers).</summary>
        public static float WatchmanMult(Hireling h, Character? target) =>
            h.Job == JobType.Smelter && h.Enh.Has(Watchman) ? 1f + h.Enh.Value(Watchman, "damage") : 1f;

        /// <summary>Errand runner: how many chores in a row it may go straight on to, and how near they must be.</summary>
        public static (int Chain, float Range) Errands(Hireling h) =>
            h.Enh.Has(ErrandRunner) ? (Mathf.RoundToInt(h.Enh.Value(ErrandRunner, "chain")), h.Enh.Value(ErrandRunner, "range")) : (0, 0f);
    }

    /// <summary>
    /// Bonesetter: hurt hirelings near a Steward with it, not in a fight, heal a share of their max health a second.
    /// Run on every machine for the hirelings it owns (the healing goes through their own health), once a second.
    /// </summary>
    internal static class Bonesetter
    {
        private static float _next;
        private static readonly Dictionary<string, float> LastFx = new();

        public static void Tick()
        {
            if (Time.time < _next)
                return;
            _next = Time.time + 1f;
            List<Hireling>? setters = null;
            foreach (Hireling h in Hireling.Loaded)
                if (h != null && h.Job == JobType.Smelter && h.Enh.Has(EnhancementIdBonesetter) && !h.Humanoid.IsDead())
                    (setters ??= new List<Hireling>()).Add(h);
            if (setters == null)
                return;
            foreach (Hireling h in Hireling.Loaded)
            {
                if (h == null || h.Zdo == null || h.Humanoid.IsDead() || !h.Humanoid.m_nview.IsOwner() || h.Ai == null || h.Ai.CombatTarget != null)
                    continue;
                Humanoid hum = h.Humanoid;
                if (hum.GetHealth() >= hum.GetMaxHealth() - 0.01f)
                    continue;
                foreach (Hireling s in setters)
                {
                    float range = s.Enh.Value(EnhancementIdBonesetter, "range");
                    if (Vector3.Distance(s.transform.position, h.transform.position) > range)
                        continue;
                    float heal = hum.GetMaxHealth() * s.Enh.Value(EnhancementIdBonesetter, "rate");
                    bool show = !LastFx.TryGetValue(h.Hid, out float at) || Time.time - at > 4f;
                    if (show)
                        LastFx[h.Hid] = Time.time;
                    hum.Heal(heal, show);
                    EnhancementEvents.Fire(s, "Bonesetter", LogCat.Smelter, ("patient", h.Hid), ("heal", heal));
                    break;
                }
            }
        }

        private const Core.Enhancements.EnhancementId EnhancementIdBonesetter = Core.Enhancements.EnhancementId.Bonesetter;
    }
}
