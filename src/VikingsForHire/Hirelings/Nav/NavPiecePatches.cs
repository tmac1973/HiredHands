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
                    // Built or loaded in: a real piece has its ZDO by now (the placement ghost never does).
                    if (__instance != null && __instance.m_nview != null && __instance.m_nview.GetZDO() != null)
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
                    // Removed: its ZDO is already gone by now, so tell the ghost apart by its layer instead.
                    if (__instance != null && __instance.gameObject.layer != GhostLayer)
                        Mark(__instance);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("NavPiecePatches.OnDestroy", e);
                }
            }
        }

        private static readonly int GhostLayer = UnityEngine.LayerMask.NameToLayer("ghost");

        private static void Mark(Piece piece)
        {
            if (NavLinkRegistry.Enabled)
                NavLinkRegistry.MarkDirty(piece.transform.position);
        }
    }
}
