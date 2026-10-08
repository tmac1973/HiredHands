using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>Tests only: a hireling with a target here walks to it and waits there, ahead of everything but leaving.</summary>
    internal sealed class TestWalkBehaviour : IHirelingBehaviour
    {
        /// <summary>Hireling id -> where to walk. Set by the pass_test fixture, cleared with pass_clear.</summary>
        public static readonly Dictionary<string, Vector3> Targets = new();

        public string Name => "TestWalk";
        public int Priority => 990;

        public bool Wants(HirelingAI ai) => Targets.Count > 0 && Targets.ContainsKey(ai.Hireling.Hid);

        public void Tick(HirelingAI ai, float dt)
        {
            if (!Targets.TryGetValue(ai.Hireling.Hid, out Vector3 target))
                return;
            if (ai.WalkTo(dt, target, 0.5f, run: false))
                ai.Halt();
        }
    }
}
