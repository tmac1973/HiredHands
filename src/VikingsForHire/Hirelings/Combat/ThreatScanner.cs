using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// What a hireling knows about danger: the nearest visible enemy (refreshed every scan interval, staggered so
    /// hirelings don't all scan on the same frame) and who last hit it.
    /// </summary>
    internal sealed class ThreatScanner
    {
        private const float MaxScanRange = 60f;
        private const float AttackedMemorySeconds = 6f;

        private readonly HirelingAI _ai;
        private float _nextScan;

        public Character? Nearest { get; private set; }
        public float NearestDistance { get; private set; } = float.MaxValue;
        public Character? LastAttacker { get; private set; }
        public float LastAttackedAt { get; private set; } = -999f;

        public ThreatScanner(HirelingAI ai)
        {
            _ai = ai;
            _nextScan = Time.time + (Mathf.Abs(ai.GetInstanceID()) % 100) / 100f;
        }

        public bool RecentlyAttacked => Time.time - LastAttackedAt < AttackedMemorySeconds && Alive(LastAttacker);

        public void OnDamaged(Character? attacker)
        {
            if (attacker == null || attacker is Player || Hireling.Of(attacker) != null)
                return;
            LastAttacker = attacker;
            LastAttackedAt = Time.time;
            _nextScan = 0f; // react now
        }

        public void Tick(float scanInterval)
        {
            if (Time.time < _nextScan)
                return;
            _nextScan = Time.time + scanInterval;
            Vector3 me = _ai.transform.position;
            Character? best = null;
            float bestSq = MaxScanRange * MaxScanRange;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == _ai.Hireling.Humanoid || c.IsDead() || c is Player || c.IsTamed() || Hireling.Of(c) != null)
                    continue;
                if (!BaseAI.IsEnemy(_ai.Hireling.Humanoid, c))
                    continue;
                float sq = (c.transform.position - me).sqrMagnitude;
                if (sq < bestSq && _ai.CanSeeTarget(c))
                {
                    best = c;
                    bestSq = sq;
                }
            }
            Nearest = best;
            NearestDistance = best != null ? Mathf.Sqrt(bestSq) : float.MaxValue;
        }

        public static bool Alive(Character? c) => c != null && !c.IsDead();
    }
}
