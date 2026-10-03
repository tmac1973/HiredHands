using System;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>
    /// The board's storage holds only Coins and acceptable food. Every way an item can enter an inventory from the UI
    /// (drag-drop, right-click, stack-all) ends in one of the patched methods below; loading a saved inventory uses a
    /// private overload, so a config change never deletes stored items.
    /// </summary>
    internal static class BoardStorage
    {
        public const string InventoryName = "$vfh_board_storage";
        public const string CoinsPrefab = "Coins";

        /// <summary>Test fixtures set this to put anything into a board (board_force_add).</summary>
        [ThreadStatic] public static bool Bypass;

        private static float _lastRejectMessage;

        public static bool IsBoardInventory(Inventory? inventory) => inventory != null && inventory.GetName() == InventoryName;

        public static bool IsCoins(ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == CoinsPrefab;

        public static bool IsFood(ItemDrop.ItemData item)
        {
            ItemDrop.ItemData.SharedData s = item.m_shared;
            bool hasFood = s.m_food > 0f || s.m_foodStamina > 0f || s.m_foodEitr > 0f;
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            return FoodPoints.IsAcceptable(prefab, hasFood, VfhConfig.AllowRawFood.Value, DataStore.Current.Food.RawFoods);
        }

        public static int PointsPerItem(ItemDrop.ItemData item) =>
            FoodPoints.PointsFor(item.m_shared.m_food, item.m_shared.m_foodStamina, item.m_shared.m_foodEitr);

        public static bool Allowed(ItemDrop.ItemData item) => Bypass || IsCoins(item) || IsFood(item);

        /// <summary>Food points and coins currently in an inventory.</summary>
        public static Cost Totals(Inventory inventory)
        {
            int food = 0, coins = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (IsCoins(item))
                    coins += item.m_stack;
                else if (IsFood(item))
                    food += PointsPerItem(item) * item.m_stack;
            }
            return new Cost(food, coins);
        }

        private static bool Reject(Inventory inventory, ItemDrop.ItemData? item)
        {
            if (item == null || !IsBoardInventory(inventory) || Allowed(item))
                return false;
            VfhLog.D(LogCat.Board, "storage.reject", ("item", item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name));
            if (Player.m_localPlayer != null && Time.time - _lastRejectMessage > 1f)
            {
                _lastRejectMessage = Time.time;
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$vfh_board_storage_reject");
            }
            return true;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
        private static class AddItemPatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                try
                {
                    if (!Reject(__instance, item))
                        return true;
                    __result = false;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("BoardStorage.AddItem", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData), typeof(Vector2i))]
        private static class AddItemAtPatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                try
                {
                    if (!Reject(__instance, item))
                        return true;
                    __result = false;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("BoardStorage.AddItemAt", e);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int))]
        private static class MoveItemToThisPatch
        {
            private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
            {
                try
                {
                    if (!Reject(__instance, item))
                        return true;
                    __result = false;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("BoardStorage.MoveItemToThis", e);
                    return true;
                }
            }
        }

        /// <summary>Catches drag-drop before the grid swaps two items, so a rejected drop leaves both where they were.</summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        private static class DropItemPatch
        {
            private static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, ref bool __result)
            {
                try
                {
                    if (!Reject(__instance.GetInventory(), item))
                        return true;
                    __result = false;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("BoardStorage.DropItem", e);
                    return true;
                }
            }
        }
    }
}
