using System;
using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Net;

namespace VikingsForHire.Board
{
    /// <summary>
    /// Deconstructing a board that still has hirelings asks first (repairing it doesn't). However a board goes (hammer or destroyed), its
    /// hirelings are sent away; its storage drops like any chest's.
    /// </summary>
    internal static class BoardDestroyPatches
    {
        private static ZDOID _confirmed = ZDOID.None;

        // CheckCanRemovePiece is also called by hammer repair; only deconstructing should ask.
        private static bool _removing;

        // The board being deconstructed with the hammer right now (no confirmation needed), for its charter.
        private static HiringBoard? _removingBoard;

        [HarmonyPatch(typeof(Player), "RemovePiece")]
        private static class RemoveContextPatch
        {
            private static void Prefix()
            {
                _removing = true;
                _removingBoard = null;
            }

            private static void Postfix(bool __result)
            {
                if (__result && _removingBoard != null)
                    VfhLog.Guard(LogCat.Board, "charter.give_failed", () => HiringCharter.Give(_removingBoard));
            }

            private static Exception? Finalizer(Exception? __exception)
            {
                _removing = false;
                _removingBoard = null;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
        private static class ConfirmPatch
        {
            private static bool Prefix(Piece piece, ref bool __result)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    HiringBoard? board = piece != null ? piece.GetComponent<HiringBoard>() : null;
                    if (!_removing || board == null || board.Zdo == null)
                        return true;
                    int count = BoardRosterOps.Read(board.Zdo).Count;
                    if (count == 0)
                    {
                        _removingBoard = board; // handed its charter once the removal goes through
                        return true;
                    }
                    if (board.Zdo.m_uid == _confirmed)
                        return true; // the confirmation already handed the charter

                    __result = false;
                    ZDOID id = board.Zdo.m_uid;
                    string text = Localization.instance.Localize("$vfh_confirm_remove", count.ToString());
                    if (board.Level >= 2)
                        text += "\n" + Localization.instance.Localize("$vfh_confirm_remove_charter", board.Level.ToString());
                    UnifiedPopup.Push(new YesNoPopup(Localization.instance.Localize("$vfh_board"), text,
                        () =>
                        {
                            UnifiedPopup.Pop();
                            if (board == null || board.Zdo == null)
                                return;
                            _confirmed = id;
                            VfhLog.I(LogCat.Board, "board.remove_confirmed", ("board", board.Id), ("hirelings", count));
                            HiringCharter.Give(board);
                            board.GetComponent<WearNTear>()?.Remove();
                        },
                        () => UnifiedPopup.Pop(), localizeText: false));
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("BoardDestroyPatches.CheckCanRemovePiece", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("BoardDestroyPatches.CheckCanRemovePiece", vfhStarted);
                }
            }
        }

        [HarmonyPatch(typeof(WearNTear), "Destroy")]
        private static class DestroyPatch
        {
            private static void Prefix(WearNTear __instance)
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
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
                catch (Exception e)
                {
                    VfhLog.PatchFailed("BoardDestroyPatches.WearNTearDestroy", e);
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("BoardDestroyPatches.WearNTearDestroy", vfhStarted);
                }
            }
        }
    }
}
