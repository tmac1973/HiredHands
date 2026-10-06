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
            Fixtures.Add("fire", "<prefab> <fuel> [tag] - a fire (hearth, fire_pit, piece_groundtorch_wood…) in a ring 7 m from the board with that much fuel", Fire);
            Fixtures.Add("torches_eternal", "<on|off> - switch Torches Eternal's keep-every-fire-full patch on or off (if it's installed), so fire tests can run with it", TorchesEternal);
            TestHarness.RegisterCheck("fire_fuel", "<tag> - a tagged fire's fuel as a fraction of full (0..1)", args =>
            {
                Fireplace f = FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponentInChildren<Fireplace>() ?? throw new InvalidOperationException("no such tagged fire");
                return (f.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel) / f.m_maxFuel).ToString("0.00", CultureInfo.InvariantCulture);
            });
            TestHarness.RegisterCheck("fuel_added", "<tag> - fuel items Stewards have put into a tagged fire", args =>
            {
                Fireplace f = FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponentInChildren<Fireplace>() ?? throw new InvalidOperationException("no such tagged fire");
                return (Hirelings.Work.Steward.FiresChore.FuelAdded.TryGetValue(f.GetInstanceID(), out int n) ? n : 0).ToString(CultureInfo.InvariantCulture);
            });
            Fixtures.Add("producer", "<piece_beehive|piece_sapcollector> <level|full> [tag] - a beehive or sap collector in a ring 7 m from the board, already that full", Producer);
            TestHarness.RegisterCheck("producer_level", "<tag> - how full a tagged beehive or sap collector is", args =>
            {
                GameObject go = FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged producer");
                return go.GetComponent<ZNetView>().GetZDO().GetInt(ZDOVars.s_level).ToString(CultureInfo.InvariantCulture);
            });
            Fixtures.Add("tame", "<prefab> [tag] [distance=20] - a tamed, hungry animal (Boar, Wolf…) that far from the board", Tame);
            TestHarness.RegisterCheck("animal_hungry", "<tag> - whether a tagged tamed animal is hungry", args =>
                (FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponent<Tameable>() ?? throw new InvalidOperationException("no such tagged animal")).IsHungry() ? "true" : "false");
            TestHarness.RegisterCheck("fed_by_steward", "<tag> - food items Stewards have dropped for a tagged animal", args =>
            {
                GameObject go = FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged animal");
                Character c = go.GetComponent<Character>();
                return (Hirelings.Work.Steward.AnimalsChore.Fed.TryGetValue(c.GetInstanceID(), out int n) ? n : 0).ToString(CultureInfo.InvariantCulture);
            });
            Fixtures.Add("damaged", "<prefab> <health 0..1> [tag] [near|far] [high:<m>] - a damaged piece 6 m from the board (near: within the base's workbench range) or 28 m out (far: beyond a workbench's 20 m range), optionally floating that high off the ground", Damaged);
            Fixtures.Add("set_health", "<tag> <health 0..1> - set a tagged piece's health", SetHealth);
            TestHarness.RegisterCheck("piece_health", "<tag> - a tagged piece's health (0..1)", args =>
                (FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponent<WearNTear>() ?? throw new InvalidOperationException("no such tagged piece"))
                    .GetHealthPercentage().ToString("0.00", CultureInfo.InvariantCulture));
            Fixtures.Add("fermenter", "<empty|ready> [tag] - a fermenter 7 m from the board (put a roof over it with roof_over fermenter); ready: holding a finished mead base", Fermenter);
            Fixtures.Add("shieldgen", "<fuel> [tag] - a shield generator 7 m from the board with that much fuel", ShieldGen);
            Fixtures.Add("litter", "<item> <count> [tag] - drop items 10 m from the board, already on the ground long enough to be tidied", Litter);
            TestHarness.RegisterCheck("fermenter_status", "<tag> - Empty, Fermenting, Exposed or Ready", args =>
                (FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponentInChildren<global::Fermenter>() ?? throw new InvalidOperationException("no such tagged fermenter")).GetStatus().ToString());
            TestHarness.RegisterCheck("fermenter_cover", "<tag> - a tagged fermenter's cover (0..1, it needs 0.7) and whether it has a roof, as the game measures them", args =>
            {
                global::Fermenter f = FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponentInChildren<global::Fermenter>() ?? throw new InvalidOperationException("no such tagged fermenter");
                Cover.GetCoverForPoint(f.m_roofCheckPoint.position, out float cover, out bool roof);
                return $"{cover.ToString("0.00", CultureInfo.InvariantCulture)} roof={roof} exposed={f.m_exposed} hasRoof={f.m_hasRoof}";
            });
            TestHarness.RegisterCheck("shield_fuel", "<tag> - a tagged shield generator's fuel", args =>
                (FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponentInChildren<ShieldGenerator>() ?? throw new InvalidOperationException("no such tagged generator"))
                    .GetFuel().ToString("0.0", CultureInfo.InvariantCulture));
            TestHarness.RegisterCheck("ground_items", "<item> [radius=40] - how many of an item lie on the ground within that distance of the nearest board", args =>
            {
                string item = args.ElementAtOrDefault(0) ?? "Wood";
                float radius = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 40f;
                Vector3 at = Board().transform.position;
                return ItemDrop.s_instances.Where(d => d != null && d.m_itemData?.m_dropPrefab != null && d.m_itemData.m_dropPrefab.name == item &&
                                                      Utils.DistanceXZ(d.transform.position, at) <= radius).Sum(d => d.m_itemData.m_stack).ToString(CultureInfo.InvariantCulture);
            });
            Fixtures.Add("station_info", "<prefab> - log the nearest such station's make-up and state (as vfh_station), in step with the test", args => StationInfo(args));
            TestHarness.RegisterCheck("crop_known", "<item> - true when a crop (planted or regrowing) yields this item", args =>
                Hirelings.Work.Farm.CropCatalog.All.Any(c => c.Info.Yields == (args.ElementAtOrDefault(0) ?? "")).ToString().ToLowerInvariant());
            TestHarness.RegisterCheck("recipe_known", "<item> - true when a Cook knows a way to make this item", args =>
                Hirelings.Work.Kitchen.KitchenCatalog.All.Any(k => k.Output == (args.ElementAtOrDefault(0) ?? "")).ToString().ToLowerInvariant());
            TestHarness.RegisterCheck("stock", "<item> [chests|growing] - the nearest board's stock of an item as orders count it (chests above reserves + growing)", args =>
            {
                HiringBoard board = Board();
                Hirelings.Work.Stock s = Hirelings.Work.StockCounter.Count(board.transform.position, new Core.LevelRules(Config.DataStore.Current).MaxWorkRadius(board.Level));
                string item = args.ElementAtOrDefault(0) ?? "";
                int n = args.ElementAtOrDefault(1) switch
                {
                    "chests" => s.Chests.TryGetValue(item, out int c) ? c : 0,
                    "growing" => s.Growing.TryGetValue(item, out int g) ? g : 0,
                    _ => s.Have(item),
                };
                return n.ToString(CultureInfo.InvariantCulture);
            });
            TestHarness.RegisterCheck("steward_chore", "- the chore the Steward from your last contract is doing now (Fires, Stations…), or none", _ =>
            {
                Hirelings.Hireling h = Posted();
                return h.Ai?.Chores?.Doing?.ToString() ?? "none";
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

        private static IEnumerator Producer(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "piece_beehive";
            string levelArg = args.ElementAtOrDefault(1) ?? "full";
            string tag = args.ElementAtOrDefault(2) ?? "";
            HiringBoard board = Board();
            Vector3 pos = board.transform.position + Quaternion.Euler(0f, 140f + 50f * (_fires++ % 6), 0f) * board.transform.forward * 7f;
            GameObject go = Spawn(prefab, pos, tag);
            OwnBuilt(go);
            yield return null;
            // "full": as full as this producer gets (beehives and sap collectors hold different amounts).
            int max = go.GetComponentInChildren<Beehive>()?.m_maxHoney ?? go.GetComponentInChildren<SapCollector>()?.m_maxLevel ?? 4;
            int level = levelArg == "full" ? max : int.Parse(levelArg, CultureInfo.InvariantCulture);
            go.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_level, level);
            VfhLog.I(LogCat.Test, "fixture.producer", ("prefab", prefab), ("level", level), ("max", max), ("tag", tag));
            yield return null;
        }

        private static IEnumerator Tame(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "Boar";
            string tag = args.ElementAtOrDefault(1) ?? "";
            float distance = args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 20f;
            HiringBoard board = Board();
            Vector3 pos = board.transform.position - board.transform.forward * distance;
            GameObject go = Spawn(prefab, pos, tag);
            yield return null;
            Character c = go.GetComponent<Character>() ?? throw new InvalidOperationException($"{prefab} isn't a creature");
            c.SetTamed(true);
            go.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_tameLastFeeding, 0L);
            VfhLog.I(LogCat.Test, "fixture.tame", ("prefab", prefab), ("tag", tag), ("hungry", go.GetComponent<Tameable>()?.IsHungry() ?? false));
            yield return null;
        }

        private static IEnumerator Damaged(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "woodwall";
            float health = float.Parse(args.ElementAtOrDefault(1) ?? "0.3", CultureInfo.InvariantCulture);
            string tag = args.ElementAtOrDefault(2) ?? "";
            bool far = args.ElementAtOrDefault(3) == "far";
            string? high = args.Skip(3).FirstOrDefault(a => a.StartsWith("high:"));
            float up = high != null ? float.Parse(high.Substring(5), CultureInfo.InvariantCulture) : 0f;
            HiringBoard board = Board();
            Vector3 pos = board.transform.position + board.transform.forward * (far ? 28f : 6f) + board.transform.right * (_fires++ % 3 - 1) * 2.5f;
            pos.y = ZoneSystem.instance.GetGroundHeight(pos) + up;
            GameObject go = Spawn(prefab, pos, tag);
            if (up > 0f)
            {
                // Spawn puts it on the ground; lift it (and its saved position).
                go.transform.position = pos;
                go.GetComponent<ZNetView>().GetZDO().SetPosition(pos);
            }
            OwnBuilt(go);
            if (go.GetComponent<WearNTear>() is WearNTear wnt)
                wnt.m_noSupportWear = false;
            yield return null;
            SetHealthOf(go, health);
            VfhLog.I(LogCat.Test, "fixture.damaged", ("prefab", prefab), ("health", health), ("tag", tag), ("far", far), ("up", up));
            yield return null;
        }

        private static IEnumerator SetHealth(string[] args)
        {
            GameObject go = FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged piece");
            SetHealthOf(go, float.Parse(args.ElementAtOrDefault(1) ?? "0.3", CultureInfo.InvariantCulture));
            yield return null;
        }

        private static void SetHealthOf(GameObject go, float fraction)
        {
            WearNTear wnt = go.GetComponent<WearNTear>() ?? throw new InvalidOperationException("not a building piece");
            float hp = wnt.m_health * fraction;
            wnt.m_nview.GetZDO().Set(ZDOVars.s_health, hp);
            // The piece caches its health fraction; this is how the game tells every copy of it.
            wnt.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_HealthChanged", hp);
        }

        private static IEnumerator Fermenter(string[] args)
        {
            bool ready = args.ElementAtOrDefault(0) == "ready";
            string tag = args.ElementAtOrDefault(1) ?? "";
            HiringBoard board = Board();
            Vector3 pos = board.transform.position + Quaternion.Euler(0f, 100f + 50f * (_fires++ % 6), 0f) * board.transform.forward * 7f;
            GameObject go = Spawn("fermenter", pos, tag);
            OwnBuilt(go);
            yield return null;
            global::Fermenter f = go.GetComponentInChildren<global::Fermenter>();
            if (ready)
            {
                // The minor healing mead base if this fermenter takes it (the tests seed a chest for its mead), else the first.
                string mead = f.m_conversion.Where(c => c.m_from != null).Select(c => c.m_from.gameObject.name)
                    .OrderBy(m => m == "MeadBaseHealthMinor" ? 0 : 1).First();
                ZDO z = f.m_nview.GetZDO();
                z.Set(ZDOVars.s_content, mead.GetStableHashCode());
                z.Set(ZDOVars.s_startTime, ZNet.instance.GetTime().AddSeconds(-f.m_fermentationDuration - 30.0).Ticks);
            }
            VfhLog.I(LogCat.Test, "fixture.fermenter", ("ready", ready), ("tag", tag));
            yield return null;
        }

        private static IEnumerator ShieldGen(string[] args)
        {
            float fuel = float.Parse(args.ElementAtOrDefault(0) ?? "0", CultureInfo.InvariantCulture);
            string tag = args.ElementAtOrDefault(1) ?? "";
            HiringBoard board = Board();
            Vector3 pos = board.transform.position + Quaternion.Euler(0f, 100f + 50f * (_fires++ % 6), 0f) * board.transform.forward * 7f;
            GameObject go = Spawn("piece_shieldgenerator", pos, tag);
            OwnBuilt(go);
            yield return null;
            go.GetComponentInChildren<ShieldGenerator>().m_nview.GetZDO().Set(ZDOVars.s_fuel, fuel);
            VfhLog.I(LogCat.Test, "fixture.shieldgen", ("fuel", fuel), ("tag", tag));
            yield return null;
        }

        private static IEnumerator Litter(string[] args)
        {
            string item = args.ElementAtOrDefault(0) ?? "Wood";
            int count = int.Parse(args.ElementAtOrDefault(1) ?? "10", CultureInfo.InvariantCulture);
            HiringBoard board = Board();
            GameObject prefab = ObjectDB.instance.GetItemPrefab(item) ?? throw new InvalidOperationException($"no item {item}");
            Vector3 at = board.transform.position - board.transform.right * 10f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = at + new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
                p.y = ZoneSystem.instance.GetGroundHeight(p) + 0.3f;
                GameObject go = Object.Instantiate(prefab, p, Quaternion.identity);
                ZDO z = go.GetComponent<ZNetView>().GetZDO();
                z.Set(BoardZdo.Fixture, true);
                // Long enough on the ground to be tidied.
                z.Set(ZDOVars.s_spawnTime, ZNet.instance.GetTime().AddSeconds(-600.0).Ticks);
            }
            VfhLog.I(LogCat.Test, "fixture.litter", ("item", item), ("count", count));
            yield return null;
        }

        private static int _fires;

        private static IEnumerator Fire(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "hearth";
            float fuel = float.Parse(args.ElementAtOrDefault(1) ?? "0", CultureInfo.InvariantCulture);
            string tag = args.ElementAtOrDefault(2) ?? "";
            HiringBoard board = Board();
            Vector3 pos = board.transform.position + Quaternion.Euler(0f, 200f + 50f * (_fires++ % 6), 0f) * board.transform.forward * 7f;
            GameObject go = Spawn(prefab, pos, tag);
            OwnBuilt(go);
            yield return null;
            Fireplace f = go.GetComponentInChildren<Fireplace>() ?? throw new InvalidOperationException($"{prefab} isn't a fire");
            f.m_nview.GetZDO().Set(ZDOVars.s_fuel, fuel);
            VfhLog.I(LogCat.Test, "fixture.fire", ("prefab", prefab), ("fuel", fuel), ("max", f.m_maxFuel), ("tag", tag));
            yield return null;
        }

        // Torches Eternal keeps every fire full each frame (a Harmony prefix on Fireplace.UpdateFireplace): take it off to
        // test fuelling, put it back after.
        private static IEnumerator TorchesEternal(string[] args)
        {
            const string id = "Xenofell.TorchesEternal";
            bool on = args.ElementAtOrDefault(0) != "off";
            System.Reflection.MethodInfo target = HarmonyLib.AccessTools.Method(typeof(Fireplace), "UpdateFireplace");
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(id, out var info))
            {
                VfhLog.I(LogCat.Test, "fixture.torches_eternal", ("installed", false));
                yield break;
            }
            var harmony = new HarmonyLib.Harmony(id);
            harmony.Unpatch(target, HarmonyLib.HarmonyPatchType.Prefix, id);
            if (on)
            {
                System.Reflection.MethodInfo? prefix = info.Instance.GetType().Assembly.GetType("TorchesEternal.Patches")?.GetMethod("Fireplace_UpdateFireplace");
                if (prefix != null)
                    harmony.Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
            }
            VfhLog.I(LogCat.Test, "fixture.torches_eternal", ("installed", true), ("on", on));
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
