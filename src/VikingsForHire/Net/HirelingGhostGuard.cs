using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;

namespace VikingsForHire.Net
{
    /// <summary>
    /// Server: when a hireling jumps far (through a portal with its player, a catch-up teleport, a trip home), send its
    /// new position to every player. Vanilla only sends what's near each player, so players left at the old spot never
    /// heard it had gone and kept a frozen copy there (the portal "ghost" vanilla players get too).
    /// </summary>
    internal static class HirelingGhostGuard
    {
        private const float CheckSeconds = 0.5f;
        private const float JumpMeters = 64f;

        private static readonly Dictionary<ZDOID, Vector3> Last = new();
        private static float _next;

        public static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || Time.time < _next)
                return;
            _next = Time.time + CheckSeconds;
            var seen = new HashSet<ZDOID>();
            foreach (ZDO zdo in WorldIndex.AllHirelings())
            {
                seen.Add(zdo.m_uid);
                Vector3 pos = zdo.GetPosition();
                if (Last.TryGetValue(zdo.m_uid, out Vector3 was) && Vector3.Distance(was, pos) >= JumpMeters)
                {
                    ZDOMan.instance.ForceSendZDO(zdo.m_uid);
                    VfhLog.D(LogCat.Follow, "ghost.resent", ("hid", zdo.GetString(HirelingZdo.Hid)), ("jump", Vector3.Distance(was, pos)));
                }
                Last[zdo.m_uid] = pos;
            }
            foreach (ZDOID gone in Last.Keys.Where(k => !seen.Contains(k)).ToList())
                Last.Remove(gone);
        }
    }
}
