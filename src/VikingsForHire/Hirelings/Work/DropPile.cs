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

        private static readonly int WallMask = LayerMask.GetMask("piece", "Default", "static_solid");
        private static readonly System.Collections.Generic.Dictionary<HiringBoard, (float At, Vector3 Spot)> Spots = new();

        /// <summary>
        /// A spot beside the board with nothing built between it and the board: in front first, then behind, then either
        /// side. Svanhild's board stands by a wall, its front towards it, and she tried to leave the food on the far side.
        /// </summary>
        public static Vector3 Position(HiringBoard board)
        {
            if (Spots.TryGetValue(board, out var hit) && Time.time - hit.At < 30f)
                return hit.Spot;
            Vector3 at = board.transform.position;
            float off = Config.VfhConfig.DropPileOffset.Value;
            Vector3 best = at + board.transform.forward * off;
            foreach (Vector3 dir in new[] { board.transform.forward, -board.transform.forward, board.transform.right, -board.transform.right })
            {
                Vector3 spot = at + dir * off;
                if (!Blocked(board, at + Vector3.up, spot + Vector3.up))
                {
                    best = spot;
                    break;
                }
            }
            best += Vector3.up * 0.5f;
            Spots[board] = (Time.time, best);
            return best;
        }

        /// <summary>Tests: nothing built between the board and its pile spot.</summary>
        public static bool Clear(HiringBoard board) => !Blocked(board, board.transform.position + Vector3.up, Position(board) + Vector3.up * 0.5f);

        private static bool Blocked(HiringBoard board, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            foreach (RaycastHit h in Physics.RaycastAll(from, d.normalized, d.magnitude, WallMask, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(board.transform))
                    return true;
            return false;
        }

        /// <summary>Drops the items (all, or those <paramref name="which"/> picks) at <paramref name="at"/>. Returns how many stacks.</summary>
        public static int DropAll(Inventory inventory, Vector3 at, string reason, string hid, System.Func<ItemDrop.ItemData, bool>? which = null)
        {
            var items = inventory.GetAllItems().Where(i => which == null || which(i)).ToList();
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
            foreach (ItemDrop.ItemData item in items)
                inventory.RemoveItem(item);
            if (items.Count > 0)
                VfhLog.I(LogCat.Deliver, "drop_pile", ("hid", hid), ("stacks", items.Count), ("pos", at), ("reason", reason),
                    ("items", string.Join(",", items.Select(i => $"{GearApplier.Name(i)}x{i.m_stack}"))));
            return items.Count;
        }
    }
}
