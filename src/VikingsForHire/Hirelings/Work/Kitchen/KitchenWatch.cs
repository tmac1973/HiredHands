using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Kitchen
{
    /// <summary>
    /// A Cook keeps an eye on its stoves while nobody's about. Away from the base, nothing runs the Cook, but a spit or oven
    /// catches up on the time it missed when its area loads again, and food that sat past done comes back as coal (Tim left
    /// Svanhild's three spits full and found coal). In a Cook's area, a catch-up stops each item at done instead: the Cook
    /// takes it off when it's back. Ordinary updates (someone's there) are left as they are: then the Cook has to be on time.
    /// </summary>
    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.UpdateCooking))]
    internal static class KitchenWatch
    {
        // Updates come about once a second; a longer gap is the area having been unloaded (or the game paused).
        private const double CatchUpSeconds = 10.0;

        private static void Prefix(CookingStation __instance)
        {
            try
            {
                if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || ZNet.instance == null)
                    return;
                ZDO zdo = __instance.m_nview.GetZDO();
                DateTime now = ZNet.instance.GetTime();
                double away = (now - new DateTime(zdo.GetLong(ZDOVars.s_startTime, now.Ticks))).TotalSeconds;
                if (away < CatchUpSeconds || !Watched(__instance.transform.position))
                    return;
                bool heated = (__instance.m_requireFire && __instance.IsFireLit()) || (__instance.m_useFuel && __instance.GetFuel() > 0f);
                zdo.Set(ZDOVars.s_startTime, now.Ticks); // the time is spent here: the update itself then sees none
                if (!heated)
                    return;
                __instance.UpdateFuel((float)away);
                int saved = 0;
                for (int i = 0; i < __instance.m_slots.Length; i++)
                {
                    __instance.GetSlot(i, out string item, out float cooked, out CookingStation.Status st, out bool cheated);
                    if (item.Length == 0 || st == CookingStation.Status.Burnt || __instance.GetItemConversion(item) is not CookingStation.ItemConversion c)
                        continue;
                    float cookTime = c.m_cookTime;
                    if (st == CookingStation.Status.Done)
                    {
                        // Done already: it waits there done (a full cook time before it could burn once someone's back).
                        __instance.SetSlot(i, item, Mathf.Min(cooked, cookTime), st, cheated);
                        saved++;
                    }
                    else if (cooked + away >= cookTime)
                    {
                        __instance.SetSlot(i, c.m_to.name, cookTime, CookingStation.Status.Done, cheated);
                        saved++;
                    }
                    else
                        __instance.SetSlot(i, item, cooked + (float)away, st, cheated);
                }
                if (saved > 0)
                    VfhLog.I(LogCat.Work, "cook.watched", ("station", __instance.transform.position), ("away", Math.Round(away)), ("kept", saved));
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("KitchenWatch.UpdateCooking", e);
            }
        }

        // In the area of an active Cook (round its post when posted). Boards from the world data, not the loaded objects:
        // coming back, a spit can be built before its board, and it catches up as it's built.
        private static bool Watched(Vector3 at)
        {
            foreach (ZDO b in Net.WorldIndex.AllBoards(fresh: true))
            {
                if (b == null || Utils.DistanceXZ(b.GetPosition(), at) > 150f)
                    continue;
                foreach (ContractEntry e in BoardRosterOps.Read(b).Entries)
                {
                    if (e.Job != JobType.Cook || e.State != ContractState.Active)
                        continue;
                    Vector3 centre = e.Post != null ? new Vector3(e.Post.X, e.Post.Y, e.Post.Z) : b.GetPosition();
                    if (Utils.DistanceXZ(at, centre) <= e.Radius)
                        return true;
                }
            }
            return false;
        }
    }
}
