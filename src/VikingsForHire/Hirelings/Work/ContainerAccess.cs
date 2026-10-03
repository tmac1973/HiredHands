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
                cargo.RemoveItem(template.m_shared.m_name, moved);
            VfhLog.I(LogCat.Deliver, "deliver.deposit", ("hid", hid), ("chest", Utils.GetPrefabName(chest.m_rootObjectOverride != null ? chest.m_rootObjectOverride.gameObject : chest.gameObject)),
                ("pos", chest.transform.position), ("tag", chest.m_nview != null && chest.m_nview.GetZDO() != null ? chest.m_nview.GetZDO().GetString("vfh_tag") : ""), ("item", prefab), ("asked", amount), ("moved", moved));
            return moved;
        }
    }
}
