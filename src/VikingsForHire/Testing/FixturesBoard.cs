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
            Fixtures.Add("base", "<radius=12> <floors=40> - workbench, bed and wood floors 6m ahead, built by you", args => Base(args, null));
            Fixtures.Add("base_partial", "<workbench|bed|pieces> - like base but missing one requirement (pieces = only 10 floors)",
                args => Base(new[] { "12", args.FirstOrDefault() == "pieces" ? "10" : "40" }, args.FirstOrDefault()));
            Fixtures.Add("board_here", "- place a hiring board 5m ahead through the real base check", _ => BoardHere());
            Fixtures.Add("board_put", "<item> <n> - give yourself n items and move them into the nearest board like the UI does", BoardPut);
            Fixtures.Add("board_force_add", "<item> <n> - put items into the nearest board, skipping its food/coins filter", BoardForceAdd);
            Fixtures.Add("crafty_probe", "- add a workbench recipe 'Wood' costing 1 Coins + 1 CookedMeat (this session only)", _ => CraftyProbe());
            Fixtures.Add("clear_area", "<radius=40> - remove everything these fixtures spawned nearby", ClearArea);

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

            // Floors on a 2m grid, ring by ring outward from 4m, so they stay inside the radius and clear of the bench and bed.
            int placed = 0;
            for (int ring = 2; placed < floors && ring * 2f <= radius; ring++)
            {
                for (int x = -ring; x <= ring && placed < floors; x++)
                for (int z = -ring; z <= ring && placed < floors; z++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(z)) != ring)
                        continue;
                    Spawn("wood_floor", Ground(center + new Vector3(x * 2f, 0f, z * 2f)), Quaternion.identity);
                    placed++;
                }
            }
            VfhLog.I(LogCat.Test, "fixture.base", ("center", center), ("radius", radius), ("floors", placed), ("missing", missing ?? "none"));
            yield return null;
        }

        private static IEnumerator BoardHere()
        {
            Player player = RequirePlayer();
            Vector3 spot = Ground(Ahead(player, SpotAhead));
            PlacementVerdict verdict = PlacementCheck.Evaluate(spot);
            for (float waited = 0f; verdict.Pending && waited < 5f; waited += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                verdict = PlacementCheck.Evaluate(spot);
            }
            if (!verdict.Ok)
            {
                VfhLog.W(LogCat.Test, "fixture.board_here_blocked", ("pos", spot), ("missing", verdict.MissingTokens()));
                VfhCommand.Print("board_here blocked: " + verdict.Message());
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
            VfhLog.I(LogCat.Test, "fixture.clear_area", ("radius", radius), ("removed", doomed.Count));
            yield return null;
        }

        private static GameObject Spawn(string prefabName, Vector3 pos, Quaternion rot)
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

        private static PlacementVerdict Verdict(string[] args) => PlacementCheck.Evaluate(Ground(Ahead(RequirePlayer(), Radius(args, 0, SpotAhead))));

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
    }
}
