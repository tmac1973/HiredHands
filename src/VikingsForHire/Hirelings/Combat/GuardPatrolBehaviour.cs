using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>Guards with nothing to fight walk the work radius: a new spot every 20–40 s, pausing 5–10 s at each.</summary>
    internal sealed class GuardPatrolBehaviour : IHirelingBehaviour
    {
        private Vector3 _point;
        private float _nextPick;
        private float _pauseUntil;

        public string Name => "Patrol";
        public int Priority => 100;

        public bool Wants(HirelingAI ai) => ai.Hireling.Job.IsGuard() && ai.Hireling.Mode == HirelingMode.Working && !ai.Hireling.HasPost;

        public void Tick(HirelingAI ai, float dt)
        {
            if (Time.time >= _nextPick || _point == Vector3.zero)
                Pick(ai);
            if (Time.time < _pauseUntil)
            {
                ai.Halt();
                return;
            }
            if (ai.WalkTo(dt, _point, 1.5f, run: false))
                _pauseUntil = Time.time + Random.Range(5f, 10f);
        }

        // A spot on dry ground or a floor that it can walk to. Any spot in the radius would do before: Thorvald patrolled to
        // spots in the water below the base and ended up swimming against the bank.
        private void Pick(HirelingAI ai)
        {
            Vector3 home = ai.Hireling.Home;
            float r = ai.Hireling.Radius * 0.8f;
            float water = ZoneSystem.instance.m_waterLevel;
            Vector3 p = home;
            for (int tries = 0; tries < 12; tries++)
            {
                Vector2 offset = Random.insideUnitCircle * r;
                Vector3 c = home + new Vector3(offset.x, 0f, offset.y);
                if (!GroundRules.Below(c, home.y, out float h) || h < water + 0.3f)
                    continue;
                c.y = h;
                if (!ai.CanReach(c))
                    continue;
                p = c;
                break;
            }
            _point = p;
            _nextPick = Time.time + Random.Range(20f, 40f);
            VfhLog.T(LogCat.AI, "patrol.point", ("hid", ai.Hireling.Hid), ("point", p));
        }
    }
}
