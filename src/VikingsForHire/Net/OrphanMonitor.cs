using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Followers;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Net
{
    /// <summary>
    /// Server, every 5 s: followers that have lost their owner head home (HomeReturn). The server holds every ZDO and
    /// knows where every player is, so this works for followers in unloaded areas too. Rules (OrphanRules.Check):
    /// following but more than OrphanDistance from its owner for OrphanDistanceSeconds; in Stay or Gather Here with its
    /// owner more than OrphanStayDistance away for OrphanStaySeconds; or its owner offline for 30 s. Distances are along
    /// the ground, so a dungeon (5000 m up) doesn't count as far. Followers inside their board's area never head home
    /// this way, and passengers stay aboard while their owner is online.
    /// </summary>
    internal static class OrphanMonitor
    {
        private const float TickSeconds = 5f;

        private sealed class Track
        {
            public float BeyondSince = -1f;
            public float OfflineSince = -1f;
        }

        private static readonly Dictionary<string, Track> Tracks = new();
        private static float _next;

        public static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Time.realtimeSinceStartup < _next)
                return;
            _next = Time.realtimeSinceStartup + TickSeconds;
            float now = Time.realtimeSinceStartup;

            var where = new Dictionary<long, Vector3>();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                long pid = FollowerServer.PlayerIdOf(peer);
                if (pid != 0L)
                    where[pid] = peer.m_refPos;
            }
            if (Player.m_localPlayer != null)
                where[Player.m_localPlayer.GetPlayerID()] = Player.m_localPlayer.transform.position;

            var limits = new OrphanLimits
            {
                FollowDistance = VfhConfig.OrphanDistance.Value,
                FollowSeconds = VfhConfig.Get(VfhConfig.OrphanDistanceSeconds),
                StayDistance = VfhConfig.OrphanStayDistance.Value,
                StaySeconds = VfhConfig.Get(VfhConfig.OrphanStaySeconds),
                OfflineSeconds = 30f,
            };
            var seen = new HashSet<string>();
            foreach (ZDO zdo in WorldIndex.AllHirelings().Where(z => z.GetInt(HirelingZdo.Mode) == (int)HirelingMode.Following).ToList())
            {
                string hid = zdo.GetString(HirelingZdo.Hid);
                long owner = zdo.GetLong(HirelingZdo.Owner);
                seen.Add(hid);
                if (!Tracks.TryGetValue(hid, out Track t))
                    Tracks[hid] = t = new Track();
                var mode = (FollowMode)zdo.GetInt(HirelingZdo.FollowMode);
                bool online = where.TryGetValue(owner, out Vector3 ownerPos);
                float distance = online ? Utils.DistanceXZ(zdo.GetPosition(), ownerPos) : 0f;
                bool beyond = online && distance > OrphanRules.DistanceLimit(mode, limits);
                t.BeyondSince = beyond ? (t.BeyondSince < 0f ? now : t.BeyondSince) : -1f;
                t.OfflineSince = online ? -1f : (t.OfflineSince < 0f ? now : t.OfflineSince);
                OrphanReason reason = OrphanRules.Check(mode, FollowerServer.InsideHome(zdo), zdo.GetString(HirelingZdo.Stowed).Length > 0,
                    online, t.BeyondSince < 0f ? 0f : now - t.BeyondSince, t.OfflineSince < 0f ? 0f : now - t.OfflineSince, limits);
                if (reason == OrphanReason.None)
                    continue;
                string name = zdo.GetString(HirelingZdo.Name);
                float seconds = HomeReturn.Begin(zdo, reason.ToString());
                Tracks.Remove(hid);
                VfhLog.I(LogCat.Follow, "follow.orphaned", ("hid", hid), ("owner", owner), ("reason", reason), ("distance", distance), ("seconds", seconds));
                if (online)
                    FollowerServer.Tell(owner, Localization.instance.Localize("$vfh_follow_lost", name, FollowerServer.Minutes(seconds)));
            }
            foreach (string gone in Tracks.Keys.Where(k => !seen.Contains(k)).ToList())
                Tracks.Remove(gone);
        }
    }
}
