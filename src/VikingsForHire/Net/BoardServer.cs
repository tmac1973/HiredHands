using System;
using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Net
{
    /// <summary>
    /// Server-side upkeep of the hireling world: answers "does this board still exist?", sends a destroyed board's
    /// hirelings away, and every 10 minutes sweeps up hirelings that left long ago or whose board is gone.
    /// </summary>
    internal static class BoardServer
    {
        private const float SweepSeconds = 600f;
        private const long LeavingTimeoutSeconds = 5400; // 3 in-game days

        private static CustomRPC _existsRpc = null!;
        private static CustomRPC _voidRpc = null!;
        private static readonly Dictionary<int, Action<bool>> ExistsCallbacks = new();
        private static int _nextId = 1;
        private static float _nextSweep = SweepSeconds;

        public static void Register()
        {
            _existsRpc = NetworkManager.Instance.AddRPC("VFH_BoardExists", OnExistsRequest, OnExistsAnswer);
            _voidRpc = NetworkManager.Instance.AddRPC("VFH_VoidBoard", OnVoidRequest, NoClient);
        }

        public static void CheckBoardExists(string boardId, Action<bool> answer)
        {
            if (ZNet.instance == null)
                return;
            if (ZNet.instance.IsServer())
            {
                answer(WorldIndex.BoardExists(boardId));
                return;
            }
            int id = _nextId++;
            ExistsCallbacks[id] = answer;
            var pkg = new ZPackage();
            pkg.Write(id);
            pkg.Write(boardId);
            _existsRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        /// <summary>The board is being destroyed: every hireling it had leaves (its roster dies with the board's ZDO).</summary>
        public static void VoidBoard(string boardId)
        {
            if (ZNet.instance == null || boardId.Length == 0)
                return;
            if (ZNet.instance.IsServer())
            {
                SendAllAway(boardId);
                return;
            }
            var pkg = new ZPackage();
            pkg.Write(boardId);
            _voidRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        public static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Time.realtimeSinceStartup < _nextSweep)
                return;
            _nextSweep = Time.realtimeSinceStartup + SweepSeconds;
            long now = (long)ZNet.instance.GetTimeSeconds();
            int removed = 0, sentAway = 0;
            foreach (ZDO zdo in new List<ZDO>(WorldIndex.AllHirelings()))
            {
                string hid = zdo.GetString(HirelingZdo.Hid);
                string boardId = zdo.GetString(HirelingZdo.BoardId);
                bool leaving = zdo.GetInt(HirelingZdo.Mode) == (int)HirelingMode.Leaving;
                if (leaving && now - zdo.GetLong(HirelingZdo.LeavingSince) > LeavingTimeoutSeconds)
                {
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(zdo);
                    removed++;
                    VfhLog.I(LogCat.Roster, "sweep.removed", ("hid", hid), ("board", boardId), ("reason", "left long ago"));
                }
                else if (!leaving && boardId.Length > 0 && !WorldIndex.BoardExists(boardId))
                {
                    BoardRosterOps.SendAway(hid, "$vfh_status_board_gone");
                    sentAway++;
                }
            }
            VfhLog.D(LogCat.Roster, "sweep", ("removed", removed), ("sentAway", sentAway));
        }

        private static void SendAllAway(string boardId)
        {
            List<ZDO> hirelings = WorldIndex.HirelingsOf(boardId);
            foreach (ZDO zdo in hirelings)
                BoardRosterOps.SendAway(zdo.GetString(HirelingZdo.Hid), "$vfh_status_board_gone");
            VfhLog.I(LogCat.Roster, "board.voided", ("board", boardId), ("hirelings", hirelings.Count));
        }

        private static IEnumerator OnExistsRequest(long sender, ZPackage pkg)
        {
            int id = pkg.ReadInt();
            string boardId = pkg.ReadString();
            var reply = new ZPackage();
            reply.Write(id);
            reply.Write(WorldIndex.BoardExists(boardId));
            _existsRpc.SendPackage(sender, reply);
            yield break;
        }

        private static IEnumerator OnExistsAnswer(long sender, ZPackage pkg)
        {
            int id = pkg.ReadInt();
            bool exists = pkg.ReadBool();
            if (ExistsCallbacks.TryGetValue(id, out Action<bool> cb))
            {
                ExistsCallbacks.Remove(id);
                VfhLog.Guard(LogCat.Net, "board_exists_callback_failed", () => cb(exists));
            }
            yield break;
        }

        private static IEnumerator OnVoidRequest(long sender, ZPackage pkg)
        {
            string boardId = pkg.ReadString();
            VfhLog.Guard(LogCat.Roster, "board.void_failed", () => SendAllAway(boardId), ("board", boardId));
            yield break;
        }

        private static IEnumerator NoClient(long sender, ZPackage pkg)
        {
            yield break;
        }
    }
}
