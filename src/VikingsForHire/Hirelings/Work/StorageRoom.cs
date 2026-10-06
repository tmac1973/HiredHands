using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Whether the chests that take an item (those already holding it: the delivery rule) have room for more. With
    /// PauseWhenStorageFull on, work that only makes more of a full item stops until there's room again, so a base
    /// doesn't fill up with piles of it.
    /// </summary>
    internal static class StorageRoom
    {
        /// <summary>
        /// True when at least one chest holds the item and, between them, they have room for less than one stack of it.
        /// An item no chest holds isn't "full": it has no home yet and goes to the board's pile, as before.
        /// </summary>
        public static bool NoRoom(IEnumerable<Container> chests, string item)
        {
            if (!VfhConfig.PauseWhenStorageFull.Value || string.IsNullOrEmpty(item) || ObjectDB.instance == null)
                return false;
            ItemDrop? drop = ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>();
            if (drop == null)
                return false;
            int stack = System.Math.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            bool home = false;
            int room = 0;
            foreach (Container c in chests)
            {
                Inventory? inv = c != null ? c.GetInventory() : null;
                if (inv == null)
                    continue;
                List<ItemDrop.ItemData> held = inv.GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == item).ToList();
                if (held.Count == 0)
                    continue;
                home = true;
                room += held.Sum(i => System.Math.Max(0, i.m_shared.m_maxStackSize - i.m_stack)) + inv.GetEmptySlots() * stack;
                if (room >= stack)
                    return false;
            }
            return home;
        }
    }
}
