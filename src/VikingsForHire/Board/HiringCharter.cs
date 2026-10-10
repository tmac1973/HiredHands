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
    /// Moving a board without losing its level or its people. Deconstructing a board with the hammer gives you a Hiring
    /// Charter (into your inventory, or at your feet if it's full) when it has a level to keep (2 or more) or hirelings:
    /// it remembers the level and carries the hirelings, packed away (CharterPacking). Placing a new board while you
    /// carry one starts it at that level, and the hirelings walk in; the charter is used up. A charter with hirelings is
    /// used first, then the highest level. A board destroyed by monsters or damage gives none (its hirelings leave).
    /// </summary>
    internal static class HiringCharter
    {
        public const string PrefabName = "VFH_HiringCharter";
        private const string BasePrefab = "AmberPearl";
        public const string LevelKey = "vfh_charter_level";
        public const string RosterKey = "vfh_charter_roster";
        public const string CountKey = "vfh_charter_count";
        public const string NamesKey = "vfh_charter_names";

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

        public static int CountOf(ItemDrop.ItemData item) =>
            item.m_customData.TryGetValue(CountKey, out string v) && int.TryParse(v, out int n) ? n : 0;

        /// <summary>Charters with hirelings first (placing a board means taking them along), then the highest level.</summary>
        public static ItemDrop.ItemData? Best(Inventory inv) =>
            inv.GetAllItems().Where(IsCharter).OrderByDescending(i => CountOf(i) > 0).ThenByDescending(LevelOf).FirstOrDefault();

        /// <summary>
        /// A new board takes a charter: its level (when higher) and its hirelings, who arrive like new hires. Returns
        /// whether the charter was used (and removes it from the inventory).
        /// </summary>
        public static bool Apply(HiringBoard placed, ItemDrop.ItemData charter, Inventory inv, Player? who)
        {
            if (placed.Zdo == null || !placed.Zdo.IsOwner())
                return false;
            int level = LevelOf(charter);
            int count = CountOf(charter);
            if (count == 0 && placed.Level >= level)
                return false;
            if (level > placed.Level)
                placed.Zdo.Set(BoardZdo.Level, level);
            if (count > 0 && charter.m_customData.TryGetValue(RosterKey, out string data))
                count = CharterPacking.Unpack(placed.Zdo, Convert.FromBase64String(data));
            inv.RemoveItem(charter);
            who?.Message(MessageHud.MessageType.Center, count > 0
                ? Localization.instance.Localize("$vfh_charter_used_hirelings", Math.Max(level, placed.Level).ToString(), count.ToString())
                : Localization.instance.Localize("$vfh_charter_used", level.ToString()));
            VfhLog.I(LogCat.Board, "charter.used", ("board", placed.Id), ("level", level), ("hirelings", count));
            return true;
        }

        /// <summary>The removing player's game, as a board is deconstructed: hand over a charter for its level (and hirelings).</summary>
        public static void Give(HiringBoard board, CharterPacking.Packed? packed = null)
        {
            Player me = Player.m_localPlayer;
            int level = packed?.Level ?? board.Level;
            int count = packed?.Count ?? 0;
            if (me == null || (level < 2 && count == 0))
                return;
            GameObject? prefab = ObjectDB.instance?.GetItemPrefab(PrefabName);
            if (prefab == null)
                return;
            ItemDrop.ItemData charter = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            charter.m_stack = 1;
            charter.m_dropPrefab = prefab;
            charter.m_customData[LevelKey] = level.ToString();
            if (packed != null && count > 0)
            {
                charter.m_customData[RosterKey] = Convert.ToBase64String(packed.Roster);
                charter.m_customData[CountKey] = count.ToString();
                charter.m_customData[NamesKey] = packed.Names;
            }
            if (!me.GetInventory().AddItem(charter))
                ItemDrop.DropItem(charter, 1, me.transform.position + Vector3.up, Quaternion.identity);
            me.Message(MessageHud.MessageType.Center, count > 0
                ? Localization.instance.Localize("$vfh_charter_given_hirelings", level.ToString(), count.ToString())
                : Localization.instance.Localize("$vfh_charter_given", level.ToString()));
            VfhLog.I(LogCat.Board, "charter.given", ("board", board.Id), ("level", level), ("hirelings", count));
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
                    ItemDrop.ItemData? best = Best(inv);
                    if (best == null)
                        return;
                    HiringBoard? placed = HiringBoard.Loaded.Where(b => b != null && b.Zdo != null && Vector3.Distance(b.transform.position, pos) < 0.5f)
                        .FirstOrDefault();
                    if (placed != null)
                        Apply(placed, best, inv, __instance);
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
                    if (IsCharter(item) && CountOf(item) > 0)
                        __result += "\n" + Localization.instance.Localize("$vfh_charter_carries", CountOf(item).ToString(),
                            item.m_customData.TryGetValue(NamesKey, out string names) ? names : "");
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HiringCharter.Tooltip", e);
                }
            }
        }
    }
}
