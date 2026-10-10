using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>
    /// Moving a board with its people. Deconstructing a board that has hirelings asks the server to pack them: each one's
    /// ZDO (job, level, looks, cargo, name…) is saved into its contract and removed from the world, and the whole roster
    /// comes back to the remover's game to go into the Hiring Charter. Placing a board with that charter puts the roster
    /// on the new board and they walk in like new hires. A hireling is always in exactly one place: the world or a charter.
    /// </summary>
    internal static class CharterPacking
    {
        /// <summary>What a charter carries: the board's level and its contracts (with their hirelings' snapshots).</summary>
        internal sealed class Packed
        {
            public int Level;
            public byte[] Roster = Array.Empty<byte>();
            public int Count;
            public string Names = "";
        }

        private static CustomRPC _rpc = null!;
        private static readonly Dictionary<int, Action<Packed?>> Callbacks = new();
        private static int _nextId = 1;

        public static void Register() => _rpc = NetworkManager.Instance.AddRPC("VFH_PackBoard", OnRequest, OnAnswer);

        /// <summary>The remover's game: pack this board's hirelings (on the server), then call back with what to carry.</summary>
        public static void Request(HiringBoard board, Action<Packed?> done)
        {
            if (ZNet.instance == null)
                return;
            if (ZNet.instance.IsServer())
            {
                done(Pack(board.Id));
                return;
            }
            int id = _nextId++;
            Callbacks[id] = done;
            var pkg = new ZPackage();
            pkg.Write(id);
            pkg.Write(board.Id);
            _rpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        /// <summary>
        /// Server: snapshot and remove every hireling of the board, and return its roster as it should come back. Contracts
        /// on their way out are dropped. Waiting ones (just hired, dead and coming back) keep their snapshot and how long
        /// they still had to wait. Guard posts are cleared: they belong to the old spot.
        /// </summary>
        public static Packed? Pack(string boardId)
        {
            ZDO? boardZdo = WorldIndex.Board(boardId);
            if (boardZdo == null)
                return null;
            Roster roster = BoardRosterOps.Read(boardZdo);
            double now = ZNet.instance.GetTimeSeconds();
            var packed = new Roster();
            var names = new List<string>();
            foreach (ContractEntry e in roster.Entries.ToList())
            {
                if (e.State == ContractState.Leaving)
                    continue;
                ZDO? zdo = e.Hid.Length > 0 ? WorldIndex.Hireling(e.Hid) : null;
                if (zdo != null)
                {
                    e.Snapshot = HirelingSnapshot.FromZdo(zdo).ToBytes();
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(zdo);
                    e.ArriveAt = 0;
                }
                else
                    e.ArriveAt = Math.Max(0, e.ArriveAt - now); // still waiting: keep how long (a respawn isn't skipped)
                e.State = ContractState.Pending;
                e.Post = null;
                e.UnpaidDays = 0;
                packed.Add(e);
                names.Add(e.Name);
            }
            var pkg = new ZPackage();
            packed.Write(new HirelingSnapshot.PackageWriter(pkg));
            VfhLog.I(LogCat.Board, "charter.packed", ("board", boardId), ("hirelings", packed.Entries.Count), ("bytes", pkg.Size()));
            return new Packed
            {
                Level = boardZdo.GetInt(BoardZdo.Level, 1),
                Roster = pkg.GetArray(),
                Count = packed.Entries.Count,
                Names = string.Join(", ", names),
            };
        }

        /// <summary>
        /// The placer's game (it owns the new board): the carried roster becomes the board's, each contract arriving after
        /// the usual arrival time (or the rest of its respawn wait, if longer).
        /// </summary>
        public static int Unpack(ZDO boardZdo, byte[] rosterBytes)
        {
            Roster carried = Roster.Read(new HirelingSnapshot.PackageReader(new ZPackage(rosterBytes)));
            Roster roster = BoardRosterOps.Read(boardZdo);
            double now = ZNet.instance.GetTimeSeconds();
            float min = VfhConfig.Get(VfhConfig.ArrivalDelayMinSeconds), max = VfhConfig.Get(VfhConfig.ArrivalDelayMaxSeconds);
            foreach (ContractEntry e in carried.Entries)
            {
                double arrival = UnityEngine.Random.Range(min, Math.Max(min, max));
                e.ArriveAt = now + Math.Max(e.ArriveAt, arrival);
                roster.Add(e);
            }
            BoardRosterOps.Write(boardZdo, roster);
            VfhLog.I(LogCat.Board, "charter.unpacked", ("hirelings", carried.Entries.Count));
            return carried.Entries.Count;
        }

        private static IEnumerator OnRequest(long sender, ZPackage pkg)
        {
            int id = pkg.ReadInt();
            string boardId = pkg.ReadString();
            Packed? packed = null;
            VfhLog.Guard(LogCat.Board, "charter.pack_failed", () => packed = Pack(boardId), ("board", boardId));
            var reply = new ZPackage();
            reply.Write(id);
            reply.Write(packed != null);
            if (packed != null)
            {
                reply.Write(packed.Level);
                reply.Write(packed.Roster);
                reply.Write(packed.Count);
                reply.Write(packed.Names);
            }
            _rpc.SendPackage(sender, reply);
            yield break;
        }

        private static IEnumerator OnAnswer(long sender, ZPackage pkg)
        {
            int id = pkg.ReadInt();
            Packed? packed = null;
            if (pkg.ReadBool())
                packed = new Packed { Level = pkg.ReadInt(), Roster = pkg.ReadByteArray(), Count = pkg.ReadInt(), Names = pkg.ReadString() };
            if (Callbacks.TryGetValue(id, out Action<Packed?> cb))
            {
                Callbacks.Remove(id);
                VfhLog.Guard(LogCat.Board, "charter.pack_callback_failed", () => cb(packed));
            }
            yield break;
        }
    }
}
