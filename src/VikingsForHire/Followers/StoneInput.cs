using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// The Command Stone in hand. Left click acts on what you're looking at:
    ///   a board's hireling → recruit it; your follower at home → post it here (guards, and gatherers with "Works at
    ///   home" off) or back to work (other workers);
    ///   your follower in the field → toggle Follow / Stay; a tree or log → your woodcutters harvest it; a rock →
    ///   your miners mine it; an enemy → your guards attack it; the ground → your followers move there and hold (guards
    ///   at home are posted there instead).
    /// Right click: on your follower at home → back to work; on a posted guard → clear its post; anything else (your
    /// follower in the field included) →
    /// recall every follower within 50 m to follow you; on your follower in another board's area → it joins that board
    /// (Kind.Transfer: if the board has room for it). Middle click (or the RetreatKey setting, stone in hand or not): retreat, everyone within 50 m drops its fight and
    /// follows you, ignoring enemies until things are quiet (a left-click order ends it). All act once per press.
    /// </summary>
    internal static class StoneInput
    {
        private const float Range = 50f;
        private const float OrderRange = 30f;
        private const float RecallRange = 50f;
        // The game keeps calling StartAttack while the button is held: act only after a gap (a fresh press), and
        // never on the same hireling twice in quick succession (a held click recruited and then released a guard).
        private const float PressGap = 0.3f;
        private const float SameTargetSeconds = 2f;
        private static readonly int Mask = LayerMask.GetMask("character", "character_net", "character_noenv", "piece", "piece_nonsolid",
            "terrain", "static_solid", "Default", "Default_small");

        private static float _lastCall = -10f;
        private static string _lastHid = "";
        private static float _lastHidAt = -10f;
        private static float _lastBlockPress = -10f;
        private static float _lastMiddlePress = -10f;

        public static bool StoneInHand =>
            Player.m_localPlayer != null && CommandStoneItem.IsStone(Player.m_localPlayer.GetRightItem());

        private static int Quality => Player.m_localPlayer.GetRightItem()?.m_quality ?? 0;

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        private static class StartAttackPatch
        {
            private static bool Prefix(Humanoid __instance, bool secondaryAttack, ref bool __result)
            {
                try
                {
                    if (__instance != Player.m_localPlayer || !StoneInHand)
                        return true;
                    __result = false;
                    bool freshPress = Time.time - _lastCall > PressGap;
                    _lastCall = Time.time;
                    if (!secondaryAttack && freshPress)
                        LeftClick(Player.m_localPlayer);
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("StoneInput.StartAttack", e);
                    return true;
                }
            }
        }

        /// <summary>Every frame (Plugin.Update): right click (the block button) with the stone in hand.</summary>
        public static void Tick()
        {
            if (Player.m_localPlayer == null || !Player.m_localPlayer.TakeInput())
                return;
            // The retreat key (unset by default): the middle click without needing the stone in hand.
            if (Config.VfhConfig.RetreatKey.Value.MainKey != KeyCode.None && Config.VfhConfig.RetreatKey.Value.IsDown() && Time.time - _lastMiddlePress > PressGap)
            {
                _lastMiddlePress = Time.time;
                VfhLog.Guard(LogCat.Orders, "stone.retreat_key_failed", () => Retreat(Player.m_localPlayer));
            }
            if (!StoneInHand)
                return;
            if (ZInput.GetMouseButtonDown(2) && Time.time - _lastMiddlePress > PressGap)
            {
                _lastMiddlePress = Time.time;
                VfhLog.Guard(LogCat.Orders, "stone.middle_click_failed", () => Retreat(Player.m_localPlayer));
            }
            if (!(ZInput.GetButtonDown("Block") || ZInput.GetButtonDown("JoyBlock")) || Time.time - _lastBlockPress < PressGap)
                return;
            _lastBlockPress = Time.time;
            VfhLog.Guard(LogCat.Follow, "stone.right_click_failed", () => RightClick(Player.m_localPlayer));
        }

        private static void LeftClick(Player me)
        {
            RaycastHit? hit = Look(out Hireling? h);
            if (h != null)
            {
                if (!Fresh(h))
                    return;
                if (h.Mode == HirelingMode.Following && h.OwnerId == me.GetPlayerID())
                {
                    if (AtHome(h))
                    {
                        if (Postable(h))
                            FollowerServer.Send(FollowerServer.Kind.Post, h.Hid, Quality, h.transform.position, me.transform.eulerAngles.y);
                        else
                            FollowerServer.Send(FollowerServer.Kind.Release, h.Hid, Quality);
                    }
                    else
                    {
                        ToggleStay(h);
                    }
                    return;
                }
                if (h.Mode == HirelingMode.Following)
                {
                    Say("$vfh_follow_taken");
                    return;
                }
                if (PrivateArea.CheckAccess(h.Home, 0f, flash: true))
                    FollowerServer.Send(FollowerServer.Kind.Recruit, h.Hid, Quality);
                return;
            }
            if (hit is RaycastHit target)
                ContextOrder(me, target);
        }

        private static void RightClick(Player me)
        {
            Look(out Hireling? h);
            if (h != null && Fresh(h))
            {
                if (h.Mode == HirelingMode.Following && h.OwnerId == me.GetPlayerID())
                {
                    // At home: back to work. In the field a right click is just a recall: sending a follower home is
                    // in the Shift+E panel, so a misclick in a fight can't send one away.
                    if (AtHome(h))
                    {
                        FollowerServer.Send(FollowerServer.Kind.Release, h.Hid, Quality);
                        return;
                    }
                    // In another board's area: it joins that board (if it has room) and works there.
                    if (OtherBoard(h) is HiringBoard other)
                    {
                        if (PrivateArea.CheckAccess(other.transform.position, 0f, flash: true))
                            FollowerServer.Send(FollowerServer.Kind.Transfer, h.Hid, Quality, Vector3.zero, 0f, other.Id);
                        return;
                    }
                }
                if (h.HasPost && h.Mode == HirelingMode.Working)
                {
                    if (PrivateArea.CheckAccess(h.Home, 0f, flash: true))
                        FollowerServer.Send(FollowerServer.Kind.ClearPost, h.Hid, Quality);
                    return;
                }
            }
            Recall(me);
        }

        // Middle click: the escape button. Everyone near you drops its fight and runs after you, ignoring enemies, until
        // things have been quiet for a while (HirelingAI.RetreatOrdered); stances are untouched.
        internal static void Retreat(Player me)
        {
            int n = 0;
            foreach (Hireling f in MyFollowers(me, RecallRange))
            {
                f.Ai.Order = null;
                f.Ai.OrderRetreat();
                if (f.FollowMode != FollowMode.Follow)
                    MutationService.SubmitHireling(f.Hid, new HirelingOp { FollowMode = FollowMode.Follow });
                n++;
            }
            VfhLog.I(LogCat.Orders, "order.retreat", ("followers", n));
            Say(n == 0 ? "$vfh_order_none_near" : Localization.instance.Localize("$vfh_order_retreat", n.ToString()));
        }

        // Right click on nothing: everyone near you follows you again, wherever you're looking.
        private static void Recall(Player me)
        {
            int n = 0;
            foreach (Hireling f in MyFollowers(me, RecallRange))
            {
                f.Ai.Order = null;
                if (f.FollowMode != FollowMode.Follow)
                    MutationService.SubmitHireling(f.Hid, new HirelingOp { FollowMode = FollowMode.Follow });
                n++;
            }
            VfhLog.I(LogCat.Orders, "order.recall", ("followers", n));
            Say(n == 0 ? "$vfh_order_none_near" : Localization.instance.Localize("$vfh_order_recall", n.ToString()));
        }

        private static void ContextOrder(Player me, RaycastHit hit)
        {
            List<Hireling> near = MyFollowers(me, OrderRange).ToList();
            if (near.Count == 0)
                return;
            foreach (Hireling f in near)
                f.Ai.CancelRetreat();
            Collider c = hit.collider;
            Component? tree = (Component?)c.GetComponentInParent<TreeBase>() ?? c.GetComponentInParent<TreeLog>();
            if (tree == null && c.GetComponentInParent<Destructible>() is Destructible dt && dt.m_destructibleType == DestructibleType.Tree)
                tree = dt;
            Component? rock = (Component?)c.GetComponentInParent<MineRock5>() ?? c.GetComponentInParent<MineRock>();
            if (rock == null && tree == null && c.GetComponentInParent<Destructible>() is Destructible dr && dr.m_destructibleType == DestructibleType.Default)
                rock = dr;
            Character? enemy = c.GetComponentInParent<Character>();
            Container? chest = c.GetComponentInParent<Container>();

            if (chest != null && Hireling.Of(chest) == null)
                Unload(me, near, chest);
            else if (tree != null)
                Harvest(near, JobType.Woodcutter, tree, "$vfh_order_chop", "$vfh_order_no_woodcutter");
            else if (rock != null)
                Harvest(near, JobType.Miner, rock, "$vfh_order_mine", "$vfh_order_no_miner");
            else if (enemy != null && enemy != me && Hireling.Of(enemy) == null && BaseAI.IsEnemy(me, enemy))
                Attack(near, enemy);
            else
                MoveHold(me, near, hit.point);
        }

        private static void Harvest(List<Hireling> near, JobType job, Component target, string done, string none)
        {
            int n = 0;
            foreach (Hireling f in near.Where(f => f.Job == job && f.Ai.Gather != null))
            {
                if (!f.Ai.Gather!.CanHarvest(target, f))
                    continue;
                GiveHarvestOrder(f, target);
                n++;
            }
            VfhLog.I(LogCat.Orders, "order.harvest", ("job", job), ("target", target.name), ("followers", n));
            Say(n == 0 ? none : Localization.instance.Localize(done, n.ToString()));
        }

        /// <summary>
        /// A harvest order: work this tree or rock and what it leaves behind, staying at the job (Stay there) so the
        /// follower doesn't leave it as soon as you move, nor get sent home as "left behind"; it waits there afterwards.
        /// </summary>
        public static void GiveHarvestOrder(Hireling f, Component target)
        {
            f.Ai.Order = FieldOrder.Harvest(target);
            f.Ai.Gather?.Force(f.Ai, target);
            Vector3 at = target.transform.position;
            if (f.FollowMode != FollowMode.Stay || Vector3.Distance(f.StayPos, at) > 1f)
                MutationService.SubmitHireling(f.Hid, new HirelingOp { FollowMode = FollowMode.Stay, StayPos = (at.x, at.y, at.z) });
        }

        // A chest: everyone near you carrying something that chest already holds puts it in, then carries on.
        internal static void Unload(Player me, List<Hireling> near, Container chest)
        {
            if (!chest.CheckAccess(me.GetPlayerID()) || !PrivateArea.CheckAccess(chest.transform.position, 0f, flash: true))
            {
                Say("$vfh_order_unload_no_access");
                return;
            }
            int n = 0;
            foreach (Hireling f in near)
            {
                if (f.CargoInventory == null || FieldOrder.ForChest(f.CargoInventory, chest).Count == 0)
                    continue;
                f.Ai.Order = FieldOrder.Unload(chest);
                n++;
            }
            VfhLog.I(LogCat.Orders, "order.unload", ("chest", chest.transform.position), ("followers", n));
            Say(n == 0 ? "$vfh_order_unload_none" : Localization.instance.Localize("$vfh_order_unload", n.ToString()));
        }

        private static void Attack(List<Hireling> near, Character enemy)
        {
            int n = 0;
            foreach (Hireling f in near.Where(f => f.Job.IsGuard()))
            {
                f.Ai.Order = new FieldOrder { Kind = FieldOrder.OrderKind.Attack, Enemy = enemy, Position = enemy.transform.position, Until = Time.time + FieldOrder.Lifetime };
                n++;
            }
            VfhLog.I(LogCat.Orders, "order.attack", ("target", enemy.m_name), ("guards", n));
            Say(n == 0 ? "$vfh_order_no_guard" : Localization.instance.Localize("$vfh_order_attack", n.ToString()));
        }

        // Move there and hold: each follower gets its own slot on a 2 m ring round the point, then stays. At home a
        // guard is posted at its slot instead (facing the way you face): pointing a guard at a spot in your base means
        // "guard this", and a plain hold there kept it on the stone as a follower.
        private static void MoveHold(Player me, List<Hireling> near, Vector3 point)
        {
            int posted = 0;
            for (int i = 0; i < near.Count; i++)
            {
                Hireling f = near[i];
                Vector3 slot = near.Count == 1 ? point : point + Quaternion.Euler(0f, 360f * i / near.Count, 0f) * Vector3.forward * 2f;
                f.Ai.Order = null;
                if (Postable(f) && Utils.DistanceXZ(slot, f.Home) <= f.Radius)
                {
                    FollowerServer.Send(FollowerServer.Kind.Post, f.Hid, Quality, slot, me.transform.eulerAngles.y);
                    posted++;
                    continue;
                }
                MutationService.SubmitHireling(f.Hid, new HirelingOp { FollowMode = FollowMode.Stay, StayPos = (slot.x, slot.y, slot.z) });
            }
            VfhLog.I(LogCat.Orders, "order.move", ("point", point), ("followers", near.Count), ("posted", posted));
            if (posted < near.Count)
                Say(Localization.instance.Localize("$vfh_order_move", (near.Count - posted).ToString()));
        }

        // Away from home, aiming at your follower switches it between following you and holding its spot.
        private static void ToggleStay(Hireling h)
        {
            h.Ai.CancelRetreat();
            bool stay = h.FollowMode == FollowMode.Follow;
            Vector3 p = h.transform.position;
            MutationService.SubmitHireling(h.Hid, stay
                ? new HirelingOp { FollowMode = FollowMode.Stay, StayPos = (p.x, p.y, p.z) }
                : new HirelingOp { FollowMode = FollowMode.Follow });
            Say(Localization.instance.Localize(stay ? "$vfh_follow_stay" : "$vfh_follow_follow", h.DisplayName));
            VfhLog.I(LogCat.Follow, "follow.mode", ("hid", h.Hid), ("mode", stay ? FollowMode.Stay : FollowMode.Follow));
        }

        /// <summary>Your followers within range that can take orders (not passengers below deck).</summary>
        public static IEnumerable<Hireling> MyFollowers(Player me, float range) =>
            Hireling.Loaded.Where(f => f != null && f.Mode == HirelingMode.Following && f.OwnerId == me.GetPlayerID() && !f.IsStowed &&
                                       Vector3.Distance(f.transform.position, me.transform.position) <= range);

        // Clicked into place at home: guards are posted; so are gatherers with "Works at home" off, which would otherwise
        // wander about the base with nothing to do. Other workers go back to work.
        private static bool Postable(Hireling h) => h.Job.IsGuard() || !h.WorksAtHome;

        // The nearest board other than its own whose area (the board level's largest work radius) the hireling is in.
        private static HiringBoard? OtherBoard(Hireling h)
        {
            var rules = new LevelRules(Config.DataStore.Current);
            Vector3 at = h.transform.position;
            return HiringBoard.Loaded
                .Where(b => b != null && b.Zdo != null && b.Id.Length > 0 && b.Id != h.BoardId &&
                            Utils.DistanceXZ(at, b.transform.position) <= rules.MaxWorkRadius(b.Level))
                .OrderBy(b => Utils.DistanceXZ(at, b.transform.position))
                .FirstOrDefault();
        }

        private static bool AtHome(Hireling h) => Utils.DistanceXZ(h.transform.position, h.Home) <= h.Radius;

        private static bool Fresh(Hireling h)
        {
            if (h.Hid == _lastHid && Time.time - _lastHidAt < SameTargetSeconds)
                return false;
            _lastHid = h.Hid;
            _lastHidAt = Time.time;
            return true;
        }

        private static void Say(string text) =>
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, Localization.instance.Localize(text));

        /// <summary>What's under the crosshair (up to 50 m): the first hit that isn't you, and the hireling it belongs to, if any.</summary>
        private static RaycastHit? Look(out Hireling? hireling)
        {
            hireling = null;
            if (GameCamera.instance == null)
                return null;
            Transform cam = GameCamera.instance.transform;
            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, Range, Mask);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Player>() == Player.m_localPlayer)
                    continue;
                hireling = hit.collider.GetComponentInParent<Hireling>();
                return hit;
            }
            return null;
        }
    }
}
