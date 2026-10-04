using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Net
{
    /// <summary>
    /// Recruiting and releasing followers. Clients ask; the server decides (so the follower cap can't be bypassed),
    /// writes the hireling through MutationService and answers with a message. The server also keeps each follower
    /// simulated on its owner's machine (so it follows smoothly and goes through portals with them) and handles owners
    /// logging out.
    /// </summary>
    internal static class FollowerServer
    {
        public enum Kind : byte
        {
            Recruit = 1,
            Release = 2,
            ReleaseAll = 3,
        }

        private const float OwnershipSeconds = 5f;
        private static CustomRPC _rpc = null!;
        private static float _nextOwnership;

        public static void Register() => _rpc = NetworkManager.Instance.AddRPC("VFH_FollowerOp", OnServer, OnClient);

        /// <summary>Client: ask the server. <paramref name="quality"/> is the equipped stone's quality.</summary>
        public static void Send(Kind kind, string hid, int quality)
        {
            if (ZNet.instance == null || Player.m_localPlayer == null)
                return;
            var pkg = new ZPackage();
            pkg.Write((byte)kind);
            pkg.Write(hid);
            pkg.Write(quality);
            pkg.Write(Player.m_localPlayer.GetPlayerName());
            VfhLog.D(LogCat.Follow, "follow.request", ("kind", kind), ("hid", hid), ("quality", quality));
            if (ZNet.instance.IsServer())
                VfhLog.Guard(LogCat.Follow, "follow.op_failed", () => Handle(ZDOMan.GetSessionID(), pkg));
            else
                _rpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        private static IEnumerator OnServer(long sender, ZPackage pkg)
        {
            VfhLog.Guard(LogCat.Follow, "follow.op_failed", () => Handle(sender, pkg));
            yield break;
        }

        private static IEnumerator OnClient(long sender, ZPackage pkg)
        {
            string message = pkg.ReadString();
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, message);
            yield break;
        }

        private static void Handle(long sender, ZPackage pkg)
        {
            var kind = (Kind)pkg.ReadByte();
            string hid = pkg.ReadString();
            int quality = pkg.ReadInt();
            string name = pkg.ReadString();
            long pid = PlayerIdOf(sender);
            if (pid == 0L)
            {
                VfhLog.W(LogCat.Follow, "follow.unknown_player", ("sender", sender));
                return;
            }
            string reply = kind switch
            {
                Kind.Recruit => Recruit(pid, name, hid, quality),
                Kind.Release => Release(pid, hid),
                _ => ReleaseAll(pid),
            };
            Reply(sender, reply);
        }

        private static string Recruit(long pid, string playerName, string hid, int quality)
        {
            var rules = new LevelRules(DataStore.Current);
            int cap;
            try
            {
                cap = rules.StoneFollowerCap(quality);
            }
            catch (ArgumentOutOfRangeException)
            {
                return "$vfh_follow_no_stone";
            }
            ZDO? zdo = WorldIndex.Hireling(hid);
            if (zdo == null)
                return "$vfh_follow_not_found";
            var mode = (HirelingMode)zdo.GetInt(HirelingZdo.Mode, (int)HirelingMode.Idle);
            string name = zdo.GetString(HirelingZdo.Name);
            if (mode == HirelingMode.Following)
                return zdo.GetLong(HirelingZdo.Owner) == pid ? "$vfh_follow_already" : "$vfh_follow_taken";
            if (mode != HirelingMode.Working && mode != HirelingMode.Idle || zdo.GetString(HirelingZdo.BoardId).Length == 0)
                return "$vfh_follow_not_available";
            int count = FollowersOf(pid).Count;
            if (count >= cap)
            {
                VfhLog.I(LogCat.Follow, "follow.cap_reached", ("player", pid), ("count", count), ("cap", cap), ("hid", hid));
                return Localization.instance.Localize("$vfh_follow_cap", count.ToString(), cap.ToString());
            }
            MutationService.SubmitHireling(hid, new HirelingOp
            {
                Mode = HirelingMode.Following, Owner = pid, OwnerName = playerName, FollowMode = FollowMode.Follow,
                Status = "", DeliverPending = false,
            });
            _nextOwnership = 0f; // move it to the owner's machine straight away
            VfhLog.I(LogCat.Follow, "follow.recruited", ("player", pid), ("hid", hid), ("name", name), ("count", count + 1), ("cap", cap));
            return Localization.instance.Localize("$vfh_follow_recruited", name, (count + 1).ToString(), cap.ToString());
        }

        private static string Release(long pid, string hid)
        {
            ZDO? zdo = WorldIndex.Hireling(hid);
            if (zdo == null || zdo.GetLong(HirelingZdo.Owner) != pid)
                return "$vfh_follow_not_yours";
            if (!InsideHome(zdo))
                return "$vfh_follow_too_far";
            ReleaseOne(zdo, "released");
            return Localization.instance.Localize("$vfh_follow_released", zdo.GetString(HirelingZdo.Name));
        }

        private static string ReleaseAll(long pid)
        {
            int released = 0, away = 0;
            foreach (ZDO zdo in FollowersOf(pid))
            {
                if (InsideHome(zdo))
                {
                    ReleaseOne(zdo, "released_all");
                    released++;
                }
                else
                {
                    away++;
                }
            }
            return Localization.instance.Localize("$vfh_follow_released_all", released.ToString(), away.ToString());
        }

        private static void ReleaseOne(ZDO zdo, string why)
        {
            string hid = zdo.GetString(HirelingZdo.Hid);
            MutationService.SubmitHireling(hid, ReleaseOp());
            VfhLog.I(LogCat.Follow, "follow.released", ("hid", hid), ("why", why));
        }

        private static HirelingOp ReleaseOp() =>
            new() { Mode = HirelingMode.Working, Owner = 0L, OwnerName = "", FollowMode = FollowMode.Follow, DeliverPending = true };

        public static List<ZDO> FollowersOf(long pid) =>
            WorldIndex.AllHirelings().Where(z => z.GetLong(HirelingZdo.Owner) == pid &&
                                                 z.GetInt(HirelingZdo.Mode) == (int)HirelingMode.Following).ToList();

        private static bool InsideHome(ZDO zdo)
        {
            Vector3 home = zdo.GetVec3(HirelingZdo.Home, zdo.GetPosition());
            return Utils.DistanceXZ(zdo.GetPosition(), home) <= zdo.GetFloat(HirelingZdo.Radius, 20f);
        }

        private static void Reply(long to, string message)
        {
            if (to == ZDOMan.GetSessionID())
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, Localization.instance.Localize(message));
                return;
            }
            var pkg = new ZPackage();
            pkg.Write(message);
            _rpc.SendPackage(to, pkg);
        }

        /// <summary>The player id (Player.GetPlayerID) behind a network session, or 0.</summary>
        public static long PlayerIdOf(long sender)
        {
            if (sender == ZDOMan.GetSessionID() && Player.m_localPlayer != null)
                return Player.m_localPlayer.GetPlayerID();
            ZNetPeer? peer = ZNet.instance.GetPeer(sender);
            return peer == null ? 0L : PlayerIdOf(peer);
        }

        private static long PlayerIdOf(ZNetPeer peer)
        {
            ZDO? character = ZDOMan.instance.GetZDO(peer.m_characterID);
            return character?.GetLong(ZDOVars.s_playerID) ?? 0L;
        }

        /// <summary>
        /// Server, every frame: every few seconds move each follower's simulation to its owner's machine, so it follows
        /// smoothly wherever the owner goes.
        /// </summary>
        public static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Time.realtimeSinceStartup < _nextOwnership)
                return;
            _nextOwnership = Time.realtimeSinceStartup + OwnershipSeconds;
            var peerOf = new Dictionary<long, long>();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                long pid = PlayerIdOf(peer);
                if (pid != 0L)
                    peerOf[pid] = peer.m_uid;
            }
            if (Player.m_localPlayer != null)
                peerOf[Player.m_localPlayer.GetPlayerID()] = ZDOMan.GetSessionID();
            foreach (ZDO zdo in WorldIndex.AllHirelings())
            {
                if (zdo.GetInt(HirelingZdo.Mode) != (int)HirelingMode.Following)
                    continue;
                if (peerOf.TryGetValue(zdo.GetLong(HirelingZdo.Owner), out long uid) && zdo.GetOwner() != uid)
                {
                    zdo.SetOwner(uid);
                    VfhLog.D(LogCat.Follow, "follow.ownership", ("hid", zdo.GetString(HirelingZdo.Hid)), ("to", uid));
                }
            }
        }

        /// <summary>
        /// An owner leaving: followers inside their home radius go back to work; the rest stay where they are (follow
        /// mode Stay) until their owner comes back. Applied straight to the ZDOs, because the leaving peer can no longer
        /// apply anything.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        private static class DisconnectPatch
        {
            private static void Prefix(ZNetPeer peer)
            {
                try
                {
                    if (ZNet.instance == null || !ZNet.instance.IsServer() || peer == null)
                        return;
                    long pid = PlayerIdOf(peer);
                    if (pid == 0L)
                        return;
                    foreach (ZDO zdo in FollowersOf(pid))
                    {
                        zdo.SetOwner(ZDOMan.GetSessionID());
                        HirelingOp op = InsideHome(zdo)
                            ? ReleaseOp()
                            : new HirelingOp { FollowMode = FollowMode.Stay, StayPos = (zdo.GetPosition().x, zdo.GetPosition().y, zdo.GetPosition().z) };
                        HirelingOps.Apply(zdo, op);
                        VfhLog.I(LogCat.Follow, "follow.owner_left", ("player", pid), ("hid", zdo.GetString(HirelingZdo.Hid)),
                            ("result", op.Mode == HirelingMode.Working ? "back to work" : "stays"));
                    }
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("FollowerServer.Disconnect", e);
                }
            }
        }
    }
}
