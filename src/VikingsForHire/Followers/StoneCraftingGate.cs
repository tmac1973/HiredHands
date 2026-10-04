using System;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// Each Command Stone quality also needs a hiring board of a high enough level near the workbench
    /// (commandStone[q].requiredBoardLevel within StoneBoardSearchRadius). Without one the recipe can't be made and its
    /// description says what's missing.
    /// </summary>
    internal static class StoneCraftingGate
    {
        /// <summary>Whether a board good enough for this quality is near the station the local player is using.</summary>
        public static bool BoardOk(int quality, out int neededLevel) =>
            BoardOk(quality, Player.m_localPlayer != null ? Player.m_localPlayer.GetCurrentCraftingStation() : null, out neededLevel);

        /// <summary>Whether a board good enough for this quality is near the given station.</summary>
        public static bool BoardOk(int quality, CraftingStation? station, out int neededLevel)
        {
            neededLevel = 0;
            try
            {
                neededLevel = new LevelRules(DataStore.Current).RequiredBoardLevelForStone(quality);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            if (station == null)
                return false;
            float radius = VfhConfig.StoneBoardSearchRadius.Value;
            int level = neededLevel;
            return HiringBoard.Loaded.Any(b => b != null && b.Level >= level &&
                                               Vector3.Distance(b.transform.position, station.transform.position) <= radius);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Recipe), typeof(bool), typeof(int), typeof(int))]
        private static class HaveRequirementsPatch
        {
            private static void Postfix(Recipe recipe, bool discover, int qualityLevel, ref bool __result)
            {
                try
                {
                    if (!__result || discover || recipe == null || recipe != CommandStoneItem.Recipe)
                        return;
                    if (!BoardOk(qualityLevel, out _))
                        __result = false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("StoneCraftingGate.HaveRequirements", e);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private static class DescriptionPatch
        {
            private static void Postfix(InventoryGui __instance)
            {
                try
                {
                    Recipe? recipe = __instance.m_selectedRecipe.Recipe;
                    if (recipe == null || recipe != CommandStoneItem.Recipe)
                        return;
                    ItemDrop.ItemData? item = __instance.m_selectedRecipe.ItemData;
                    int quality = item == null ? 1 : item.m_quality + 1;
                    if (quality > Core.Data.DataValidator.StoneQualities || BoardOk(quality, out int needed))
                        return;
                    __instance.m_recipeDecription.text += "\n\n<color=red>" + Localization.instance.Localize("$vfh_stone_needs_board",
                        needed.ToString(), VfhConfig.StoneBoardSearchRadius.Value.ToString("0")) + "</color>";
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("StoneCraftingGate.UpdateRecipe", e);
                }
            }
        }
    }
}
