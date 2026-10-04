using System.Collections.Generic;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>Brings a contracted viking in from the edge of the base: a dry, open, reachable spot about 35 m out.</summary>
    internal static class ArrivalSpawner
    {
        private const int Directions = 12;
        private static readonly List<Piece> Pieces = new();
        private static readonly List<Vector3> Path = new();

        /// <summary>Spawns the contract's hireling with its current job/level/stance/radius; returns its hid.</summary>
        public static string Spawn(HiringBoard board, ContractEntry entry)
        {
            HirelingSnapshot snap = HirelingSnapshot.FromBytes(entry.Snapshot);
            snap.Set(HirelingZdo.BoardId, board.Id);
            snap.Set(HirelingZdo.Job, (int)entry.Job);
            snap.Set(HirelingZdo.Level, entry.Level);
            snap.Set(HirelingZdo.Stance, (int)entry.Stance);
            snap.Set(HirelingZdo.Radius, entry.Radius);
            snap.Set(HirelingZdo.Home, board.transform.position);
            snap.Set(HirelingZdo.Mode, (int)HirelingMode.Working);
            snap.Set(HirelingZdo.Status, "");
            snap.Set(HirelingZdo.Owner, 0L); // a hireling always arrives (or comes back) as nobody's follower
            snap.Set(HirelingZdo.OwnerName, "");
            snap.Set(HirelingZdo.FollowMode, (int)FollowMode.Follow);
            // A posted guard comes back to its post.
            snap.Set(HirelingZdo.Posted, entry.Post != null);
            if (entry.Post != null)
            {
                snap.Set(HirelingZdo.Post, new Vector3(entry.Post.X, entry.Post.Y, entry.Post.Z));
                snap.Set(HirelingZdo.PostYaw, entry.Post.Yaw);
            }

            Vector3 point = FindPoint(board, out string how);
            Quaternion facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(board.transform.position - point, Vector3.up).normalized + Vector3.forward * 0.001f);
            ZDO zdo = snap.Spawn(point, facing);
            string hid = snap.Record.Hid;
            VfhLog.I(LogCat.Roster, "contract.arrived", ("board", board.Id), ("contract", entry.ContractId), ("hid", hid), ("name", entry.Name),
                ("job", entry.Job), ("level", entry.Level), ("pos", point), ("spot", how), ("respawn", entry.RespawnPending), ("zdo", zdo.m_uid.ToString()));
            return hid;
        }

        private static Vector3 FindPoint(HiringBoard board, out string how)
        {
            Vector3 center = board.transform.position;
            float distance = VfhConfig.ArrivalSpawnDistance.Value;
            float water = ZoneSystem.instance.m_waterLevel;
            Vector3? dryFallback = null;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < Directions; i++)
            {
                Vector3 p = center + Quaternion.Euler(0f, start + i * 360f / Directions, 0f) * Vector3.forward * distance;
                if (!ZoneSystem.instance.GetSolidHeight(p, out float h) || h <= water + 0.5f)
                    continue;
                p.y = h;
                Pieces.Clear();
                Piece.GetAllPiecesInRadius(p, 2f, Pieces);
                if (Pieces.Count > 0)
                    continue;
                dryFallback ??= p;
                Path.Clear();
                if (Pathfinding.instance != null && Pathfinding.instance.GetPath(p, center, Path, Pathfinding.AgentType.Humanoid, requireFullPath: true))
                {
                    how = "edge";
                    return p;
                }
            }
            if (dryFallback is Vector3 dry)
            {
                how = "edge_nopath";
                return dry;
            }
            how = "board";
            return center + board.transform.forward * 2f;
        }
    }
}
