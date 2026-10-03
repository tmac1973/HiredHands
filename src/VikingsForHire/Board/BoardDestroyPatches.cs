using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Net;

namespace VikingsForHire.Board
{
    /// <summary>
    /// Deconstructing a board that still has hirelings asks first. However a board goes (hammer or destroyed), its
    /// hirelings are sent away; its storage drops like any chest's.
    /// </summary>
    internal static class BoardDestroyPatches
    {
        private static ZDOID _confirmed = ZDOID.None;

        [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
        private static class ConfirmPatch
        {
            private static bool Prefix(Piece piece, ref bool __result)
            {
                HiringBoard? board = piece != null ? piece.GetComponent<HiringBoard>() : null;
                if (board == null || board.Zdo == null || board.Zdo.m_uid == _confirmed)
                    return true;
                int count = BoardRosterOps.Read(board.Zdo).Count;
                if (count == 0)
                    return true;

                __result = false;
                ZDOID id = board.Zdo.m_uid;
                UnifiedPopup.Push(new YesNoPopup(Localization.instance.Localize("$vfh_board"),
                    Localization.instance.Localize("$vfh_confirm_remove", count.ToString()),
                    () =>
                    {
                        UnifiedPopup.Pop();
                        if (board == null || board.Zdo == null)
                            return;
                        _confirmed = id;
                        VfhLog.I(LogCat.Board, "board.remove_confirmed", ("board", board.Id), ("hirelings", count));
                        board.GetComponent<WearNTear>()?.Remove();
                    },
                    () => UnifiedPopup.Pop(), localizeText: false));
                return false;
            }
        }

        [HarmonyPatch(typeof(WearNTear), "Destroy")]
        private static class DestroyPatch
        {
            private static void Prefix(WearNTear __instance)
            {
                HiringBoard? board = __instance.GetComponent<HiringBoard>();
                if (board == null || board.Zdo == null)
                    return;
                VfhLog.Guard(LogCat.Board, "board.destroy_failed", () =>
                {
                    VfhLog.I(LogCat.Board, "board.destroyed", ("board", board.Id), ("level", board.Level),
                        ("hirelings", BoardRosterOps.Read(board.Zdo).Count));
                    BoardServer.VoidBoard(board.Id);
                });
            }
        }
    }
}
