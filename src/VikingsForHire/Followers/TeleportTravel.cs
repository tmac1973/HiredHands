using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// Followers go with you when you teleport (portals, dungeon entrances and exits, any Player.TeleportTo). Your
    /// followers within PortalFollowRadius in Follow or Gather Here are moved to the far side the moment you step in and
    /// held there (the same world object: it just moves, so nothing is copied or lost, and a crash mid-trip leaves it
    /// waiting at the exit). Once you've arrived they're set down beside you. Through a portal, a follower carrying
    /// something the portal won't take (ore, metal) stays behind in Stay, as you would, unless the portal allows
    /// everything or AllowNonTeleportableThroughPortals is on. Dungeon doors take anything.
    /// </summary>
    internal static class TeleportTravel
    {
        private const float ArriveTimeoutSeconds = 40f;

        private sealed class Traveler
        {
            public string Hid = "";
            public ZDOID Zdo;
            public float Deadline;
        }

        private static readonly List<Traveler> Travelers = new();
        private static TeleportWorld? _portal;

        public static int InTransit => Travelers.Count;

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        private static class PortalPatch
        {
            private static void Prefix(TeleportWorld __instance) => _portal = __instance;
            private static void Finalizer() => _portal = null;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
        private static class TeleportToPatch
        {
            private static void Postfix(Player __instance, Vector3 pos, Quaternion rot, bool distantTeleport, bool __result)
            {
                try
                {
                    if (__result && __instance == Player.m_localPlayer)
                        Depart(__instance, pos, rot, distantTeleport);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("TeleportTravel.TeleportTo", e);
                }
            }
        }

        private static void Depart(Player me, Vector3 pos, Quaternion rot, bool distant)
        {
            bool portal = _portal != null;
            bool takesAnything = !portal || _portal!.m_allowAllItems || VfhConfig.AllowNonTeleportableThroughPortals.Value;
            List<Hireling> going = Hireling.Loaded.Where(f => f != null && f.Zdo != null && f.IsOwner && f.Mode == HirelingMode.Following &&
                                                             f.OwnerId == me.GetPlayerID() && !f.IsStowed && f.FollowMode != FollowMode.Stay &&
                                                             Vector3.Distance(f.transform.position, me.transform.position) <= VfhConfig.PortalFollowRadius.Value).ToList();
            int slot = 0;
            foreach (Hireling f in going)
            {
                if (!takesAnything && f.CargoInventory != null && !f.CargoInventory.IsTeleportable(false))
                {
                    Vector3 p = f.transform.position;
                    MutationService.SubmitHireling(f.Hid, new HirelingOp { FollowMode = FollowMode.Stay, StayPos = (p.x, p.y, p.z) });
                    me.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$vfh_portal_cargo_blocked", f.DisplayName));
                    VfhLog.I(LogCat.Follow, "follow.portal_blocked", ("hid", f.Hid), ("why", "cargo not teleportable"));
                    continue;
                }
                // Beside the exit point, left and right in turn (behind it is usually the portal itself).
                float side = (slot % 2 == 0 ? 1f : -1f) * 1.5f * (slot / 2 + 1);
                Vector3 dest = pos + rot * new Vector3(side, 0.3f, 0.5f);
                slot++;
                f.Ai.Order = null;
                FollowCatchUp.Place(f.Ai, dest, pos);
                f.Ai.TravelPin = dest;
                Travelers.Add(new Traveler { Hid = f.Hid, Zdo = f.Zdo!.m_uid, Deadline = Time.time + ArriveTimeoutSeconds });
                VfhLog.I(LogCat.Follow, "follow.travel_start", ("hid", f.Hid), ("to", dest), ("portal", portal), ("distant", distant));
            }
        }

        /// <summary>Every frame (Plugin.Update): once you've arrived, set your travelling followers down beside you.</summary>
        public static void Tick()
        {
            if (Travelers.Count == 0)
                return;
            Player me = Player.m_localPlayer;
            if (me == null)
            {
                Travelers.Clear();
                return;
            }
            if (me.IsTeleporting() || ZNetScene.instance == null || !ZNetScene.instance.IsAreaReady(me.transform.position))
                return;
            for (int i = Travelers.Count - 1; i >= 0; i--)
            {
                Traveler t = Travelers[i];
                Hireling? h = Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == t.Hid);
                if (h != null && h.IsOwner)
                {
                    h.Ai.TravelPin = null;
                    Vector3 spot = FollowCatchUp.SpotNear(me, outOfSight: false, slot: i) ?? me.transform.position;
                    FollowCatchUp.Place(h.Ai, spot, me.transform.position);
                    Travelers.RemoveAt(i);
                    VfhLog.I(LogCat.Follow, "follow.travel_end", ("hid", t.Hid), ("pos", spot));
                    continue;
                }
                // Not loaded here: the trip ended somewhere else (a blocked dungeon door sends you back). Fetch it.
                if (h == null && ZDOMan.instance.GetZDO(t.Zdo) is ZDO zdo &&
                    Vector3.Distance(zdo.GetPosition(), me.transform.position) > 30f && (zdo.GetOwner() == 0L || zdo.IsOwner()))
                {
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    zdo.SetPosition(me.transform.position - me.transform.forward * 2f);
                    VfhLog.I(LogCat.Follow, "follow.travel_fetched", ("hid", t.Hid));
                }
                if (Time.time > t.Deadline)
                {
                    if (h != null)
                        h.Ai.TravelPin = null;
                    Travelers.RemoveAt(i);
                    VfhLog.W(LogCat.Follow, "follow.travel_timeout", ("hid", t.Hid), ("loaded", h != null));
                }
            }
        }
    }
}
