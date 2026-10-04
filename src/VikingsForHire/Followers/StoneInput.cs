using System;
using HarmonyLib;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// With the Command Stone in hand, the primary attack doesn't swing: it acts on what you're looking at.
    /// A board's hireling (working or idle) → recruit it. Your own follower inside its home radius → release it to work;
    /// elsewhere → toggle it between following and staying put. Shift + attack → release every follower that's home.
    /// </summary>
    internal static class StoneInput
    {
        private const float Range = 50f;
        // The game keeps calling StartAttack while the button is held: act only after a gap (a fresh press), and
        // never on the same hireling twice in quick succession (a held click recruited and then released a guard).
        private const float PressGap = 0.3f;
        private const float SameTargetSeconds = 2f;
        private static readonly int Mask = LayerMask.GetMask("character", "character_net", "character_noenv", "piece", "terrain", "static_solid", "Default");
        private static float _lastCall = -10f;
        private static string _lastHid = "";
        private static float _lastHidAt = -10f;

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        private static class StartAttackPatch
        {
            private static bool Prefix(Humanoid __instance, bool secondaryAttack, ref bool __result)
            {
                try
                {
                    if (__instance != Player.m_localPlayer || !CommandStoneItem.IsStone(__instance.GetRightItem()))
                        return true;
                    __result = false;
                    bool freshPress = Time.time - _lastCall > PressGap;
                    _lastCall = Time.time;
                    if (!secondaryAttack && freshPress)
                        Act(Player.m_localPlayer, __instance.GetRightItem()!.m_quality);
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("StoneInput.StartAttack", e);
                    return true;
                }
            }
        }

        private static void Act(Player me, int quality)
        {
            if (ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift))
            {
                FollowerServer.Send(FollowerServer.Kind.ReleaseAll, "", quality);
                return;
            }
            Hireling? h = LookedAt();
            if (h == null || h.Hid.Length == 0)
            {
                VfhLog.D(LogCat.Follow, "stone.no_target");
                return;
            }
            if (h.Hid == _lastHid && Time.time - _lastHidAt < SameTargetSeconds)
                return;
            _lastHid = h.Hid;
            _lastHidAt = Time.time;
            if (h.Mode == HirelingMode.Following && h.OwnerId == me.GetPlayerID())
            {
                if (Utils.DistanceXZ(h.transform.position, h.Home) <= h.Radius)
                    FollowerServer.Send(FollowerServer.Kind.Release, h.Hid, quality);
                else
                    ToggleStay(h);
                return;
            }
            if (h.Mode == HirelingMode.Following)
            {
                me.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$vfh_follow_taken"));
                return;
            }
            if (!PrivateArea.CheckAccess(h.Home, 0f, flash: true))
                return;
            FollowerServer.Send(FollowerServer.Kind.Recruit, h.Hid, quality);
        }

        // Away from home, aiming at your follower switches it between following you and holding its spot.
        private static void ToggleStay(Hireling h)
        {
            bool stay = h.FollowMode != FollowMode.Stay;
            Vector3 p = h.transform.position;
            MutationService.SubmitHireling(h.Hid, stay
                ? new HirelingOp { FollowMode = FollowMode.Stay, StayPos = (p.x, p.y, p.z) }
                : new HirelingOp { FollowMode = FollowMode.Follow });
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(stay ? "$vfh_follow_stay" : "$vfh_follow_follow", h.DisplayName));
            VfhLog.I(LogCat.Follow, "follow.mode", ("hid", h.Hid), ("mode", stay ? FollowMode.Stay : FollowMode.Follow));
        }

        public static Hireling? LookedAt()
        {
            if (GameCamera.instance == null)
                return null;
            Transform cam = GameCamera.instance.transform;
            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, Range, Mask);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Player>() == Player.m_localPlayer)
                    continue;
                Hireling? h = hit.collider.GetComponentInParent<Hireling>();
                if (h != null)
                    return h;
                if (hit.collider.GetComponentInParent<Character>() == null)
                    return null; // a wall or the ground is in the way
            }
            return null;
        }
    }
}
