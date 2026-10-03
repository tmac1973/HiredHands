using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Where hirelings leave items when there's nowhere better: just in front of their board. Each drop is tagged so
    /// gatherers never pick the pile back up (AzuAutoStore may still sort it into chests, which is fine).
    /// </summary>
    internal static class DropPile
    {
        public const string Tag = "vfh_pile";

        public static Vector3 Position(HiringBoard board) =>
            board.transform.position + board.transform.forward * Config.VfhConfig.DropPileOffset.Value + Vector3.up * 0.5f;

        /// <summary>Drops every item in the inventory at <paramref name="at"/> and empties it. Returns how many stacks.</summary>
        public static int DropAll(Inventory inventory, Vector3 at, string reason, string hid)
        {
            var items = inventory.GetAllItems().ToList();
            foreach (ItemDrop.ItemData item in items)
            {
                Vector3 spread = new(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                ItemDrop drop = ItemDrop.DropItem(item, item.m_stack, at + spread, Quaternion.identity);
                if (drop != null)
                {
                    drop.m_itemData.m_customData[Tag] = "1";
                    drop.Save();
                }
            }
            inventory.RemoveAll();
            if (items.Count > 0)
                VfhLog.I(LogCat.Deliver, "drop_pile", ("hid", hid), ("stacks", items.Count), ("pos", at), ("reason", reason),
                    ("items", string.Join(",", items.Select(i => $"{GearApplier.Name(i)}x{i.m_stack}"))));
            return items.Count;
        }
    }
}
