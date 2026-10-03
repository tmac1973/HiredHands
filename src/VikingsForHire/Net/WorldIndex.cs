using System.Collections.Generic;
using VikingsForHire.Board;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Net
{
    /// <summary>
    /// Server-side lookups by our ids: board id → board ZDO, hireling id → hireling ZDO, board id → its hirelings.
    /// Only the server holds every ZDO, so it can find objects in unloaded zones. Rebuilt from the ZDO table on demand
    /// (at most every 2 s, or immediately on a miss after that).
    /// </summary>
    internal static class WorldIndex
    {
        private const float RefreshSeconds = 2f;

        private static readonly Dictionary<string, ZDOID> Boards = new();
        private static readonly Dictionary<string, ZDOID> Hirelings = new();
        private static readonly Dictionary<string, List<ZDOID>> ByBoard = new();
        private static float _builtAt = -999f;

        public static ZDO? Board(string boardId) => Find(Boards, boardId);

        public static ZDO? Hireling(string hid) => Find(Hirelings, hid);

        public static List<ZDO> HirelingsOf(string boardId)
        {
            Refresh(false);
            var list = new List<ZDO>();
            if (ByBoard.TryGetValue(boardId, out List<ZDOID> ids))
            {
                foreach (ZDOID id in ids)
                {
                    ZDO? zdo = ZDOMan.instance.GetZDO(id);
                    if (zdo != null)
                        list.Add(zdo);
                }
            }
            return list;
        }

        public static IEnumerable<ZDO> AllHirelings()
        {
            Refresh(false);
            foreach (ZDOID id in Hirelings.Values)
            {
                ZDO? zdo = ZDOMan.instance.GetZDO(id);
                if (zdo != null)
                    yield return zdo;
            }
        }

        public static bool BoardExists(string boardId) => boardId.Length > 0 && Board(boardId) != null;

        public static int BoardCount
        {
            get
            {
                Refresh(false);
                return Boards.Count;
            }
        }

        private static ZDO? Find(Dictionary<string, ZDOID> map, string id)
        {
            if (id.Length == 0 || ZDOMan.instance == null)
                return null;
            Refresh(false);
            if (map.TryGetValue(id, out ZDOID zid) && ZDOMan.instance.GetZDO(zid) is ZDO zdo)
                return zdo;
            Refresh(true);
            return map.TryGetValue(id, out zid) ? ZDOMan.instance.GetZDO(zid) : null;
        }

        private static void Refresh(bool missed)
        {
            if (ZDOMan.instance == null)
                return;
            float age = Time.realtimeSinceStartup - _builtAt;
            if (age < RefreshSeconds && !(missed && age > 0.25f))
                return;
            _builtAt = Time.realtimeSinceStartup;
            Boards.Clear();
            Hirelings.Clear();
            ByBoard.Clear();
            foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
            {
                int prefab = zdo.GetPrefab();
                if (prefab == BoardZdo.PrefabHash)
                {
                    string id = zdo.GetString(BoardZdo.Id);
                    if (id.Length > 0)
                        Boards[id] = zdo.m_uid;
                }
                else if (prefab == HirelingZdo.PrefabHash)
                {
                    string hid = zdo.GetString(HirelingZdo.Hid);
                    if (hid.Length > 0)
                        Hirelings[hid] = zdo.m_uid;
                    string board = zdo.GetString(HirelingZdo.BoardId);
                    if (board.Length > 0)
                    {
                        if (!ByBoard.TryGetValue(board, out List<ZDOID> list))
                            ByBoard[board] = list = new List<ZDOID>();
                        list.Add(zdo.m_uid);
                    }
                }
            }
        }
    }
}
