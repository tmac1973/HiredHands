using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VikingsForHire.Testing
{
    /// <summary>Phase 08: trees, tagged chests and the checks the woodcutter tests read.</summary>
    internal static class FixturesWork
    {
        internal const string TagKey = "vfh_tag";

        public static void Register()
        {
            Fixtures.Add("board_level", "<1-8> - set the nearest board's level (free, through its owner)", BoardLevel);
            Fixtures.Add("trees", "<prefab> <n> <distance> [tag] - plant trees in a ring that far from the nearest board (the first gets the tag)", Trees);
            Fixtures.Add("tree_near_wall", "<distance=22> - a wall and a beech 4 m from it (tagged near_wall), that far from the board", TreeNearWall);
            Fixtures.Add("chest", "<tag> [item count]… [noazu] - a wooden chest beside the board holding those items (noazu: not registered with AzuAutoStore)", Chest);
            Fixtures.Add("room", "<tag> [item count]… - a closed 6x6 m room 12 m to the board's right with a door facing the board (tagged <tag>_door) and a chest inside (tagged <tag>)", Room);
            Fixtures.Add("deliver_now", "- tell the hireling from your last contract to deliver what it carries now", DeliverNow);
            Fixtures.Add("fill_chest", "<tag> <item> - fill every free slot of a tagged chest with full stacks", FillChest);
            Fixtures.Add("steward_chores", "<chore=on|off>… - switch chores for the Steward from your last contract (fires, beehives, stations, mills, sap, animals, repairs)", StewardChores);
            Fixtures.Add("wind_on", "- steady wind, so windmills turn (wind_off puts the weather back)", _ => WindOn());
            Fixtures.Add("wind_off", "- the weather's own wind again", _ => WindOff());
            Fixtures.Add("station_info", "<prefab> - log the nearest such station's make-up and state (as vfh_station), in step with the test", args => StationInfo(args));
            TestHarness.RegisterCheck("steward_chore", "- the chore the Steward from your last contract is doing now (Fires, Stations…), or none", _ =>
            {
                Hirelings.Hireling h = Posted();
                return h.Ai?.Steward?.Doing?.ToString() ?? "none";
            });

            TestHarness.RegisterCheck("chest", "<tag> <item|free_slots> - count of an item in a tagged chest, or its free slots", args =>
            {
                Container c = Tagged(args.ElementAtOrDefault(0) ?? "");
                Inventory inv = c.GetInventory();
                string what = args.ElementAtOrDefault(1) ?? "free_slots";
                return what == "free_slots"
                    ? (inv.GetWidth() * inv.GetHeight() - inv.NrOfItems()).ToString()
                    : inv.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == what).Sum(i => i.m_stack).ToString();
            });
            TestHarness.RegisterCheck("drop_pile", "<item> - how many of an item lie in the drop pile in front of the nearest board", args =>
            {
                HiringBoard board = Board();
                Vector3 pile = DropPile.Position(board);
                return ItemDrop.s_instances.Where(d => d != null && d.m_itemData?.m_dropPrefab != null && d.m_itemData.m_dropPrefab.name == args.ElementAtOrDefault(0) &&
                                                       Vector3.Distance(d.transform.position, pile) < 4f).Sum(d => d.m_itemData.m_stack).ToString();
            });
            TestHarness.RegisterCheck("object_alive", "<tag> - whether a tagged object still exists", args => FindTagged(args.ElementAtOrDefault(0) ?? "") != null ? "true" : "false");
            TestHarness.RegisterCheck("deposited", "<tag> <item> - how many of an item hirelings themselves put into a tagged chest since login", args =>
                Hirelings.Work.ContainerAccess.Deposited(args.ElementAtOrDefault(0) ?? "", args.ElementAtOrDefault(1) ?? "").ToString());
            TestHarness.RegisterCheck("logs_near", "<radius=60> - fallen logs within this distance of the nearest board", args =>
                Hirelings.Work.LogRegistry.Within(Board().transform.position, args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 60f).Count().ToString());
            TestHarness.RegisterCheck("door_open", "<tag> - whether a tagged door is open", args =>
            {
                GameObject door = FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged door");
                return door.GetComponent<ZNetView>().GetZDO().GetInt(ZDOVars.s_state) != 0 ? "true" : "false";
            });
            TestHarness.RegisterCheck("reservations_unique", "- no harvest target is claimed by two hirelings", _ => Reservations.AllUnique() ? "true" : "false");
        }

        internal static HiringBoard Board() =>
            HiringBoard.Nearest(Player.m_localPlayer.transform.position, 60f) ?? throw new InvalidOperationException("no hiring board within 60m");

        private static IEnumerator BoardLevel(string[] args)
        {
            BoardUpgrade.RequestSetLevel(Board(), int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture));
            yield return new WaitForSeconds(2.5f); // the board's level poll
        }

        private static IEnumerator Trees(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "Beech1";
            int n = int.Parse(args.ElementAtOrDefault(1) ?? "3", CultureInfo.InvariantCulture);
            float dist = float.Parse(args.ElementAtOrDefault(2) ?? "24", CultureInfo.InvariantCulture);
            HiringBoard board = Board();
            Vector3 away = -board.transform.forward;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, (i - (n - 1) / 2f) * 15f, 0f) * away;
                GameObject tree = Spawn(prefab, board.transform.position + dir * dist, i == 0 ? args.ElementAtOrDefault(3) ?? "" : "");
                Component? target = (Component?)tree.GetComponent<TreeBase>() ?? tree.GetComponent<Destructible>();
                if (target != null)
                {
                    bool safe = WoodcutterProfile.PlanFelling(target, out Vector3? fell, out string reason);
                    VfhLog.I(LogCat.Test, "fixture.tree", ("i", i), ("height", WoodcutterProfile.Height(target)), ("safe", safe),
                        ("fellDir", fell?.ToString() ?? "any"), ("reason", reason));
                }
            }
            VfhLog.I(LogCat.Test, "fixture.trees", ("prefab", prefab), ("n", n), ("dist", dist));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator TreeNearWall(string[] args)
        {
            float dist = float.Parse(args.ElementAtOrDefault(0) ?? "22", CultureInfo.InvariantCulture);
            HiringBoard board = Board();
            Vector3 dir = board.transform.forward;
            Vector3 wall = board.transform.position + dir * dist;
            GameObject w = Spawn("woodwall", wall, "");
            Piece piece = w.GetComponent<Piece>();
            if (piece != null && Player.m_localPlayer != null)
            {
                piece.m_creator = Player.m_localPlayer.GetPlayerID();
                w.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_creator, piece.m_creator);
            }
            Spawn("Beech1", wall + Vector3.Cross(dir, Vector3.up) * 4f, "near_wall");
            yield return new WaitForSeconds(0.5f);
        }

        private static string _chestBoard = "";
        private static int _chestCount;

        private static IEnumerator Chest(string[] args)
        {
            // "noazu": keep AzuAutoStore away, so wood it would sweep in from the ground can't stand in for a delivery.
            bool noAzu = args.Contains("noazu", StringComparer.OrdinalIgnoreCase);
            args = args.Where(a => !a.Equals("noazu", StringComparison.OrdinalIgnoreCase)).ToArray();
            string tag = args.ElementAtOrDefault(0) ?? "A";
            HiringBoard board = Board();
            // Chests go round the board in the order they're made: 6 per ring, rings 1.6 m apart.
            if (_chestBoard != board.Id)
            {
                _chestBoard = board.Id;
                _chestCount = 0;
            }
            int index = _chestCount++;
            float ring = 4f + index / 6 * 1.6f;
            Vector3 pos = board.transform.position + Quaternion.Euler(0f, 60f * (index % 6) + 30f, 0f) * board.transform.forward * ring;
            GameObject go = Spawn("piece_chest_wood", pos, tag);
            Hirelings.Work.ContainerAccess.ResetDeposited(tag); // counts start fresh for this test's chest
            Piece piece = go.GetComponent<Piece>();
            if (piece != null && Player.m_localPlayer != null)
            {
                piece.m_creator = Player.m_localPlayer.GetPlayerID();
                go.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_creator, piece.m_creator);
            }
            yield return null; // let the Container wake up
            bool azu = !noAzu && Compat.AzuAutoStoreCompat.Register(go.GetComponent<Container>());
            Inventory inv = go.GetComponent<Container>().GetInventory();
            for (int i = 1; i + 1 < args.Length; i += 2)
                AddStacks(inv, args[i], int.Parse(args[i + 1], CultureInfo.InvariantCulture));
            VfhLog.I(LogCat.Test, "fixture.chest", ("tag", tag), ("pos", pos), ("items", string.Join(" ", args.Skip(1))), ("azu", azu));
        }

        private static IEnumerator FillChest(string[] args)
        {
            Container c = Tagged(args.ElementAtOrDefault(0) ?? "");
            GameObject item = ObjectDB.instance.GetItemPrefab(args.ElementAtOrDefault(1) ?? "Wood");
            int max = item.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize;
            Inventory inv = c.GetInventory();
            int guard = 0;
            while (inv.NrOfItems() < inv.GetWidth() * inv.GetHeight() && guard++ < 64)
                inv.AddItem(item, max);
            foreach (ItemDrop.ItemData i in inv.GetAllItems().Where(i => i.m_dropPrefab == item))
                i.m_stack = max;
            inv.Changed();
            yield return null;
        }

        private static IEnumerator Room(string[] args)
        {
            string tag = args.ElementAtOrDefault(0) ?? "R";
            HiringBoard board = Board();
            Vector3 n = -board.transform.right; // from the room towards the board
            Vector3 t = board.transform.forward;
            Vector3 c = board.transform.position + board.transform.right * 12f;
            // Three 2 m walls per side; the middle of the side facing the board is the door.
            var parts = new System.Collections.Generic.List<(string Prefab, Vector3 Pos, Vector3 Facing, string Tag)>();
            for (int i = -1; i <= 1; i++)
            {
                parts.Add((i == 0 ? "wood_door" : "woodwall", c + n * 3f + t * (2f * i), n, i == 0 ? tag + "_door" : ""));
                parts.Add(("woodwall", c - n * 3f + t * (2f * i), n, ""));
                parts.Add(("woodwall", c + t * 3f + n * (2f * i), t, ""));
                parts.Add(("woodwall", c - t * 3f + n * (2f * i), t, ""));
            }
            foreach (var p in parts)
            {
                GameObject go = Spawn(p.Prefab, p.Pos, p.Tag);
                go.transform.rotation = Quaternion.LookRotation(p.Facing);
                OwnBuilt(go);
                if (go.GetComponent<WearNTear>() is WearNTear wnt)
                    wnt.m_noSupportWear = false; // test walls on uneven ground mustn't collapse
            }
            GameObject chest = Spawn("piece_chest_wood", c, tag);
            OwnBuilt(chest);
            yield return null;
            Inventory inv = chest.GetComponent<Container>().GetInventory();
            for (int i = 1; i + 1 < args.Length; i += 2)
                AddStacks(inv, args[i], int.Parse(args[i + 1], CultureInfo.InvariantCulture));
            // Not registered with AzuAutoStore: it would fill the chest from the ground and fake a delivery.
            bool azu = false;
            VfhLog.I(LogCat.Test, "fixture.room", ("tag", tag), ("center", c), ("items", string.Join(" ", args.Skip(1))), ("azu", azu));
            yield return new WaitForSeconds(0.5f);
        }

        // One AddItem call is capped at a single stack (50 wood), so add stack by stack.
        internal static void AddStacks(Inventory inv, string prefab, int amount)
        {
            GameObject item = ObjectDB.instance.GetItemPrefab(prefab) ?? throw new ArgumentException($"no item {prefab}");
            int max = Math.Max(1, item.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize);
            for (int left = amount; left > 0; left -= max)
                inv.AddItem(item, Math.Min(left, max));
        }

        internal static void OwnBuilt(GameObject go)
        {
            Piece piece = go.GetComponent<Piece>();
            if (piece != null && Player.m_localPlayer != null)
            {
                piece.m_creator = Player.m_localPlayer.GetPlayerID();
                go.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_creator, piece.m_creator);
            }
        }

        private static IEnumerator DeliverNow(string[] args)
        {
            string hid = BoardContracts.LastPostedHid;
            Hirelings.Hireling h = Hirelings.Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == hid) ?? throw new InvalidOperationException("the posted hireling isn't loaded");
            h.Zdo!.Set(Hirelings.HirelingZdo.DeliverPending, true);
            VfhLog.I(LogCat.Test, "fixture.deliver_now", ("hid", hid));
            yield return null;
        }

        private static Hirelings.Hireling Posted()
        {
            string hid = BoardContracts.LastPostedHid;
            return Hirelings.Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == hid) ?? throw new InvalidOperationException("the posted hireling isn't loaded");
        }

        // On the contract, so it works before the Steward has even arrived (the arrival copies it onto the hireling).
        private static IEnumerator StewardChores(string[] args)
        {
            string hid = BoardContracts.LastPostedHid;
            HiringBoard board = Board();
            Core.ContractEntry entry = BoardRosterOps.Read(board.Zdo!).ByHid(hid) ?? throw new InvalidOperationException("no contract posted");
            string skip = entry.SkipItems;
            foreach (string a in args)
            {
                string[] kv = a.Split('=');
                if (kv.Length != 2 || !Core.Chores.ChoreKeys.TryParse(kv[0], out Core.Chores.ChoreKind kind))
                    throw new InvalidOperationException($"not chore=on|off: {a}");
                skip = Core.Chores.ChoreRules.WithChore(skip, kind, kv[1] == "on");
            }
            Net.MutationService.SubmitBoard(board.Id, new Core.RosterOp { Type = Core.RosterOpType.SetGather, Hid = hid, SkipItems = skip, NoHomeWork = entry.NoHomeWork });
            VfhLog.I(LogCat.Test, "fixture.steward_chores", ("hid", hid), ("skip", skip));
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator WindOn()
        {
            EnvMan.instance.SetDebugWind(0f, 1f);
            VfhLog.I(LogCat.Test, "fixture.wind_on");
            yield return null;
        }

        private static IEnumerator StationInfo(string[] args)
        {
            Commands.NavCommands.Station(args);
            yield return null;
        }

        private static IEnumerator WindOff()
        {
            EnvMan.instance.ResetDebugWind();
            yield return null;
        }

        internal static GameObject Spawn(string prefab, Vector3 pos, string tag)
        {
            pos.y = ZoneSystem.instance.GetGroundHeight(pos);
            GameObject go = Object.Instantiate(ZNetScene.instance.GetPrefab(prefab) ?? throw new ArgumentException($"no prefab {prefab}"), pos, Quaternion.identity);
            ZDO zdo = go.GetComponent<ZNetView>().GetZDO();
            zdo.Set(BoardZdo.Fixture, true);
            if (tag.Length > 0)
                zdo.Set(TagKey, tag);
            return go;
        }

        internal static GameObject? FindTagged(string tag) =>
            ZNetScene.instance.m_instances.Values.FirstOrDefault(v => v != null && v.GetZDO() != null && v.GetZDO().GetString(TagKey) == tag)?.gameObject;

        private static Container Tagged(string tag) =>
            FindTagged(tag)?.GetComponent<Container>() ?? throw new InvalidOperationException($"no tagged chest {tag}");
    }
}
