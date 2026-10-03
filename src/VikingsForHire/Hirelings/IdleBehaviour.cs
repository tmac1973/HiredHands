using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>Mill about near home (the board). The fallback for every mode without its own behaviour yet.</summary>
    internal sealed class IdleBehaviour : IHirelingBehaviour
    {
        private const float WanderRadius = 6f;
        private const float ReturnDistance = 10f;

        public string Name => "Idle";
        public int Priority => 0;

        public bool Wants(HirelingAI ai) => true;

        public void Tick(HirelingAI ai, float dt)
        {
            Vector3 home = ai.Hireling.Home;
            if (Utils.DistanceXZ(home, ai.transform.position) > ReturnDistance)
                ai.WalkTo(dt, home, WanderRadius * 0.5f, run: false);
            else
                ai.Wander(dt, home);
        }
    }
}
