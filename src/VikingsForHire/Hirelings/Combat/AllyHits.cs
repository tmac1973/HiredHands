using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Recent hits on friendlies (players, hirelings, tamed creatures), so a Defensive guard can come to the aid of
    /// someone being attacked nearby. Records what this machine applies; kept for 5 seconds.
    /// </summary>
    internal static class AllyHits
    {
        private const float KeepSeconds = 5f;

        private readonly struct Hit
        {
            public readonly Vector3 Where;
            public readonly Character Attacker;
            public readonly float At;

            public Hit(Vector3 where, Character attacker, float at)
            {
                Where = where;
                Attacker = attacker;
                At = at;
            }
        }

        private static readonly List<Hit> Hits = new();

        /// <summary>The attacker of the most recent hit on a friendly within <paramref name="range"/> of a point.</summary>
        public static Character? RecentAttackerNear(Vector3 point, float range)
        {
            float now = Time.time;
            Hits.RemoveAll(h => now - h.At > KeepSeconds || h.Attacker == null || h.Attacker.IsDead());
            Character? best = null;
            float bestAt = -1f;
            foreach (Hit h in Hits)
            {
                if (h.At > bestAt && (h.Where - point).sqrMagnitude <= range * range)
                {
                    best = h.Attacker;
                    bestAt = h.At;
                }
            }
            return best;
        }

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class DamagePatch
        {
            private static void Postfix(Character __instance, HitData hit)
            {
                Character? attacker = hit?.GetAttacker();
                if (attacker == null || __instance == null || attacker == __instance)
                    return;
                bool friendly = __instance is Player || __instance.IsTamed() || Hireling.Of(__instance) != null;
                if (!friendly || attacker is Player || Hireling.Of(attacker) != null)
                    return;
                Hits.Add(new Hit(__instance.transform.position, attacker, Time.time));
            }
        }
    }
}
