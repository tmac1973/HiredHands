using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Who's working on what (on this machine), so two hirelings don't pick the same tree, and what turned out to be
    /// unreachable or invalid (skipped for a while).
    /// </summary>
    internal static class Reservations
    {
        private const float ReserveSeconds = 60f;
        private static readonly Dictionary<int, (string Hid, float Until)> Held = new();
        private static readonly Dictionary<int, float> Blacklist = new();

        public static bool TryReserve(Component target, string hid)
        {
            int id = target.GetInstanceID();
            if (Held.TryGetValue(id, out var r) && r.Hid != hid && Time.time < r.Until)
                return false;
            Held[id] = (hid, Time.time + ReserveSeconds);
            return true;
        }

        public static void Release(Component? target, string hid)
        {
            if (target == null)
                return;
            int id = target.GetInstanceID();
            if (Held.TryGetValue(id, out var r) && r.Hid == hid)
                Held.Remove(id);
        }

        public static bool IsReservedByOther(Component target, string hid) =>
            Held.TryGetValue(target.GetInstanceID(), out var r) && r.Hid != hid && Time.time < r.Until;

        public static void Skip(Component target, float seconds) => Blacklist[target.GetInstanceID()] = Time.time + seconds;

        public static bool IsSkipped(Component target) =>
            Blacklist.TryGetValue(target.GetInstanceID(), out float until) && Time.time < until;

        /// <summary>For the test check: no target is held by two hirelings.</summary>
        public static bool AllUnique()
        {
            var seen = new HashSet<int>();
            foreach (var kv in Held)
                if (Time.time < kv.Value.Until && !seen.Add(kv.Key))
                    return false;
            return true;
        }
    }
}
