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

        /// <summary>The trip time from where it is to its board.</summary>
        public static float TripSeconds(ZDO zdo)
        {
            Vector3 from = zdo.GetPosition();
            float distance = Utils.DistanceXZ(from, zdo.GetVec3(HirelingZdo.Home, from));
            return OrphanRules.ReturnSeconds(distance, VfhConfig.Get(VfhConfig.ReturnSecondsPer100m),
                VfhConfig.Get(VfhConfig.ReturnMinSeconds), VfhConfig.Get(VfhConfig.ReturnMaxSeconds));
        }

        /// <summary>
        /// Server: send it home. The machine simulating it must make the change (its own updates would overwrite the
        /// server's otherwise), so if a connected player's game owns it the order goes there; with nobody simulating it
        /// (owner offline, area unloaded) the server changes the ZDO itself. Returns the trip time.
        /// </summary>
        public static float Order(ZDO zdo, string why)
        {
            long owner = zdo.GetOwner();
            bool remote = owner != 0L && owner != ZDOMan.GetSessionID() && ZNet.instance.GetPeer(owner) != null;
            if (remote)
            {
                FollowerServer.SendHomeOrder(owner, zdo.GetString(HirelingZdo.Hid), why);
                return TripSeconds(zdo);
            }
            return Begin(zdo, why);
        }

        /// <summary>Whether a follower must leave this behind when heading home (ReturnHomeWithNonTeleportable off).</summary>
        public static bool LeavesBehind(ItemDrop.ItemData item) =>
            !VfhConfig.ReturnHomeWithNonTeleportable.Value && !item.m_shared.m_teleportable &&
            !(ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll));

        // Drop what it may not take home, where it stands, from its cargo: the loaded hireling's own, or (nobody
        // simulating it) a copy read from and written back to its ZDO.
        private static int DropContraband(ZDO zdo, Vector3 at, string hid)
        {
            if (VfhConfig.ReturnHomeWithNonTeleportable.Value)
                return 0;
            Hireling? h = Hireling.Loaded.FirstOrDefault(x => x != null && x.Zdo == zdo);
            if (h?.CargoInventory != null)
                return Hirelings.Work.DropPile.DropAll(h.CargoInventory, at, "heading home", hid, LeavesBehind);
            byte[]? data = zdo.GetByteArray(ZDOVars.s_items);
            if (data == null || data.Length == 0)
                return 0;
            var inv = new Inventory("cargo", null, HirelingPrefab.CargoWidth, HirelingPrefab.CargoHeight);
            inv.Load(new ZPackage(data));
            int n = Hirelings.Work.DropPile.DropAll(inv, at, "heading home", hid, LeavesBehind);
            if (n > 0)
            {
                var pkg = new ZPackage();
                inv.Save(pkg);
                zdo.Set(ZDOVars.s_items, pkg.GetArray());
            }
            return n;
        }

        /// <summary>Start the trip home on the ZDO (on the machine that owns it, or the server when nobody does).</summary>
        public static float Begin(ZDO zdo, string why)
        {
            Vector3 from = zdo.GetPosition();
            int dropped = DropContraband(zdo, from + Vector3.up * 0.5f, zdo.GetString(HirelingZdo.Hid));
            Vector3 home = zdo.GetVec3(HirelingZdo.Home, from);
            float distance = Utils.DistanceXZ(from, home);
            float seconds = OrphanRules.ReturnSeconds(distance, VfhConfig.Get(VfhConfig.ReturnSecondsPer100m),
                VfhConfig.Get(VfhConfig.ReturnMinSeconds), VfhConfig.Get(VfhConfig.ReturnMaxSeconds));
            Vector3 at = home + Quaternion.Euler(0f, (float)Rng.NextDouble() * 360f, 0f) * Vector3.forward * 8f;
            zdo.Set(HirelingZdo.Mode, (int)HirelingMode.Returning);
            zdo.Set(HirelingZdo.Owner, 0L);
            zdo.Set(HirelingZdo.OwnerName, "");
            zdo.Set(HirelingZdo.FollowMode, (int)FollowMode.Follow);
            zdo.Set(HirelingZdo.Stowed, "");
            zdo.Set(HirelingZdo.DeliverPending, true);
            zdo.Set(HirelingZdo.Status, "");
            zdo.Set(HirelingZdo.ReturnAt, (long)(ZNet.instance.GetTimeSeconds() + seconds));
            zdo.SetPosition(at);
            if (ZNetScene.instance?.FindInstance(zdo) is ZNetView view)
            {
                view.transform.position = at;
                if (view.GetComponent<Rigidbody>() is Rigidbody body)
                    body.position = at;
            }
            // Last: whoever simulated it lets go (it's nobody's follower now); a client near the board picks it up.
            zdo.SetOwner(0L);
            VfhLog.I(LogCat.Follow, "follow.returning", ("hid", zdo.GetString(HirelingZdo.Hid)), ("why", why), ("from", from),
                ("distance", distance), ("seconds", seconds), ("leftBehind", dropped));
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
