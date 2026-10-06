using System.Collections.Generic;
using UnityEngine;
using VikingsForHire.Core.Chores;

namespace VikingsForHire.Hirelings.Work.Chores
{
    internal enum ChoreProgress
    {
        Running,
        Done,
        Failed,
    }

    /// <summary>One thing a Steward could do now: a target, how urgent it is, and the status line while doing it.</summary>
    internal sealed class ChoreJob
    {
        public ChoreKind Kind;
        public Component Target = null!;
        public float Urgency;
        public float Score;
        /// <summary>Status line while doing it (an <see cref="ActivityText"/> label).</summary>
        public string Label = "";
        /// <summary>What kind of job within the chore (e.g. load or collect), for the chore itself.</summary>
        public int Variant;
    }

    /// <summary>
    /// One of the Steward's chores. Each survey the Steward asks every chore it may do for candidates, runs the best
    /// one with <see cref="Begin"/> and <see cref="Tick"/> until it's done or fails, then surveys again.
    /// </summary>
    internal interface IChore
    {
        ChoreKind Kind { get; }

        /// <summary>Jobs that need doing now, scored. Sets <see cref="Missing"/> when something needs doing but can't be.</summary>
        IEnumerable<ChoreJob> Candidates(WorkContext ctx);

        /// <summary>Why the chore has nothing it can do although something needs doing (a label), or null.</summary>
        string? Missing { get; }

        void Begin(ChoreJob job, WorkContext ctx);

        ChoreProgress Tick(HirelingAI ai, float dt);

        /// <summary>Seconds to wait before the next survey, after the last Tick returned Done or Failed.</summary>
        float RestAfter { get; }

        /// <summary>Stop now (combat, mode change) and let go of anything claimed.</summary>
        void Abort(Hireling h);
    }
}
