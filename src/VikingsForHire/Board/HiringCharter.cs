using System;
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>
    /// Moving a board without losing its level. Deconstructing a board of level 2 or more with the hammer gives you a
    /// Hiring Charter that remembers its level (into your inventory, or at your feet if it's full). Placing a new board
    /// while you carry one starts it at that level and uses the charter up (the highest one, if you carry several).
    /// Nothing is refunded twice and there's nothing heavy to carry. A board destroyed by monsters or damage gives none.
    /// </summary>
    internal static class HiringCharter
    {
        public const string PrefabName = "VFH_HiringCharter";
        private const string BasePrefab = "AmberPearl";
        public const string LevelKey = "vfh_charter_level";

        public static void Register() =>
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Board, "charter.register_failed", Create);

        private static bool _created;

        // Once: the vanilla-prefabs event fires again on every return to the main menu, and Jotunn keeps the item.
        private static void Create()
        {
            if (_created || (ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(PrefabName) != null))
                return;
            _created = true;
            var item = new CustomItem(PrefabName, BasePrefab, new ItemConfig { Name = "$vfh_charter", Description = "$vfh_charter_desc" });
            ItemDrop.ItemData.SharedData shared = item.ItemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_maxStackSize = 1; // each one carries its own level
            shared.m_weight = 0.1f;
            shared.m_teleportable = true;
            shared.m_value = 0;
            ItemManager.Instance.AddItem(item);
            VfhLog.I(LogCat.Board, "charter.registered");
        }

        public static bool IsCharter(ItemDrop.ItemData? item) => item?.m_dropPrefab != null && item.m_dropPrefab.name == PrefabName;

        public static int LevelOf(ItemDrop.ItemData item) =>
            item.m_customData.TryGetValue(LevelKey, out string v) && int.TryParse(v, out int l) ? l : 0;

        /// <summary>The removing player's game, as a board is deconstructed: hand over a charter for its level.</summary>
        public static void Give(HiringBoard board)
        {
            Player me = Player.m_localPlayer;
            int level = board.Level;
            if (me == null || level < 2)
                return;
            GameObject? prefab = ObjectDB.instance?.GetItemPrefab(PrefabName);
            if (prefab == null)
                return;
            ItemDrop.ItemData charter = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            charter.m_stack = 1;
            charter.m_dropPrefab = prefab;
            charter.m_customData[LevelKey] = level.ToString();
            if (!me.GetInventory().AddItem(charter))
                ItemDrop.DropItem(charter, 1, me.transform.position + Vector3.up, Quaternion.identity);
            me.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$vfh_charter_given", level.ToString()));
            VfhLog.I(LogCat.Board, "charter.given", ("board", board.Id), ("level", level));
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        private static class PlacePatch
        {
            private static void Postfix(Player __instance, Piece piece, Vector3 pos)
            {
                try
                {
                    if (__instance != Player.m_localPlayer || piece == null || piece.GetComponent<HiringBoard>() == null)
                        return;
                    Inventory inv = __instance.GetInventory();
                    ItemDrop.ItemData? best = inv.GetAllItems().Where(IsCharter).OrderByDescending(LevelOf).FirstOrDefault();
                    if (best == null)
                        return;
                    HiringBoard? placed = HiringBoard.Loaded.Where(b => b != null && b.Zdo != null && Vector3.Distance(b.transform.position, pos) < 0.5f)
                        .FirstOrDefault();
                    if (placed?.Zdo == null || !placed.Zdo.IsOwner() || placed.Level >= LevelOf(best))
                        return;
                    int level = LevelOf(best);
                    placed.Zdo.Set(BoardZdo.Level, level);
                    inv.RemoveItem(best);
                    __instance.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$vfh_charter_used", level.ToString()));
                    VfhLog.I(LogCat.Board, "charter.used", ("board", placed.Id), ("level", level));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HiringCharter.PlacePiece", e);
                }
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
        private static class TooltipPatch
        {
            private static void Postfix(ItemDrop.ItemData item, ref string __result)
            {
                try
                {
                    if (IsCharter(item) && LevelOf(item) > 0)
                        __result += "\n" + Localization.instance.Localize("$vfh_charter_level", LevelOf(item).ToString());
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HiringCharter.Tooltip", e);
                }
            }
        }
    }
}
