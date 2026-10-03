using System;
using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// A hireling's cargo grid is always 8×4, but only the first N slots (row by row) are usable, N from the level table.
    /// Slot-specific moves into a locked slot are refused; automatic placement is refused if the item can't fit in the
    /// usable slots (vanilla fills the earliest free slot first, so an accepted item always lands in a usable one).
    /// Locked slots are greyed out in the container UI.
    /// </summary>
    internal static class CargoGate
    {
        private static readonly Color Locked = new(0.25f, 0.25f, 0.25f, 0.85f);

        private static bool SlotAllowed(Inventory inv, int x, int y, out Hireling? hireling)
        {
            hireling = Hireling.ForCargo(inv);
            return hireling == null || y * inv.GetWidth() + x < hireling.CargoSlots;
        }

        /// <summary>Whether the whole stack fits in the usable slots (existing stacks plus empty usable slots).</summary>
        private static bool FitsAuto(Inventory inv, ItemDrop.ItemData item, out Hireling? hireling)
        {
            hireling = Hireling.ForCargo(inv);
            if (hireling == null)
                return true;
            int width = inv.GetWidth();
            int limit = hireling.CargoSlots;
            int max = Math.Max(1, item.m_shared.m_maxStackSize);
            int room = 0;
            for (int i = 0; i < limit; i++)
            {
                ItemDrop.ItemData? at = inv.GetItemAt(i % width, i / width);
                if (at == null)
                    room += max;
                else if (at.m_shared.m_name == item.m_shared.m_name && at.m_quality == item.m_quality)
                    room += Math.Max(0, max - at.m_stack);
            }
            return room >= item.m_stack;
        }

        private static void Refused(Hireling h, ItemDrop.ItemData item, string how) =>
            VfhLog.D(LogCat.Hireling, "cargo.refused", ("hid", h.Hid), ("item", item.m_shared.m_name), ("how", how), ("slots", h.CargoSlots));

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
        private static class AddPatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (item == null || FitsAuto(__instance, item, out Hireling? h))
                    return true;
                Refused(h!, item, "auto");
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData), typeof(Vector2i))]
        private static class AddAtPatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
            {
                if (item == null || SlotAllowed(__instance, pos.x, pos.y, out Hireling? h))
                    return true;
                Refused(h!, item, "slot");
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int))]
        private static class MovePatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int x, int y, ref bool __result)
            {
                if (item == null || SlotAllowed(__instance, x, y, out Hireling? h))
                    return true;
                Refused(h!, item, "move");
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        private static class DropPatch
        {
            private static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
            {
                if (item == null || SlotAllowed(__instance.GetInventory(), pos.x, pos.y, out Hireling? h))
                    return true;
                Refused(h!, item, "drop");
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        private static class GreyPatch
        {
            // The UI reuses one grid for every container, so restore anything greyed when it shows something else.
            private static readonly System.Collections.Generic.Dictionary<Image, Color> Original = new();

            private static void Postfix(InventoryGrid __instance)
            {
                Hireling? h = Hireling.ForCargo(__instance.GetInventory());
                int limit = h != null ? h.CargoSlots : int.MaxValue;
                for (int i = 0; i < __instance.m_elements.Count; i++)
                {
                    Image img = __instance.m_elements[i].GetComponent<Image>();
                    if (img == null)
                        continue;
                    if (i >= limit)
                    {
                        if (!Original.ContainsKey(img))
                            Original[img] = img.color;
                        img.color = Locked;
                    }
                    else if (Original.TryGetValue(img, out Color c))
                    {
                        img.color = c;
                        Original.Remove(img);
                    }
                }
            }
        }
    }
}
