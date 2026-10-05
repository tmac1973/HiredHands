using System;
using HarmonyLib;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>Any piece built or removed (or loaded in) near a board: rescan that board's links shortly.</summary>
    internal static class NavPiecePatches
    {
        [HarmonyPatch(typeof(Piece), "Awake")]
        private static class AwakePatch
        {
            private static void Postfix(Piece __instance)
            {
                try
                {
                    Mark(__instance);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("NavPiecePatches.Awake", e);
                }
            }
        }

        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        private static class OnDestroyPatch
        {
            private static void Prefix(Piece __instance)
            {
                try
                {
                    Mark(__instance);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("NavPiecePatches.OnDestroy", e);
                }
            }
        }

        private static void Mark(Piece piece)
        {
            // Not the ghost a player is placing.
            if (!NavLinkRegistry.Enabled || piece == null || piece.m_nview == null || piece.m_nview.GetZDO() == null)
                return;
            NavLinkRegistry.MarkDirty(piece.transform.position);
        }
    }
}
