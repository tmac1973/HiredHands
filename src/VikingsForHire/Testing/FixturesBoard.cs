using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using VikingsForHire.Board;
using VikingsForHire.Commands;
using VikingsForHire.Compat;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VikingsForHire.Testing
{
    /// <summary>
    /// Phase 03 fixtures and checks. Spots are measured from where you stand: the base centre and the board spot are
    /// 6m and 5m straight ahead, so stand in open ground facing open ground.
    /// </summary>
    internal static class FixturesBoard
    {
        private const float CenterAhead = 6f;
        private const float SpotAhead = 5f;
        private const float NearbyRadius = 50f;
        private const string ProbeRecipe = "VFH_CraftyProbe";

        public static void Register()
        {
            Fixtures.Add("base", "<radius=12> <pieces=40> - workbench and bed 6m ahead, plus a block of short posts off to the left, built by you", args => Base(args, null));
            Fixtures.Add("base_partial", "<workbench|bed|pieces> - like base but missing one requirement (pieces = only 10 posts)",
                args => Base(new[] { "12", args.FirstOrDefault() == "pieces" ? "10" : "40" }, args.FirstOrDefault()));
            Fixtures.Add("board_here", "- place a hiring board 5m ahead through the real base check", _ => BoardHere());
            Fixtures.Add("charter_pack", "- deconstruct the nearest board as the hammer does after confirming: its hirelings are packed into a Hiring Charter you get", CharterPack);
            Fixtures.Add("charter_apply", "- the nearest board takes your best Hiring Charter, as when building a board while carrying it", CharterApply);
            TestHarness.RegisterCheck("charter_carries", "- hirelings in your best Hiring Charter (0 when you have none)", _ =>
                Player.m_localPlayer.GetInventory() is Inventory inv && HiringCharter.Best(inv) is ItemDrop.ItemData c ? HiringCharter.CountOf(c).ToString() : "0");
            Fixtures.Add("board_far", "<dist=45> - a second hiring board that far to your right (no base check: for tests between two boards)", args =>
            {
                Player player = RequirePlayer();
                float dist = Radius(args, 0, 45f);
                Vector3 spot = Ground(player.transform.position + Vector3.ProjectOnPlane(player.transform.right, Vector3.up).normalized * dist);
                Spawn(BoardZdo.PrefabName, spot, Quaternion.LookRotation(-player.transform.forward));
                VfhLog.I(LogCat.Test, "fixture.board_far", ("pos", spot), ("dist", dist));
                return WaitSeconds(1f);
            });
            Fixtures.Add("board_put", "<item> <n> - give yourself n items and move them into the nearest board like the UI does", BoardPut);
            Fixtures.Add("board_force_add", "<item> <n> - put items into the nearest board, skipping its food/coins filter", BoardForceAdd);
            Fixtures.Add("crafty_probe", "- add a workbench recipe 'Wood' costing 1 Coins + 1 CookedMeat (this session only)", _ => CraftyProbe());
            Fixtures.Add("clear_area", "<radius=40> - remove everything these fixtures spawned nearby, plus logs, stumps, rock chunks and items lying on the ground there", ClearArea);

            TestHarness.RegisterCheck("placement_ok", "[meters=5] - can a hiring board go this far ahead: true | false | pending",
                args => Verdict(args) is var v && v.Pending ? "pending" : v.Ok ? "true" : "false");
            TestHarness.RegisterCheck("placement_missing", "[meters=5] - unmet requirements, e.g. Workbench,Bed,Pieces (empty when ok)",
                args => Verdict(args).MissingTokens());
            TestHarness.RegisterCheck("board_count", "- hiring boards in the whole world", _ => BoardRegistry.ServerCount().ToString(), serverSide: true);
            TestHarness.RegisterCheck("boards_near", "<radius=50> - loaded hiring boards within the radius",
                args => HiringBoard.Loaded.Count(b => b != null && Vector3.Distance(b.transform.position, PlayerPos()) <= Radius(args, 0, NearbyRadius)).ToString());
            TestHarness.RegisterCheck("board_level", "- level of the nearest board", _ => NearestBoard().Level.ToString());
            TestHarness.RegisterCheck("board_storage", "<food|coins> - funds in the nearest board", args =>
            {
                Cost funds = BoardStorage.Totals(NearestBoard().Inventory ?? throw new InvalidOperationException("board has no storage"));
                return args.FirstOrDefault() == "coins" ? funds.Coins.ToString() : funds.FoodPoints.ToString();
            });
            TestHarness.RegisterCheck("board_items", "<item> - how many of an item are in the nearest board", args =>
                (NearestBoard().Inventory?.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == args[0]).Sum(i => i.m_stack) ?? 0).ToString());
            TestHarness.RegisterCheck("food_points", "<item> - food points one item is worth", args =>
            {
                ItemDrop item = ObjectDB.instance.GetItemPrefab(args[0])?.GetComponent<ItemDrop>() ?? throw new InvalidOperationException($"no item {args[0]}");
                return BoardStorage.PointsPerItem(item.m_itemData).ToString();
            });
            TestHarness.RegisterCheck("board_access", "- can you open the nearest board (ward check)",
                _ => PrivateArea.CheckAccess(NearestBoard().transform.position, 0f, flash: false) ? "true" : "false");
            TestHarness.RegisterCheck("azu_sees_board", "- is any hiring board in AzuAutoStore's container list",
                _ => ModListHasExcluded("Azumatt.AzuAutoStore", "AzuAutoStore.Util.Boxes"));
            TestHarness.RegisterCheck("crafty_sees_board", "- is any hiring board in AzuCraftyBoxes' container list",
                _ => ModListHasExcluded("Azumatt.AzuCraftyBoxes", "AzuCraftyBoxes.Util.Functions.Boxes"));
        }

        private static IEnumerator Base(string[] args, string? missing)
        {
            Player player = RequirePlayer();
            float radius = Radius(args, 0, 12f);
            int floors = args.Length > 1 && int.TryParse(args[1], out int n) ? n : 40;
            Vector3 center = Ahead(player, CenterAhead);
            Vector3 right = player.transform.right;

            if (missing != "workbench")
                Spawn("piece_workbench", Ground(center - right * 3f), player.transform.rotation);
            if (missing != "bed")
                Spawn("bed", Ground(center + right * 3f), player.transform.rotation);

            // Short posts in a tight block off to the left, 13-17 m from the centre: they count as built pieces for the
            // base check but stay out of the area where boards, chests, stations and hirelings work. (Floors laid round
            // the board followed the terrain, left steps and edges everywhere and got in the hirelings' way.)
            int placed = 0;
            for (int i = 0; placed < floors && i < 200; i++)
            {
                int col = i % 5, row = i / 5;
                Vector3 at = center - right * (13f + col) + player.transform.forward * (row - 4f);
                if (Vector3.Distance(at, center) > radius + 8f)
                    continue;
                Spawn("wood_pole", Ground(at), Quaternion.identity);
                placed++;
            }
            VfhLog.I(LogCat.Test, "fixture.base", ("center", center), ("radius", radius), ("pieces", placed), ("missing", missing ?? "none"));
            yield return null;
        }

        private static IEnumerator BoardHere()
        {
            Player player = RequirePlayer();
            Vector3 spot = Ground(Ahead(player, SpotAhead));
            // On a client the "other boards nearby" answer comes from the server, whose board list lags a few seconds
            // behind removals: a board the previous test just cleared can still count. Ask again for a while before
            // concluding a real board is too close.
            PlacementVerdict verdict = default;
            float waited = 0f;
            while (true)
            {
                // Drop the cached answer once, then wait for the server's fresh one.
                BoardRegistry.ForgetCache();
                verdict = PlacementCheck.Evaluate(spot);
                while (verdict.Pending && waited < 10f)
                {
                    yield return new WaitForSeconds(0.25f);
                    waited += 0.25f;
                    verdict = PlacementCheck.Evaluate(spot);
                }
                bool lagging = !ZNet.instance.IsServer() && verdict.MissingTokens().Contains("BoardTooClose");
                if (!lagging || waited >= 10f)
                    break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }
            if (!verdict.Ok)
            {
                VfhLog.W(LogCat.Test, "fixture.board_here_blocked", ("pos", spot), ("missing", verdict.MissingTokens()));
                VfhCommand.Print("board_here blocked: " + verdict.Message());
                string why = verdict.MissingTokens().Contains("BoardTooClose")
                    ? "another hiring board is too close: run tests 110m+ away from your own boards"
                    : "blocked: " + verdict.MissingTokens();
                TestHarness.FailSetup("board_here", why);
                yield break;
            }
            Spawn(BoardZdo.PrefabName, spot, Quaternion.LookRotation(-player.transform.forward));
            VfhLog.I(LogCat.Test, "fixture.board_here", ("pos", spot));
        }

        private static IEnumerator BoardPut(string[] args)
        {
            Player player = RequirePlayer();
            HiringBoard board = NearestBoard();
            string prefab = args.ElementAtOrDefault(0) ?? throw new ArgumentException("usage: board_put <item> <n>");
            int amount = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            Inventory playerInv = player.GetInventory();

            ItemDrop.ItemData? item = playerInv.AddItem(prefab, amount, 1, 0, 0L, "", false);
            if (item == null)
                throw new InvalidOperationException($"couldn't give {prefab}");
            ClaimBoard(board);
            Inventory boardInv = board.Inventory!;
            boardInv.MoveItemToThis(playerInv, item);
            bool stored = !playerInv.ContainsItem(item);
            if (!stored)
                playerInv.RemoveItem(item);
            VfhLog.I(LogCat.Test, "fixture.board_put", ("item", prefab), ("n", amount), ("stored", stored));
            yield return null;
        }

        private static IEnumerator BoardForceAdd(string[] args)
        {
            HiringBoard board = NearestBoard();
            string prefab = args.ElementAtOrDefault(0) ?? throw new ArgumentException("usage: board_force_add <item> <n>");
            int amount = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(prefab) ?? throw new InvalidOperationException($"no item {prefab}");
            ClaimBoard(board);
            BoardStorage.Bypass = true;
            try
            {
                board.Inventory!.AddItem(itemPrefab, amount);
            }
            finally
            {
                BoardStorage.Bypass = false;
            }
            VfhLog.I(LogCat.Test, "fixture.board_force_add", ("board", board.Id), ("item", prefab), ("n", amount));
            yield return null;
        }

        private static IEnumerator CraftyProbe()
        {
            Player player = RequirePlayer();
            if (ObjectDB.instance.m_recipes.Any(r => r != null && r.name == ProbeRecipe))
                yield break;
            ItemDrop wood = ObjectDB.instance.GetItemPrefab("Wood").GetComponent<ItemDrop>();
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = ProbeRecipe;
            recipe.m_item = wood;
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_craftingStation = ZNetScene.instance.GetPrefab("piece_workbench").GetComponent<CraftingStation>();
            recipe.m_resources = new[]
            {
                new Piece.Requirement { m_resItem = ObjectDB.instance.GetItemPrefab("Coins").GetComponent<ItemDrop>(), m_amount = 1 },
                new Piece.Requirement { m_resItem = ObjectDB.instance.GetItemPrefab("CookedMeat").GetComponent<ItemDrop>(), m_amount = 1 },
            };
            ObjectDB.instance.m_recipes.Add(recipe);
            player.m_knownRecipes.Add(wood.m_itemData.m_shared.m_name);
            VfhLog.I(LogCat.Test, "fixture.crafty_probe", ("recipe", ProbeRecipe));
        }

        private static IEnumerator ClearArea(string[] args)
        {
            Vector3 origin = PlayerPos();
            float radius = Radius(args, 0, 40f);
            var doomed = ZNetScene.instance.m_instances.Values
                .Where(v => v != null && v.GetZDO() != null && v.GetZDO().GetBool(BoardZdo.Fixture) &&
                            Vector3.Distance(v.transform.position, origin) <= radius)
                .ToList();
            foreach (ZNetView view in doomed)
            {
                view.ClaimOwnership();
                ZNetScene.instance.Destroy(view.gameObject);
            }
            // What felled trees and broken rocks leave behind (logs, stumps, rock chunks), so the next test's
            // gatherers don't wander off to leftovers from earlier runs.
            var leftovers = ZNetScene.instance.m_instances.Values
                .Where(v => v != null && v.GetZDO() != null && Vector3.Distance(v.transform.position, origin) <= radius &&
                            (v.GetComponent<TreeLog>() != null || v.GetComponent<MineRock5>() != null ||
                             (v.GetComponent<Destructible>() is Destructible d && d.m_destructibleType == DestructibleType.Tree && v.name.Contains("_Stub"))))
                .ToList();
            foreach (ZNetView view in leftovers)
            {
                view.ClaimOwnership();
                ZNetScene.instance.Destroy(view.gameObject);
            }
            // Items on the ground too (drop piles, spilled upgrade materials, dead hirelings' cargo), or they pile up
            // over runs and throw off later tests' drop-pile counts.
            var drops = ItemDrop.s_instances.Where(d => d != null && d.m_nview != null && d.m_nview.IsValid() &&
                                                        Vector3.Distance(d.transform.position, origin) <= radius).ToList();
            foreach (ItemDrop d in drops)
            {
                d.m_nview.ClaimOwnership();
                ZNetScene.instance.Destroy(d.gameObject);
            }
            VfhLog.I(LogCat.Test, "fixture.clear_area", ("radius", radius), ("removed", doomed.Count), ("leftovers", leftovers.Count), ("groundItems", drops.Count));
            yield return null;
        }

        internal static GameObject Spawn(string prefabName, Vector3 pos, Quaternion rot)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName) ?? throw new InvalidOperationException($"no prefab {prefabName}");
            GameObject go = Object.Instantiate(prefab, pos, rot);
            ZNetView view = go.GetComponent<ZNetView>();
            view.GetZDO().Set(BoardZdo.Fixture, true);
            Piece piece = go.GetComponent<Piece>();
            if (piece != null && Player.m_localPlayer != null)
            {
                // What Piece.SetCreator does, minus the platform-user index the fixtures don't need.
                long id = Player.m_localPlayer.GetPlayerID();
                piece.m_creator = id;
                view.GetZDO().Set(ZDOVars.s_creator, id);
            }
            return go;
        }

        private static void ClaimBoard(HiringBoard board)
        {
            ZNetView? view = board.GetComponent<ZNetView>();
            if (view != null && !view.IsOwner())
                view.ClaimOwnership();
        }

        private static string ModListHasExcluded(string guid, string typeName)
        {
            if (!Chainloader.PluginInfos.TryGetValue(guid, out var info) || info.Instance == null)
                return "absent";
            Type type = info.Instance.GetType().Assembly.GetType(typeName) ?? throw new InvalidOperationException($"no type {typeName}");
            FieldInfo field = type.GetField("Containers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? throw new InvalidOperationException($"{typeName} has no Containers list");
            var containers = ((IEnumerable)field.GetValue(null)).OfType<Container>();
            return containers.Any(ExcludedContainers.IsExcluded) ? "true" : "false";
        }

        private static PlacementVerdict Verdict(string[] args)
        {
            Vector3 spot = Ground(Ahead(RequirePlayer(), Radius(args, 0, SpotAhead)));
            PlacementVerdict v = PlacementCheck.Evaluate(spot);
            VfhLog.I(LogCat.Test, "placement.check", ("pos", spot), ("ok", v.Ok), ("missing", v.MissingTokens()),
                ("workbenches", v.Counts.Workbenches), ("beds", v.Counts.Beds), ("pieces", v.Counts.Pieces),
                ("nearestBoard", v.Counts.NearestBoardDistance.HasValue ? v.Counts.NearestBoardDistance.Value : "none"),
                ("worldBoards", v.Counts.WorldBoardCount), ("near", PlacementCheck.Describe(spot)));
            return v;
        }

        private static IEnumerator WaitSeconds(float s)
        {
            yield return new WaitForSeconds(s);
        }

        private static HiringBoard NearestBoard() =>
            HiringBoard.Nearest(PlayerPos(), NearbyRadius) ?? throw new InvalidOperationException("no hiring board within 50m");

        private static Player RequirePlayer() => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

        private static Vector3 PlayerPos() => RequirePlayer().transform.position;

        private static Vector3 Ahead(Player p, float meters)
        {
            Vector3 flat = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            return p.transform.position + flat * meters;
        }

        private static Vector3 Ground(Vector3 p)
        {
            p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }

        private static float Radius(string[] args, int index, float fallback) =>
            args.Length > index && float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : fallback;

        private static IEnumerator CharterPack(string[] args)
        {
            HiringBoard board = NearestBoard();
            bool done = false;
            CharterPacking.Packed? result = null;
            CharterPacking.Request(board, packed =>
            {
                result = packed;
                done = true;
            });
            float until = Time.time + 10f;
            while (!done && Time.time < until)
                yield return null;
            if (result == null)
                throw new InvalidOperationException("packing failed or timed out");
            HiringCharter.Give(board, result);
            board.GetComponent<WearNTear>()?.Remove();
            VfhLog.I(LogCat.Test, "fixture.charter_pack", ("hirelings", result.Count), ("level", result.Level));
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator CharterApply(string[] args)
        {
            HiringBoard board = NearestBoard();
            Inventory inv = Player.m_localPlayer.GetInventory();
            ItemDrop.ItemData charter = HiringCharter.Best(inv) ?? throw new InvalidOperationException("no Hiring Charter");
            bool used = HiringCharter.Apply(board, charter, inv, Player.m_localPlayer);
            VfhLog.I(LogCat.Test, "fixture.charter_apply", ("used", used), ("level", board.Level));
            yield return null;
        }
    }
}
