using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Every live projectile, with how it's moving and who threw it, for hirelings to see what's flying at them. Works on
    /// every machine: velocity from how far it moved between looks (other machines see synced copies, whose m_vel isn't
    /// set), the thrower from m_owner where known, else the creature it appeared next to.
    /// </summary>
    internal static class ProjectileWatch
    {
        private const float ShooterRadius = 3f;

        internal sealed class Track
        {
            public Vector3 FirstPos;
            public Vector3 LastPos;
            public float LastTime;
            public Vector3 Velocity;
            public Character? Shooter;
            public bool Friendly;
            public bool Moved;
        }

        private static readonly Dictionary<Projectile, Track> Tracks = new();
        private static int _updatedFrame = -1;

        [HarmonyPatch(typeof(Projectile), "Awake")]
        private static class AwakePatch
        {
            private static void Postfix(Projectile __instance) => Tracks[__instance] = new Track { LastTime = -1f };
        }

        /// <summary>Refreshes every track (once a frame however many hirelings ask) and returns them.</summary>
        public static IReadOnlyDictionary<Projectile, Track> Live()
        {
            if (Time.frameCount == _updatedFrame)
                return Tracks;
            _updatedFrame = Time.frameCount;
            float now = Time.time;
            List<Projectile>? dead = null;
            foreach (KeyValuePair<Projectile, Track> kv in Tracks)
            {
                Projectile p = kv.Key;
                if (p == null)
                {
                    (dead ??= new List<Projectile>()).Add(p!);
                    continue;
                }
                Track t = kv.Value;
                Vector3 pos = p.transform.position;
                if (t.LastTime < 0f)
                {
                    t.FirstPos = pos;
                    t.Shooter = p.m_owner != null ? p.m_owner : Nearest(pos);
                    t.Friendly = t.Shooter != null && (t.Shooter is Player || t.Shooter.IsTamed() || Hireling.Of(t.Shooter) != null);
                    // Where this machine launched it the velocity is known at once (arrows close in arrive within a
                    // quarter of a second: no time for two looks).
                    t.Velocity = p.GetVelocity();
                    t.Moved = t.Velocity.sqrMagnitude > 1f;
                }
                else if (now > t.LastTime)
                {
                    Vector3 vel = p.GetVelocity();
                    t.Velocity = vel.sqrMagnitude > 0.01f ? vel : (pos - t.LastPos) / (now - t.LastTime);
                    t.Moved = t.Velocity.sqrMagnitude > 1f;
                }
                t.LastPos = pos;
                t.LastTime = now;
            }
            if (dead != null)
                foreach (Projectile p in dead)
                    Tracks.Remove(p);
            return Tracks;
        }

        private static Character? Nearest(Vector3 pos)
        {
            Character? best = null;
            float bestSq = ShooterRadius * ShooterRadius;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null)
                    continue;
                float sq = (c.GetCenterPoint() - pos).sqrMagnitude;
                if (sq < bestSq)
                {
                    best = c;
                    bestSq = sq;
                }
            }
            return best;
        }
    }
}
