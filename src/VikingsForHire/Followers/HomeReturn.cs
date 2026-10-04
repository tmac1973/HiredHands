using System;
using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// Followers heading home (phase 15). A follower that's sent home (right click in the field) or gets lost (the
    /// server's OrphanMonitor) keeps its own world object: it stops being a follower, turns Returning, is hidden on every
    /// client and parked a few metres from its board, and after a walking-pace timer (ReturnSecondsPer100m, clamped to
    /// ReturnMinSeconds…ReturnMaxSeconds) it reappears there in Working mode with its cargo, delivers it and goes back to
    /// work. Nothing is copied, so nothing can be lost or doubled. If nobody is near the board when the time comes, it
    /// appears as soon as someone is.
    /// </summary>
    internal static class HomeReturn
    {
        private static readonly System.Random Rng = new();

        /// <summary>Server: start the trip home straight on the ZDO (works in unloaded areas). Returns the trip time.</summary>
        public static float Begin(ZDO zdo, string why)
        {
            Vector3 from = zdo.GetPosition();
            Vector3 home = zdo.GetVec3(HirelingZdo.Home, from);
            float distance = Utils.DistanceXZ(from, home);
            float seconds = OrphanRules.ReturnSeconds(distance, VfhConfig.Get(VfhConfig.ReturnSecondsPer100m),
                VfhConfig.Get(VfhConfig.ReturnMinSeconds), VfhConfig.Get(VfhConfig.ReturnMaxSeconds));
            Vector3 at = home + Quaternion.Euler(0f, (float)Rng.NextDouble() * 360f, 0f) * Vector3.forward * 8f;
            // Whoever is simulating it stops (it's no longer anyone's follower); a client near the board picks it up.
            zdo.SetOwner(0L);
            zdo.Set(HirelingZdo.Mode, (int)HirelingMode.Returning);
            zdo.Set(HirelingZdo.Owner, 0L);
            zdo.Set(HirelingZdo.OwnerName, "");
            zdo.Set(HirelingZdo.FollowMode, (int)FollowMode.Follow);
            zdo.Set(HirelingZdo.Stowed, "");
            zdo.Set(HirelingZdo.DeliverPending, true);
            zdo.Set(HirelingZdo.Status, "");
            zdo.Set(HirelingZdo.ReturnAt, (long)(ZNet.instance.GetTimeSeconds() + seconds));
            zdo.SetPosition(at);
            VfhLog.I(LogCat.Follow, "follow.returning", ("hid", zdo.GetString(HirelingZdo.Hid)), ("why", why), ("from", from),
                ("distance", distance), ("seconds", seconds));
            return seconds;
        }

        /// <summary>
        /// Owner side, every AI tick of a Returning hireling: hold it out of sight until its time, then set it down on the
        /// ground there as a worker. Returns where to hold it, or null once it has arrived.
        /// </summary>
        public static Vector3? HoldOrArrive(Hireling h)
        {
            ZDO? zdo = h.Zdo;
            if (zdo == null)
                return null;
            if (ZNet.instance.GetTimeSeconds() < zdo.GetLong(HirelingZdo.ReturnAt))
                return h.transform.position;
            Vector3 p = h.transform.position;
            if (ZoneSystem.instance.GetSolidHeight(p, out float ground))
                p.y = ground + 0.1f;
            zdo.Set(HirelingZdo.Mode, (int)HirelingMode.Working);
            FollowCatchUp.Place(h.Ai, p, h.Home);
            VfhLog.I(LogCat.Follow, "follow.returned", ("hid", h.Hid), ("pos", p));
            return null;
        }

        /// <summary>Seconds until a returning hireling is back, for the Roster tab.</summary>
        public static float SecondsLeft(ZDO zdo) =>
            Math.Max(0f, (float)(zdo.GetLong(HirelingZdo.ReturnAt) - ZNet.instance.GetTimeSeconds()));
    }
}
