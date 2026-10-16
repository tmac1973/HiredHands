using System.Linq;
using UnityEngine;
using VikingsForHire.Board;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>Which doors a hireling may use, shared by the door helper and link routes.</summary>
    internal static class DoorRules
    {
        public static bool IsOpen(Door d) => d.m_nview != null && d.m_nview.IsValid() && d.m_nview.GetZDO().GetInt(ZDOVars.s_state) != 0;

        /// <summary>Marked by a player (Shift+E on it): every hireling may use it, none, or only the Rancher (a pen's gate).</summary>
        public const string AvoidKey = "vfh_door_avoid";

        public enum Mark
        {
            Anyone = 0,
            Nobody = 1,
            Rancher = 2,
        }

        public static Mark MarkOf(Door? d) =>
            d != null && d.m_nview != null && d.m_nview.IsValid() ? (Mark)Mathf.Clamp(d.m_nview.GetZDO().GetInt(AvoidKey), 0, 2) : Mark.Anyone;

        /// <summary>Marked for every hireling to leave alone.</summary>
        public static bool Avoided(Door? d) => MarkOf(d) == Mark.Nobody;

        /// <summary>Not for this hireling: marked to leave alone, or for the Rancher only and it isn't one.</summary>
        public static bool AvoidedBy(Door? d, Hireling h) => MarkOf(d) switch
        {
            Mark.Nobody => true,
            Mark.Rancher => h.Job != Core.JobType.Rancher,
            _ => false,
        };

        /// <summary>A route link this hireling may take (a Rancher-only gate only for the Rancher).</summary>
        public static bool MayTake(Core.Nav.NavLink link, Hireling h) => !link.RancherOnly || h.Job == Core.JobType.Rancher;

        // Not locked, can be closed again, not marked off for this hireling, and the board's owner has access under any ward covering it.
        public static bool Usable(Door? d, long boardOwner, Hireling h)
        {
            if (d == null || d.m_nview == null || !d.m_nview.IsValid() || d.m_keyItem != null || d.m_canNotBeClosed || AvoidedBy(d, h))
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
