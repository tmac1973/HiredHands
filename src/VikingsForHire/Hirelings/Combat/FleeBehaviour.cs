using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Run from danger toward home: workers on Flee stance when a threat comes close, and anyone badly hurt (except
    /// aggressive guards) until they've recovered. Calms down after 5 s with nothing within 20 m.
    /// </summary>
    internal sealed class FleeBehaviour : IHirelingBehaviour
    {
        private const float SafeDistance = 20f;
        private const float CalmSeconds = 5f;

        private bool _fleeing;
        private float _lastThreat;

        public string Name => "Flee";
        public int Priority => 950;

        public bool Wants(HirelingAI ai)
        {
            ThreatScanner scan = ai.Threats;
            bool stanceFlee = ai.Stance == Stance.Flee &&
                              StanceRules.Decide(Stance.Flee, scan.Nearest != null ? scan.NearestDistance : null, false, false, scan.RecentlyAttacked, false) == CombatAction.Flee;
            bool danger = stanceFlee || (ai.Retreating && scan.Nearest != null && scan.NearestDistance < SafeDistance) || scan.RecentlyAttacked && ai.Retreating;
            if (danger)
                _lastThreat = Time.time;

            bool want = danger || (_fleeing && Time.time - _lastThreat < CalmSeconds) || (ai.Retreating && Vector3.Distance(ai.transform.position, ai.Hireling.Home) > 4f);
            if (want != _fleeing)
            {
                _fleeing = want;
                VfhLog.D(LogCat.Combat, want ? "flee.start" : "flee.calm", ("hid", ai.Hireling.Hid), ("retreating", ai.Retreating),
                    ("threat", scan.Nearest != null ? scan.Nearest.m_name : "none"));
            }
            return want;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Vector3 me = ai.transform.position;
            Vector3 home = ai.Hireling.Home;
            Character? threat = ai.Threats.RecentlyAttacked ? ai.Threats.LastAttacker : ai.Threats.Nearest;
            if (threat == null)
            {
                ai.WalkTo(dt, home, 3f, run: true);
                return;
            }
            Vector3 away = Vector3.ProjectOnPlane(me - threat.transform.position, Vector3.up).normalized;
            Vector3 toHome = Vector3.ProjectOnPlane(home - me, Vector3.up).normalized;
            Vector3 dir = (away + toHome * 0.5f).normalized;
            ai.WalkTo(dt, me + dir * 15f, 1f, run: true);
        }
    }
}
