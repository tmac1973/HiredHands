using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>
    /// World-wide board lookups (nearest board, board count), including boards in unloaded zones. Only the server has
    /// every ZDO, so it answers from its ZDO table (cached briefly); clients ask it over VFH_BoardCheck.
    /// </summary>
    internal static class BoardRegistry
    {
        private const float ServerCacheSeconds = 2f;
        private const float ClientCacheSeconds = 2f;
        private const float CellSize = 2f;

        public readonly struct Answer
        {
            public readonly float? NearestDistance;
            public readonly int Count;

            public Answer(float? nearest, int count)
            {
                NearestDistance = nearest;
                Count = count;
            }
        }

        private static CustomRPC _rpc = null!;
        private static readonly List<Vector3> ServerBoards = new();
        private static float _serverScanAt = -999f;

        private static readonly Dictionary<Vector2Int, (Answer Answer, float At)> ClientCache = new();
        private static readonly Dictionary<int, (Vector2Int Cell, float SentAt)> Pending = new();
        private const float ResendSeconds = 3f;
        private static int _nextRequest = 1;

        /// <summary>Drops the client's cached answers (tests do this after removing boards).</summary>
        public static void ForgetCache() => ClientCache.Clear();

        public static void Register() => _rpc = NetworkManager.Instance.AddRPC("VFH_BoardCheck", OnServerRequest, OnClientAnswer);

        /// <summary>
        /// Nearest board and world count for a position. Returns false while a client waits for the server's first answer.
        /// </summary>
        public static bool TryQuery(Vector3 position, out Answer answer)
        {
            answer = default;
            if (ZNet.instance == null)
                return false;
            if (ZNet.instance.IsServer())
            {
                answer = ComputeServer(position);
                return true;
            }

            var cell = new Vector2Int(Mathf.RoundToInt(position.x / CellSize), Mathf.RoundToInt(position.z / CellSize));
            if (ClientCache.TryGetValue(cell, out var cached) && Time.realtimeSinceStartup - cached.At < ClientCacheSeconds)
            {
                answer = cached.Answer;
                return true;
            }
            // Ask again if an earlier request for this spot got no answer (sent while connecting, or lost).
            foreach (int stale in Pending.Where(p => Time.realtimeSinceStartup - p.Value.SentAt > ResendSeconds).Select(p => p.Key).ToList())
                Pending.Remove(stale);
            if (!Pending.Values.Any(p => p.Cell == cell))
            {
                int id = _nextRequest++;
                Pending[id] = (cell, Time.realtimeSinceStartup);
                var pkg = new ZPackage();
                pkg.Write(id);
                pkg.Write(position);
                _rpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
                VfhLog.T(LogCat.Placement, "registry.request", ("id", id), ("pos", position));
            }
            // An older answer for this spot is still better than nothing while the new one is on its way.
            if (cached.At > 0f)
            {
                answer = cached.Answer;
                return true;
            }
            return false;
        }

        /// <summary>Server only: every board ZDO in the world.</summary>
        public static Answer ComputeServer(Vector3 position)
        {
            RefreshServer();
            float? nearest = null;
            foreach (Vector3 p in ServerBoards)
            {
                float d = Vector3.Distance(p, position);
                if (nearest == null || d < nearest)
                    nearest = d;
            }
            return new Answer(nearest, ServerBoards.Count);
        }

        public static int ServerCount()
        {
            RefreshServer();
            return ServerBoards.Count;
        }

        private static void RefreshServer()
        {
            if (Time.realtimeSinceStartup - _serverScanAt < ServerCacheSeconds || ZDOMan.instance == null)
                return;
            _serverScanAt = Time.realtimeSinceStartup;
            ServerBoards.Clear();
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                if (zdo.GetPrefab() == BoardZdo.PrefabHash)
                    ServerBoards.Add(zdo.GetPosition());
            }
            VfhLog.T(LogCat.Placement, "registry.scan", ("boards", ServerBoards.Count), ("zdos", ZDOMan.instance.m_objectsByID.Count));
        }

        private static IEnumerator OnServerRequest(long sender, ZPackage package)
        {
            int id = package.ReadInt();
            Vector3 pos = package.ReadVector3();
            Answer a = ComputeServer(pos);
            var reply = new ZPackage();
            reply.Write(id);
            reply.Write(a.NearestDistance ?? -1f);
            reply.Write(a.Count);
            _rpc.SendPackage(sender, reply);
            VfhLog.T(LogCat.Placement, "registry.answer", ("to", sender), ("id", id), ("nearest", a.NearestDistance), ("count", a.Count));
            yield break;
        }

        private static IEnumerator OnClientAnswer(long sender, ZPackage package)
        {
            int id = package.ReadInt();
            float nearest = package.ReadSingle();
            int count = package.ReadInt();
            if (Pending.TryGetValue(id, out var pending))
            {
                Vector2Int cell = pending.Cell;
                Pending.Remove(id);
                ClientCache[cell] = (new Answer(nearest < 0f ? null : nearest, count), Time.realtimeSinceStartup);
            }
            yield break;
        }
    }
}
