using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using VikingsForHire.Compat;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// The Steward's work: every few seconds it asks each chore it may do (unlocked at its level, switched on for it,
    /// allowed on the server, not done by another mod) what needs doing, and does the best-scoring job: the most urgent,
    /// the nearer of two equal ones. A job runs until it's done or fails, then it looks again.
    /// </summary>
    internal sealed class StewardBehaviour : IHirelingBehaviour
    {
        private const float SurveySeconds = 3f;
        private const int FailuresBeforeSkip = 3;
        private const float SkipSeconds = 300f;

        private readonly List<IChore> _chores = new();
        private readonly Dictionary<int, int> _failures = new();
        private IChore? _current;
        private ChoreJob? _job;
        private float _jobStarted;
        private float _lastTick;
        private float _nextRecheck;
        private float _nextSurvey;

        public StewardBehaviour()
        {
            _chores.Add(new StationsChore(ChoreKind.Stations));
            _chores.Add(new StationsChore(ChoreKind.Mills));
        }

        public string Name => "Steward";
        public int Priority => 200;

        /// <summary>The chore it's doing right now (for the tests), or null.</summary>
        public ChoreKind? Doing => _current?.Kind;

        public bool Wants(HirelingAI ai)
        {
            Hireling h = ai.Hireling;
            if (h.Mode != HirelingMode.Working)
            {
                Stop(h, "not working");
                return false;
            }
            if (_current != null)
            {
                // Switched off, locked or taken over by another mod mid-job: drop it.
                if (Time.time >= _nextRecheck)
                {
                    _nextRecheck = Time.time + 1f;
                    if (WhyNot(_current.Kind, h) is string why)
                    {
                        Stop(h, why);
                        return false;
                    }
                }
                return true;
            }
            if (Time.time < _nextSurvey)
                return false;
            Survey(h);
            return _current != null;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_current == null || _job == null)
                return;
            // Away for a while (a fight, fleeing): whatever it was doing is stale, so look again.
            if (Time.time - _lastTick > 2f)
            {
                Stop(h, "interrupted");
                _nextSurvey = 0f;
                return;
            }
            _lastTick = Time.time;
            ChoreProgress p = _current.Tick(ai, dt);
            if (p == ChoreProgress.Running)
                return;
            int id = _job.Target != null ? _job.Target.GetInstanceID() : 0;
            if (p == ChoreProgress.Failed)
            {
                _failures[id] = (_failures.TryGetValue(id, out int n) ? n : 0) + 1;
                if (_failures[id] >= FailuresBeforeSkip && _job.Target != null)
                {
                    Reservations.Skip(_job.Target, SkipSeconds);
                    _failures.Remove(id);
                }
            }
            else
            {
                _failures.Remove(id);
            }
            VfhLog.I(LogCat.Smelter, "steward.job", ("hid", h.Hid), ("kind", _job.Kind), ("target", TargetName(_job)), ("result", p),
                ("secs", System.Math.Round(Time.time - _jobStarted, 1)));
            _nextSurvey = Time.time + System.Math.Max(_current.RestAfter, 0.25f);
            _current = null;
            _job = null;
        }

        private void Survey(Hireling h)
        {
            _nextSurvey = Time.time + SurveySeconds;
            if (h.CargoInventory == null)
                return;
            var ctx = new StewardContext(h);
            HashSet<ChoreKind> off = ChoreRules.ChoresOff(h.Zdo?.GetString(HirelingZdo.SkipItems));
            var best = (Job: (ChoreJob?)null, Chore: (IChore?)null);
            var skipped = new List<string>();
            var counts = new List<string>();
            string? missing = null;
            foreach (IChore chore in _chores)
            {
                string? why = WhyNot(chore.Kind, ctx, off);
                if (why != null)
                {
                    skipped.Add($"{ChoreKeys.Key(chore.Kind)}={why}");
                    continue;
                }
                List<ChoreJob> jobs = chore.Candidates(ctx).Where(j => j.Urgency > 0f && j.Target != null && !Reservations.IsSkipped(j.Target)).ToList();
                counts.Add($"{ChoreKeys.Key(chore.Kind)}:{jobs.Count}");
                missing ??= jobs.Count == 0 ? chore.Missing : null;
                foreach (ChoreJob j in jobs)
                    if (best.Job == null || j.Score > best.Job.Score + 0.0001f)
                        best = (j, chore);
            }
            VfhLog.D(LogCat.Smelter, "steward.survey", ("hid", h.Hid), ("level", ctx.Level), ("candidates", string.Join(",", counts)),
                ("skipped", string.Join(",", skipped)), ("winner", best.Job == null ? "none" : $"{best.Job.Kind}:{TargetName(best.Job)}"),
                ("score", best.Job?.Score ?? 0f));

            if (best.Job == null || best.Chore == null)
            {
                // Nothing to do: supplies it still carries are leftovers (delivered after a while), and the status says why.
                HashSet<string> products = StationSurvey.Products();
                bool carrying = h.CargoInventory.GetAllItems().Any(i => i.m_dropPrefab != null && !products.Contains(i.m_dropPrefab.name));
                if (carrying && h.LeftoverSince <= 0f)
                    h.LeftoverSince = Time.time;
                else if (!carrying)
                    h.LeftoverSince = 0f;
                h.SetActivity(missing ?? "$vfh_steward_idle");
                return;
            }
            _current = best.Chore;
            _job = best.Job;
            _jobStarted = Time.time;
            _lastTick = Time.time;
            h.SetActivity(best.Job.Label);
            VfhLog.I(LogCat.Smelter, "steward.job", ("hid", h.Hid), ("kind", best.Job.Kind), ("target", TargetName(best.Job)), ("result", "start"),
                ("score", System.Math.Round(best.Job.Score, 2)));
            _current.Begin(best.Job, ctx);
        }

        private static string? WhyNot(ChoreKind kind, Hireling h)
        {
            JobData job = DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out JobData? j) ? j : new JobData();
            if (!ServerAllows(kind))
                return "server";
            if (ChoreRules.ChoresOff(h.Zdo?.GetString(HirelingZdo.SkipItems)).Contains(kind))
                return "off";
            if (h.Level < ChoreRules.FirstUnlock(job, kind))
                return "locked";
            return StewardCompat.HandledBy(kind);
        }

        /// <summary>Why a chore is out for this Steward right now (null: it may do it).</summary>
        public static string? WhyNot(ChoreKind kind, StewardContext ctx, HashSet<ChoreKind> off)
        {
            if (!ServerAllows(kind))
                return "server";
            if (off.Contains(kind))
                return "off";
            if (ctx.Level < ChoreRules.FirstUnlock(ctx.Job, kind))
                return "locked";
            return StewardCompat.HandledBy(kind) is string mod ? mod : null;
        }

        public static bool ServerAllows(ChoreKind kind)
        {
            ConfigEntry<bool> setting = kind switch
            {
                ChoreKind.Fires => VfhConfig.StewardFires,
                ChoreKind.Beehives => VfhConfig.StewardBeehives,
                ChoreKind.Stations => VfhConfig.StewardStations,
                ChoreKind.Mills => VfhConfig.StewardMills,
                ChoreKind.Sap => VfhConfig.StewardSap,
                ChoreKind.Animals => VfhConfig.StewardAnimals,
                _ => VfhConfig.StewardRepairs,
            };
            return setting.Value;
        }

        // Every chore lets go of what it claimed (not just the current one: stations stay claimed between jobs).
        private void Stop(Hireling h, string why)
        {
            if (_current != null)
                VfhLog.D(LogCat.Smelter, "steward.stop", ("hid", h.Hid), ("kind", _current.Kind), ("why", why));
            foreach (IChore chore in _chores)
                chore.Abort(h);
            _current = null;
            _job = null;
        }

        private static string TargetName(ChoreJob j) => j.Target != null ? Utils.GetPrefabName(j.Target.gameObject) : "";
    }
}
