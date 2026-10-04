using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Followers;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Testing
{
    /// <summary>Phase 12 (Command Stone, followers) fixtures and checks.</summary>
    internal static class FixturesFollow
    {
        public static void Register()
        {
            Fixtures.Add("stone", "<quality> - put a Command Stone of that quality in your hands", Stone);
            Fixtures.Add("stone_mats", "<quality> - give yourself the materials that quality needs", StoneMats);
            Fixtures.Add("recruit_posted", "- ask the server to recruit the hireling from your last contract (as the stone does)", _ => Op(FollowerServer.Kind.Recruit, Posted()));
            Fixtures.Add("release_posted", "- ask the server to send the hireling from your last contract back to work", _ => Op(FollowerServer.Kind.Release, Posted()));
            Fixtures.Add("post_posted", "<right|back|front|left> <distance> - post the guard from your last contract at that spot next to the board, facing away from it", PostPosted);
            Fixtures.Add("clear_post_posted", "- clear the post of the guard from your last contract", _ => Op(FollowerServer.Kind.ClearPost, Posted()));
            Fixtures.Add("park_posted", "<GatherHere|Stay|Follow> <right|back|front|left> <distance> - set your follower's follow mode, parked at that spot next to the board", Park);
            Fixtures.Add("order_harvest", "<tag> - give the follower from your last contract a stone order to harvest a tagged tree or rock", HarvestTagged);
            Fixtures.Add("order_harvest_nearest", "- give your followers a stone order to harvest the nearest tree or rock to the board", HarvestNearest);
            Fixtures.Add("strand_posted", "<distance> - put your follower from your last contract that far behind the camera, out of your view (to test catching up)", Strand);
            Fixtures.Add("follow_stats_reset", "- start counting follower lag and catch-up teleports afresh", _ => ResetStats());
            Fixtures.Add("release_all", "- ask the server to send every follower that's home back to work", _ => Op(FollowerServer.Kind.ReleaseAll, ""));

            TestHarness.RegisterCheck("followers", "- how many followers you have (server's count)",
                args => FollowerServer.FollowersOf(long.Parse(args.ElementAtOrDefault(0) ?? "0", CultureInfo.InvariantCulture)).Count.ToString(),
                serverSide: true,
                prepareArgs: _ => new[] { (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L).ToString(CultureInfo.InvariantCulture) });
            TestHarness.RegisterCheck("follow_max_lag", "- the furthest (m) any follower has been behind you since follow_stats_reset",
                _ => FollowCatchUp.MaxLag.ToString("0.0", CultureInfo.InvariantCulture));
            TestHarness.RegisterCheck("follow_teleports", "- catch-up teleports since follow_stats_reset", _ => FollowCatchUp.Teleports.ToString());
            TestHarness.RegisterCheck("craftable", "<quality> - whether you could make that Command Stone quality at the nearest workbench right now", args =>
            {
                int q = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
                Recipe recipe = CommandStoneItem.Recipe ?? throw new InvalidOperationException("Command Stone recipe not registered");
                Inventory inv = Player.m_localPlayer.GetInventory();
                bool mats = recipe.m_resources.All(r => r.m_resItem == null || inv.CountItems(r.m_resItem.m_itemData.m_shared.m_name) >= r.GetAmount(q));
                CraftingStation? bench = Workbench();
                bool board = StoneCraftingGate.BoardOk(q, bench, out _);
                VfhLog.I(LogCat.Test, "check.craftable", ("quality", q), ("materials", mats), ("board", board), ("workbench", bench != null));
                return mats && board ? "true" : "false";
            });
        }

        private static string Posted()
        {
            string hid = BoardContracts.LastPostedHid;
            if (hid.Length == 0)
                throw new InvalidOperationException("no contract posted yet");
            return hid;
        }

        private static Vector3 Spot(string side, float dist, out Vector3 dir)
        {
            HiringBoard board = HiringBoard.Nearest(Player.m_localPlayer.transform.position, 60f) ?? throw new InvalidOperationException("no hiring board within 60m");
            dir = side switch
            {
                "right" => board.transform.right,
                "left" => -board.transform.right,
                "back" => -board.transform.forward,
                _ => board.transform.forward,
            };
            Vector3 p = board.transform.position + dir * dist;
            p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }

        private static IEnumerator PostPosted(string[] args)
        {
            Vector3 p = Spot(args.ElementAtOrDefault(0) ?? "right", float.Parse(args.ElementAtOrDefault(1) ?? "10", CultureInfo.InvariantCulture), out Vector3 dir);
            FollowerServer.Send(FollowerServer.Kind.Post, Posted(), StoneQuality(), p, Quaternion.LookRotation(dir).eulerAngles.y);
            VfhLog.I(LogCat.Test, "fixture.post", ("hid", Posted()), ("pos", p));
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator Park(string[] args)
        {
            FollowMode mode = (FollowMode)Enum.Parse(typeof(FollowMode), args.ElementAtOrDefault(0) ?? "Stay", true);
            Vector3 p = Spot(args.ElementAtOrDefault(1) ?? "back", float.Parse(args.ElementAtOrDefault(2) ?? "20", CultureInfo.InvariantCulture), out _);
            MutationService.SubmitHireling(Posted(), new HirelingOp { FollowMode = mode, StayPos = (p.x, p.y, p.z) });
            VfhLog.I(LogCat.Test, "fixture.park", ("hid", Posted()), ("mode", mode), ("pos", p));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator HarvestTagged(string[] args)
        {
            Hireling h = Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == Posted()) ?? throw new InvalidOperationException("posted hireling not here");
            GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged object");
            Component target = (Component?)go.GetComponent<TreeBase>() ?? (Component?)go.GetComponent<MineRock5>() ?? (Component?)go.GetComponent<MineRock>()
                               ?? go.GetComponent<Destructible>() ?? throw new InvalidOperationException("tagged object isn't a tree or rock");
            if (h.Ai.Gather == null || !h.Ai.Gather.CanHarvest(target, h))
                throw new InvalidOperationException("that follower can't harvest it");
            h.Ai.Order = new FieldOrder { Kind = FieldOrder.OrderKind.Harvest, Target = target, Position = target.transform.position, Until = Time.time + FieldOrder.Lifetime };
            h.Ai.Gather.Force(h.Ai, target);
            VfhLog.I(LogCat.Test, "fixture.order_harvest", ("hid", h.Hid), ("target", target.name));
            yield return null;
        }

        private static IEnumerator HarvestNearest(string[] args)
        {
            Hireling h = Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == Posted()) ?? throw new InvalidOperationException("posted hireling not here");
            if (h.Ai.Gather == null)
                throw new InvalidOperationException("that hireling doesn't gather");
            Vector3 at = h.Home;
            Component? target = UnityEngine.Object.FindObjectsByType<TreeBase>(FindObjectsSortMode.None).Cast<Component>()
                .Concat(UnityEngine.Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None))
                .Concat(UnityEngine.Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None))
                .Where(c => h.Ai.Gather.CanHarvest(c, h))
                .OrderBy(c => Vector3.Distance(c.transform.position, at)).FirstOrDefault()
                ?? throw new InvalidOperationException("nothing harvestable near the board");
            h.Ai.Order = new FieldOrder { Kind = FieldOrder.OrderKind.Harvest, Target = target, Position = target.transform.position, Until = Time.time + FieldOrder.Lifetime };
            h.Ai.Gather.Force(h.Ai, target);
            VfhLog.I(LogCat.Test, "fixture.order_harvest", ("hid", h.Hid), ("target", target.name), ("dist", Vector3.Distance(target.transform.position, at)));
            yield return null;
        }

        private static IEnumerator ResetStats()
        {
            FollowCatchUp.ResetStats();
            VfhLog.I(LogCat.Test, "fixture.follow_stats_reset");
            yield return null;
        }

        private static IEnumerator Strand(string[] args)
        {
            float dist = float.Parse(args.ElementAtOrDefault(0) ?? "50", CultureInfo.InvariantCulture);
            Hireling h = Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == Posted()) ?? throw new InvalidOperationException("posted hireling not here");
            // Followers are simulated by their owner; the server hands the follower over within a few seconds.
            ZNetView nview = h.GetComponent<ZNetView>();
            for (float waited = 0f; !nview.IsOwner() && waited < 10f; waited += 0.5f)
                yield return new WaitForSeconds(0.5f);
            if (!nview.IsOwner())
                throw new InvalidOperationException("the follower isn't simulated here");
            Transform cam = GameCamera.instance.transform;
            Vector3 back = -Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 p = Player.m_localPlayer.transform.position + back * dist;
            p.y = ZoneSystem.instance.GetGroundHeight(p) + 0.1f;
            h.transform.position = p;
            if (h.GetComponent<Rigidbody>() is Rigidbody body)
            {
                body.position = p;
                body.linearVelocity = Vector3.zero;
            }
            h.Zdo?.SetPosition(p);
            VfhLog.I(LogCat.Test, "fixture.strand", ("hid", h.Hid), ("pos", p), ("dist", dist));
            yield return null;
        }

        private static int StoneQuality() =>
            Player.m_localPlayer.GetInventory().GetAllItems().Where(CommandStoneItem.IsStone).Select(i => i.m_quality).DefaultIfEmpty(0).Max();

        private static IEnumerator Op(FollowerServer.Kind kind, string hid)
        {
            int quality = StoneQuality();
            FollowerServer.Send(kind, hid, quality);
            VfhLog.I(LogCat.Test, "fixture.follow_op", ("kind", kind), ("hid", hid), ("quality", quality));
            yield return new WaitForSeconds(1f); // the server's answer and the ZDO change
        }

        private static IEnumerator Stone(string[] args)
        {
            int q = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            Player p = Player.m_localPlayer;
            Inventory inv = p.GetInventory();
            foreach (ItemDrop.ItemData old in inv.GetAllItems().Where(CommandStoneItem.IsStone).ToList())
                inv.RemoveItem(old);
            ItemDrop.ItemData? stone = inv.AddItem(CommandStoneItem.PrefabName, 1, q, 0, p.GetPlayerID(), p.GetPlayerName(), false);
            if (stone == null)
                throw new InvalidOperationException("couldn't add a Command Stone (inventory full?)");
            p.EquipItem(stone);
            VfhLog.I(LogCat.Test, "fixture.stone", ("quality", q), ("equipped", p.GetRightItem() == stone));
            yield return null;
        }

        private static IEnumerator StoneMats(string[] args)
        {
            int q = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            Recipe recipe = CommandStoneItem.Recipe ?? throw new InvalidOperationException("Command Stone recipe not registered");
            Inventory inv = Player.m_localPlayer.GetInventory();
            foreach (Piece.Requirement r in recipe.m_resources)
            {
                int n = r.GetAmount(q);
                if (r.m_resItem != null && n > 0)
                    inv.AddItem(r.m_resItem.gameObject, n);
            }
            VfhLog.I(LogCat.Test, "fixture.stone_mats", ("quality", q));
            yield return null;
        }

        private static CraftingStation? Workbench() =>
            UnityEngine.Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None)
                .Where(s => s.m_name == "$piece_workbench" && Vector3.Distance(s.transform.position, Player.m_localPlayer.transform.position) < 20f)
                .OrderBy(s => Vector3.Distance(s.transform.position, Player.m_localPlayer.transform.position)).FirstOrDefault();
    }
}
