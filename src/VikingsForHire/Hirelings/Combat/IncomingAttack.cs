using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// An attack a hireling has noticed coming at it, before it lands: when, how hard (after its armor), whether it's an
    /// area attack, and whether the hireling read it in time (its level's read chance). The block and dodge controllers
    /// plan against these.
    /// </summary>
    internal sealed class IncomingAttack
    {
        private static int _nextId;

        public readonly int AttackId = ++_nextId;
        public Character Attacker = null!;
        public Projectile? Projectile { get; set; }

        /// <summary>When it lands (Time.time).</summary>
        public float HitTime;

        /// <summary>Estimated damage after the hireling's armor.</summary>
        public float Damage;

        public bool Area;

        /// <summary>The read roll: seen coming in time to react.</summary>
        public bool Read;

        /// <summary>The hit time came from the attack animation's hit event (false: a guess, never parried or dodged on purpose).</summary>
        public bool Timed;

        /// <summary>Flat unit vector from the hireling towards where the hit comes from.</summary>
        public Vector3 From;

        /// <summary>Set by the dodge controller once it has decided about this attack, and whether it rolls.</summary>
        public bool DodgeDecided { get; set; }
        public bool Dodging { get; set; }

        public string Weapon = "";
    }
}
