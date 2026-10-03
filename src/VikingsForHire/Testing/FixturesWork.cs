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
        private const string TagKey = "vfh_tag";

        public static void Register()
        {
            Fixtures.Add("board_level", "<1-8> - set the nearest board's level (free, through its owner)", BoardLevel);
            Fixtures.Add("trees", "<prefab> <n> <distance> - plant trees in a ring that far from the nearest board", Trees);
            Fixtures.Add("tree_near_wall", "<distance=22> - a wall and a beech 4 m from it (tagged near_wall), that far from the board", TreeNearWall);
            Fixtures.Add("chest", "<tag> [item count]… - a wooden chest beside the board holding those items", Chest);
            Fixtures.Add("deliver_now", "- tell the hireling from your last contract to deliver what it carries now", DeliverNow);
            Fixtures.Add("fill_chest", "<tag> <item> - fill every free slot of a tagged chest with full stacks", FillChest);

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
            TestHarness.RegisterCheck("reservations_unique", "- no harvest target is claimed by two hirelings", _ => Reservations.AllUnique() ? "true" : "false");
        }

        private static HiringBoard Board() =>
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
                GameObject tree = Spawn(prefab, board.transform.position + dir * dist, "");
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

        private static IEnumerator Chest(string[] args)
        {
            string tag = args.ElementAtOrDefault(0) ?? "A";
            HiringBoard board = Board();
            int index = Math.Abs(tag.GetHashCode()) % 6;
            Vector3 pos = board.transform.position + Quaternion.Euler(0f, 60f * index + 30f, 0f) * board.transform.forward * 4f;
            GameObject go = Spawn("piece_chest_wood", pos, tag);
            Piece piece = go.GetComponent<Piece>();
            if (piece != null && Player.m_localPlayer != null)
            {
                piece.m_creator = Player.m_localPlayer.GetPlayerID();
                go.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_creator, piece.m_creator);
            }
            yield return null; // let the Container wake up
            Inventory inv = go.GetComponent<Container>().GetInventory();
            for (int i = 1; i + 1 < args.Length; i += 2)
                inv.AddItem(ObjectDB.instance.GetItemPrefab(args[i]), int.Parse(args[i + 1], CultureInfo.InvariantCulture));
            VfhLog.I(LogCat.Test, "fixture.chest", ("tag", tag), ("pos", pos), ("items", string.Join(" ", args.Skip(1))));
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

        private static IEnumerator DeliverNow(string[] args)
        {
            string hid = BoardContracts.LastPostedHid;
            Hirelings.Hireling h = Hirelings.Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == hid) ?? throw new InvalidOperationException("the posted hireling isn't loaded");
            h.Zdo!.Set(Hirelings.HirelingZdo.DeliverPending, true);
            VfhLog.I(LogCat.Test, "fixture.deliver_now", ("hid", hid));
            yield return null;
        }

        private static GameObject Spawn(string prefab, Vector3 pos, string tag)
        {
            pos.y = ZoneSystem.instance.GetGroundHeight(pos);
            GameObject go = Object.Instantiate(ZNetScene.instance.GetPrefab(prefab) ?? throw new ArgumentException($"no prefab {prefab}"), pos, Quaternion.identity);
            ZDO zdo = go.GetComponent<ZNetView>().GetZDO();
            zdo.Set(BoardZdo.Fixture, true);
            if (tag.Length > 0)
                zdo.Set(TagKey, tag);
            return go;
        }

        private static GameObject? FindTagged(string tag) =>
            ZNetScene.instance.m_instances.Values.FirstOrDefault(v => v != null && v.GetZDO() != null && v.GetZDO().GetString(TagKey) == tag)?.gameObject;

        private static Container Tagged(string tag) =>
            FindTagged(tag)?.GetComponent<Container>() ?? throw new InvalidOperationException($"no tagged chest {tag}");
    }
}
