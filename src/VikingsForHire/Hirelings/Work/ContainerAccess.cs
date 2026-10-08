using System;
using System.Linq;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Moving items between a hireling's cargo and a chest the way PullMats does: take ownership of the chest when it's
    /// not open, change its inventory, and let the Container save itself. Only moves what actually fits right now.
    /// </summary>
    internal static class ContainerAccess
    {
        private static readonly System.Collections.Generic.Dictionary<string, int> DepositedByTag = new();

        /// <summary>Tests: forget what was delivered to chests with this tag (a new test's chest starts at zero).</summary>
        public static void ResetDeposited(string tag)
        {
            foreach (string key in DepositedByTag.Keys.Where(k => k.StartsWith(tag + "|")).ToList())
                DepositedByTag.Remove(key);
        }

        /// <summary>How many of an item hirelings have put into the chest with this test tag since login (for tests).</summary>
        public static int Deposited(string tag, string prefab) => DepositedByTag.TryGetValue(tag + "|" + prefab, out int n) ? n : 0;

        /// <summary>Moves up to <paramref name="amount"/> of a prefab from cargo into the chest; returns how many moved.</summary>
        public static int Deposit(Container chest, Inventory cargo, string prefab, int amount, string hid)
        {
            if (chest == null || chest.IsInUse())
                return 0;
            ZNetView? view = chest.m_nview;
            if (view == null || !view.IsValid())
                return 0;
            if (!view.IsOwner())
                view.ClaimOwnership();

            GameObject? itemPrefab = ObjectDB.instance.GetItemPrefab(prefab);
            if (itemPrefab == null)
                return 0;
            ItemDrop.ItemData template = itemPrefab.GetComponent<ItemDrop>().m_itemData;
            int max = Math.Max(1, template.m_shared.m_maxStackSize);
            Inventory inv = chest.GetInventory();
            int carried = cargo.GetAllItems().Where(i => i.m_dropPrefab == itemPrefab).Sum(i => i.m_stack);
            int room = inv.GetAllItems().Where(i => i.m_dropPrefab == itemPrefab).Sum(i => Math.Max(0, max - i.m_stack))
                       + (inv.GetWidth() * inv.GetHeight() - inv.NrOfItems()) * max;
            int move = Math.Min(amount, Math.Min(carried, room));
            int left = move;
            while (left > 0)
            {
                int n = Math.Min(left, max);
                if (!inv.AddItem(itemPrefab, n))
                    break;
                left -= n;
            }
            int moved = move - left;
            if (moved > 0)
            {
                cargo.RemoveItem(template.m_shared.m_name, moved);
                string tagged = view.GetZDO()?.GetString("vfh_tag") ?? "";
                if (tagged.Length > 0)
                    DepositedByTag[tagged + "|" + prefab] = Deposited(tagged, prefab) + moved;
            }
            if (moved > 0 && Telemetry.BalanceLog.On && Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == hid) is Hireling who)
                Telemetry.BalanceLog.Record("deliver", ("hid", Telemetry.BalanceFights.Short(hid)), ("job", who.Job), ("lvl", who.Level),
                    ("item", prefab), ("n", moved), ("biome", Telemetry.BalanceFights.Biome(who.transform.position)));
            VfhLog.I(LogCat.Deliver, "deliver.deposit", ("hid", hid), ("chest", Utils.GetPrefabName(chest.m_rootObjectOverride != null ? chest.m_rootObjectOverride.gameObject : chest.gameObject)),
                ("pos", chest.transform.position), ("tag", chest.m_nview != null && chest.m_nview.GetZDO() != null ? chest.m_nview.GetZDO().GetString("vfh_tag") : ""), ("item", prefab), ("asked", amount), ("moved", moved));
            return moved;
        }

        /// <summary>
        /// Moves up to <paramref name="amount"/> of a prefab from the chest into cargo, never leaving fewer than
        /// <paramref name="keepMin"/> in the chest and only what fits in the hireling's usable cargo slots.
        /// </summary>
        public static int Take(Container chest, Inventory cargo, string prefab, int amount, int keepMin, string hid)
        {
            if (chest == null || chest.IsInUse() || amount <= 0)
                return 0;
            ZNetView? view = chest.m_nview;
            if (view == null || !view.IsValid())
                return 0;
            GameObject? itemPrefab = ObjectDB.instance.GetItemPrefab(prefab);
            if (itemPrefab == null)
                return 0;
            if (!view.IsOwner())
                view.ClaimOwnership();

            ItemDrop.ItemData template = itemPrefab.GetComponent<ItemDrop>().m_itemData;
            int max = Math.Max(1, template.m_shared.m_maxStackSize);
            Inventory inv = chest.GetInventory();
            int inChest = inv.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab).Sum(i => i.m_stack);
            int left = Math.Min(amount, Math.Max(0, inChest - keepMin));
            int moved = 0;
            while (left > 0)
            {
                int n = Math.Min(left, max);
                int before = cargo.CountItems(template.m_shared.m_name);
                cargo.AddItem(itemPrefab, n);
                int added = cargo.CountItems(template.m_shared.m_name) - before;
                if (added <= 0)
                    break;
                inv.RemoveItem(template.m_shared.m_name, added);
                moved += added;
                left -= added;
                if (added < n)
                    break; // cargo is full
            }
            VfhLog.I(LogCat.Smelter, "smelter.take", ("hid", hid), ("chest", Utils.GetPrefabName(chest.m_rootObjectOverride != null ? chest.m_rootObjectOverride.gameObject : chest.gameObject)),
                ("pos", chest.transform.position), ("tag", view.GetZDO()?.GetString("vfh_tag") ?? ""), ("item", prefab), ("asked", amount), ("moved", moved), ("keepMin", keepMin));
            return moved;
        }
    }
}
