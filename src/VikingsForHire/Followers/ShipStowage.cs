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
    /// Followers as ship passengers. When you take the helm (or stand on a ship that's under way), your followers near
    /// you in Follow or Gather Here go aboard: each keeps its own world object, marked with the ship (vfh_stowed), hidden
    /// on every client and carried with the ship by your machine. When you're ashore again (on the ground, off any ship,
    /// out of the water) they step off beside you. A ship that sinks or disappears leaves its passengers in the water
    /// where it was, to swim after you. Nothing is copied, so nothing can be lost or doubled.
    /// </summary>
    internal static class ShipStowage
    {
        private const float TickSeconds = 0.5f;
        private const float LostShipSeconds = 3f;
        private const float UnderWaySpeed = 1f;
        private static float _next;
        private static readonly Dictionary<string, float> MissingSince = new();

        /// <summary>Every frame (Plugin.Update): board or land the local player's followers.</summary>
        public static void Tick()
        {
            Player me = Player.m_localPlayer;
            if (me == null || me.IsDead() || Time.time < _next)
                return;
            _next = Time.time + TickSeconds;
            Ship? ship = me.GetControlledShip() ?? ShipUnderWay(me);
            if (ship != null)
                Board(me, ship);
            else if (Ashore(me))
                Land(me);
        }

        private static Ship? ShipUnderWay(Player me)
        {
            Ship ship = me.GetStandingOnShip();
            if (ship == null)
                return null;
            Rigidbody? body = ship.GetComponent<Rigidbody>();
            return body != null && body.linearVelocity.magnitude > UnderWaySpeed ? ship : null;
        }

        private static bool Ashore(Player me) =>
            me.GetControlledShip() == null && me.GetStandingOnShip() == null && me.IsOnGround() && !me.InWater() && !me.IsTeleporting();

        private static IEnumerable<Hireling> Mine(Player me) =>
            Hireling.Loaded.Where(f => f != null && f.Zdo != null && f.IsOwner && f.Mode == HirelingMode.Following && f.OwnerId == me.GetPlayerID());

        private static void Board(Player me, Ship ship)
        {
            ZNetView? nview = ship.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
                return;
            string id = nview.GetZDO().m_uid.ToString();
            var boarded = new List<string>();
            foreach (Hireling f in Mine(me).Where(f => !f.IsStowed && f.FollowMode != FollowMode.Stay &&
                                                       Vector3.Distance(f.transform.position, me.transform.position) <= VfhConfig.ShipStowRadius.Value).ToList())
            {
                f.Ai.Order = null;
                f.Ai.TravelPin = null;
                f.Zdo!.Set(HirelingZdo.Stowed, id);
                boarded.Add(f.DisplayName);
                VfhLog.I(LogCat.Follow, "follow.stowed", ("hid", f.Hid), ("ship", id), ("shipName", Utils.GetPrefabName(ship.gameObject)));
            }
            if (boarded.Count > 0)
                me.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$vfh_ship_aboard", string.Join(", ", boarded)));
        }

        private static void Land(Player me)
        {
            List<Hireling> stowed = Mine(me).Where(f => f.IsStowed).ToList();
            for (int i = 0; i < stowed.Count; i++)
            {
                Hireling f = stowed[i];
                string ship = f.StowedOn;
                f.Zdo!.Set(HirelingZdo.Stowed, "");
                Vector3 spot = FollowCatchUp.SpotNear(me, outOfSight: false, slot: i) ?? me.transform.position - me.transform.forward * 2f;
                FollowCatchUp.Place(f.Ai, spot, me.transform.position);
                if (f.FollowMode != FollowMode.Follow)
                    MutationService.SubmitHireling(f.Hid, new HirelingOp { FollowMode = FollowMode.Follow });
                VfhLog.I(LogCat.Follow, "follow.unstowed", ("hid", f.Hid), ("ship", ship), ("pos", spot));
            }
            if (stowed.Count > 0)
                me.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$vfh_ship_ashore", stowed.Count.ToString()));
        }

        /// <summary>
        /// Where a passenger rides (owner side, every AI tick): inside the ship's hull. If the ship has been gone for a
        /// few seconds (sunk, broken, unloaded) the passenger is put back into the world where it is, and null is returned.
        /// </summary>
        public static Vector3? CarryPoint(Hireling h)
        {
            string id = h.StowedOn;
            // Only followers ride: one sent back to work meanwhile (its owner logged out at a ship docked at home) steps off.
            if (h.Mode != HirelingMode.Following)
            {
                h.Zdo?.Set(HirelingZdo.Stowed, "");
                VfhLog.I(LogCat.Follow, "follow.unstowed", ("hid", h.Hid), ("ship", id), ("why", "no longer a follower"));
                return null;
            }
            GameObject? ship = Parse(id) is ZDOID zdoid ? ZNetScene.instance?.FindInstance(zdoid) : null;
            if (ship != null)
            {
                MissingSince.Remove(h.Hid);
                return ship.transform.position + ship.transform.up * 1f;
            }
            if (!MissingSince.TryGetValue(h.Hid, out float since))
            {
                MissingSince[h.Hid] = Time.time;
                return h.transform.position;
            }
            if (Time.time - since < LostShipSeconds)
                return h.transform.position;
            MissingSince.Remove(h.Hid);
            h.Zdo?.Set(HirelingZdo.Stowed, "");
            VfhLog.W(LogCat.Follow, "follow.ship_lost", ("hid", h.Hid), ("ship", id), ("pos", h.transform.position));
            return null;
        }

        private static ZDOID? Parse(string id)
        {
            int colon = id.IndexOf(':');
            if (colon <= 0 || !long.TryParse(id.Substring(0, colon), out long user) || !uint.TryParse(id.Substring(colon + 1), out uint n))
                return null;
            return new ZDOID(user, n);
        }

        /// <summary>How many followers ride a ship (for its hover).</summary>
        public static int Passengers(Ship ship)
        {
            ZNetView? nview = ship.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
                return 0;
            string id = nview.GetZDO().m_uid.ToString();
            return Hireling.Loaded.Count(h => h != null && h.StowedOn == id);
        }

        [HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.GetHoverText))]
        private static class HoverPatch
        {
            private static void Postfix(ShipControlls __instance, ref string __result)
            {
                try
                {
                    int n = __instance.m_ship != null ? Passengers(__instance.m_ship) : 0;
                    if (n > 0)
                        __result += "\n" + Localization.instance.Localize("$vfh_ship_passengers", n.ToString());
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("ShipStowage.Hover", e);
                }
            }
        }
    }
}
