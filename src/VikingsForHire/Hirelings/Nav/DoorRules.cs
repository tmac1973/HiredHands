using System.Linq;
using UnityEngine;
using VikingsForHire.Board;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>Which doors a hireling may use, shared by the door helper and link routes.</summary>
    internal static class DoorRules
    {
        public static bool IsOpen(Door d) => d.m_nview != null && d.m_nview.IsValid() && d.m_nview.GetZDO().GetInt(ZDOVars.s_state) != 0;

        // Not locked, can be closed again, and the board's owner has access under any ward covering it.
        public static bool Usable(Door? d, long boardOwner)
        {
            if (d == null || d.m_nview == null || !d.m_nview.IsValid() || d.m_keyItem != null || d.m_canNotBeClosed)
                return false;
            if (!d.m_checkGuardStone)
                return true;
            if (boardOwner == 0L)
                return PrivateArea.CheckAccess(d.transform.position, 0f, flash: false);
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || !area.IsEnabled() || !area.IsInside(d.transform.position, 0f))
                    continue;
                if (area.m_piece.GetCreator() != boardOwner && !area.IsPermitted(boardOwner))
                    return false;
            }
            return true;
        }

        public static long BoardOwner(string boardId)
        {
            HiringBoard? board = HiringBoard.Loaded.FirstOrDefault(b => b != null && b.Id == boardId);
            Piece? piece = board != null ? board.GetComponent<Piece>() : null;
            return piece != null ? piece.GetCreator() : 0L;
        }

        /// <summary>Opens a door as a player standing at <paramref name="from"/> would: it swings away from them.</summary>
        public static void Open(Door d, Vector3 from)
        {
            Vector3 userDir = (from - d.transform.position).normalized;
            d.m_nview.InvokeRPC("UseDoor", Vector3.Dot(d.transform.forward, userDir) < 0f);
        }
    }
}
