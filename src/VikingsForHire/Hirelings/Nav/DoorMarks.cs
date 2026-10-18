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
    /// Shift+E on a door near a hiring board goes round its three marks: hirelings may use it; only the Rancher may (a
    /// pen's gate: the others walked through the pen and the animals got out); none may. Kept on the door, so everyone sees the same; under the ward
    /// rules; shown in the door's hover text. DoorRules.Avoided is what the door helper and link routes check.
    /// </summary>
    internal static class DoorMarks
    {
        private const float BoardNear = 80f;

        private static string Label(DoorRules.Mark m) => m switch
        {
            DoorRules.Mark.Nobody => "$vfh_door_avoid",
            DoorRules.Mark.Rancher => "$vfh_door_rancher",
            _ => "$vfh_door_use",
        };

        private static bool NearABoard(Door d) =>
            HiringBoard.Loaded.Any(b => b != null && UnityEngine.Vector3.Distance(b.transform.position, d.transform.position) <= BoardNear);

        [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
        private static class InteractPatch
        {
            private static bool Prefix(Door __instance, Humanoid character, bool hold, bool alt, ref bool __result)
            {
                try
                {
                    if (hold || !Work.MarkKeys.Held(VfhConfig.DoorMarkKey, alt) || character != Player.m_localPlayer ||
                        __instance.m_nview == null || !__instance.m_nview.IsValid() || !NearABoard(__instance))
                        return true;
                    if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: true))
                    {
                        __result = false;
                        return false;
                    }
                    // Round the three: anyone → the Rancher only → nobody → anyone.
                    DoorRules.Mark next = DoorRules.MarkOf(__instance) switch
                    {
                        DoorRules.Mark.Anyone => DoorRules.Mark.Rancher,
                        DoorRules.Mark.Rancher => DoorRules.Mark.Nobody,
                        _ => DoorRules.Mark.Anyone,
                    };
                    __instance.m_nview.ClaimOwnership();
                    __instance.m_nview.GetZDO().Set(DoorRules.AvoidKey, (int)next);
                    NavLinkRegistry.MarkDirty(__instance.transform.position); // routes through it are re-mapped
                    character.Message(MessageHud.MessageType.Center, Localization.instance.Localize(Label(next)));
                    VfhLog.I(LogCat.Nav, "door.marked", ("door", __instance.transform.position), ("mark", next));
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
                    if (!Work.MarkKeys.On(VfhConfig.DoorMarkKey) || __instance.m_nview == null || !__instance.m_nview.IsValid() || !NearABoard(__instance))
                        return;
                    __result += Localization.instance.Localize(Work.MarkKeys.Hint(VfhConfig.DoorMarkKey) + Label(DoorRules.MarkOf(__instance)));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("DoorMarks.Hover", e);
                }
            }
        }
    }
}
