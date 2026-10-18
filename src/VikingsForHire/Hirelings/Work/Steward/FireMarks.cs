using System;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Shift+E on a fire or torch near a hiring board marks it for workers to leave alone (a torch up high, for looks: the
    /// Steward can't reach it), or clears the mark. Kept on the fire, under the ward rules; shown in its hover text.
    /// </summary>
    internal static class FireMarks
    {
        private const float BoardNear = 80f;

        private static bool NearABoard(Fireplace f) =>
            HiringBoard.Loaded.Any(b => b != null && UnityEngine.Vector3.Distance(b.transform.position, f.transform.position) <= BoardNear);

        private static bool Applies(Fireplace f) =>
            MarkKeys.On(VfhConfig.FireMarkKey) && f.m_nview != null && f.m_nview.IsValid() && f.m_fuelItem != null && !f.m_infiniteFuel &&
            f.m_canRefill && NearABoard(f);

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
        private static class InteractPatch
        {
            private static bool Prefix(Fireplace __instance, Humanoid user, bool hold, bool alt, ref bool __result)
            {
                try
                {
                    if (hold || !MarkKeys.Held(VfhConfig.FireMarkKey, alt) || user != Player.m_localPlayer || !Applies(__instance))
                        return true;
                    if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: true))
                    {
                        __result = false;
                        return false;
                    }
                    bool avoid = !FiresChore.Avoided(__instance);
                    __instance.m_nview.ClaimOwnership();
                    __instance.m_nview.GetZDO().Set(FiresChore.AvoidKey, avoid ? 1 : 0);
                    user.Message(MessageHud.MessageType.Center, Localization.instance.Localize(avoid ? "$vfh_fire_avoid" : "$vfh_fire_use"));
                    VfhLog.I(LogCat.Smelter, "fire.marked", ("fire", __instance.transform.position), ("avoid", avoid));
                    __result = true;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("FireMarks.Interact", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
        private static class HoverPatch
        {
            private static void Postfix(Fireplace __instance, ref string __result)
            {
                try
                {
                    if (!Applies(__instance))
                        return;
                    __result += Localization.instance.Localize(MarkKeys.Hint(VfhConfig.FireMarkKey) +
                                                               (FiresChore.Avoided(__instance) ? "$vfh_fire_avoid" : "$vfh_fire_use"));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("FireMarks.Hover", e);
                }
            }
        }
    }
}
