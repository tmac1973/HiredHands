using System;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>
    /// Shift+E on a door near a hiring board marks it for hirelings to leave alone (a pen's gate: they walked through the
    /// pen and the animals got out), or clears the mark. Kept on the door, so everyone sees the same; under the ward
    /// rules; shown in the door's hover text. DoorRules.Avoided is what the door helper and link routes check.
    /// </summary>
    internal static class DoorMarks
    {
        private const float BoardNear = 80f;

        private static bool NearABoard(Door d) =>
            HiringBoard.Loaded.Any(b => b != null && UnityEngine.Vector3.Distance(b.transform.position, d.transform.position) <= BoardNear);

        [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
        private static class InteractPatch
        {
            private static bool Prefix(Door __instance, Humanoid character, bool hold, bool alt, ref bool __result)
            {
                try
                {
                    if (!alt || hold || !VfhConfig.ChestModeOnShiftUse.Value || character != Player.m_localPlayer ||
                        __instance.m_nview == null || !__instance.m_nview.IsValid() || !NearABoard(__instance))
                        return true;
                    if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: true))
                    {
                        __result = false;
                        return false;
                    }
                    bool avoid = !DoorRules.Avoided(__instance);
                    __instance.m_nview.ClaimOwnership();
                    __instance.m_nview.GetZDO().Set(DoorRules.AvoidKey, avoid ? 1 : 0);
                    NavLinkRegistry.MarkDirty(__instance.transform.position); // routes through it are re-mapped
                    character.Message(MessageHud.MessageType.Center, Localization.instance.Localize(avoid ? "$vfh_door_avoid" : "$vfh_door_use"));
                    VfhLog.I(LogCat.Nav, "door.marked", ("door", __instance.transform.position), ("avoid", avoid));
                    __result = true;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DoorMarks.Interact", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Door), nameof(Door.GetHoverText))]
        private static class HoverPatch
        {
            private static void Postfix(Door __instance, ref string __result)
            {
                try
                {
                    if (!VfhConfig.ChestModeOnShiftUse.Value || __instance.m_nview == null || !__instance.m_nview.IsValid() || !NearABoard(__instance))
                        return;
                    __result += Localization.instance.Localize("\n[<color=yellow><b>Shift + $KEY_Use</b></color>] " +
                                                               (DoorRules.Avoided(__instance) ? "$vfh_door_avoid" : "$vfh_door_use"));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DoorMarks.Hover", e);
                }
            }
        }
    }
}
