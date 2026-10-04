using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// The Command Stone: a held item (four qualities) made and upgraded at the workbench. Each quality's materials come
    /// from the data file (commandStone[q].cost); the recipe lists every material any quality uses, and the amount a
    /// quality needs is read from the data file when the game asks (zero hides the row).
    /// </summary>
    internal static class CommandStoneItem
    {
        public const string PrefabName = "VFH_CommandStone";
        private const string BasePrefab = "Club";     // a one-handed item with a hand attachment and animations
        private const string LookPrefab = "Crystal";  // whose mesh and icon it borrows
        private const float StoneScale = 0.35f;       // relative to the club part it replaces

        private static CustomItem? _item;
        private static readonly HashSet<Piece.Requirement> OurRequirements = new();

        public static Recipe? Recipe => _item?.Recipe?.Recipe;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Follow, "stone.register_failed", Create);
            DataStore.Changed += () => VfhLog.Guard(LogCat.Follow, "stone.recipe_update_failed", RebuildRequirements);
        }

        public static bool IsStone(ItemDrop.ItemData? item) =>
            item?.m_dropPrefab != null && item.m_dropPrefab.name == PrefabName;

        private static void Create()
        {
            if (_item != null)
                return;
            var config = new ItemConfig
            {
                Name = "$vfh_command_stone",
                Description = "$vfh_command_stone_desc",
                CraftingStation = CraftingStations.Workbench,
                MinStationLevel = 1,
                Requirements = Materials().Select(m => new RequirementConfig(m, Amount(m, 1), 0, true)).ToArray(),
            };
            _item = new CustomItem(PrefabName, BasePrefab, config);
            GameObject prefab = _item.ItemPrefab;
            ItemDrop.ItemData.SharedData shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_maxQuality = DataValidator.StoneQualities;
            shared.m_weight = 1f;
            shared.m_teleportable = true;
            shared.m_useDurability = false;
            shared.m_damages = new HitData.DamageTypes();
            shared.m_damagesPerLevel = new HitData.DamageTypes();
            shared.m_attackForce = 0f;
            shared.m_backstabBonus = 1f;
            shared.m_value = 0;
            Borrow(prefab, shared);
            ItemManager.Instance.AddItem(_item);
            OurRequirements.Clear();
            foreach (Piece.Requirement r in _item.Recipe!.Recipe.m_resources)
                OurRequirements.Add(r);
            VfhLog.I(LogCat.Follow, "stone.registered", ("materials", string.Join(",", Materials())));
        }

        // Crystal's mesh and icon on the club's hand attachment and dropped model.
        private static void Borrow(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            GameObject? look = PrefabManager.Instance.GetPrefab(LookPrefab);
            if (look == null)
            {
                VfhLog.W(LogCat.Follow, "stone.look_missing", ("prefab", LookPrefab));
                return;
            }
            ItemDrop.ItemData.SharedData lookShared = look.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_icons = lookShared.m_icons;
            MeshFilter? srcMesh = look.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.sharedMesh != null);
            MeshRenderer? srcRenderer = srcMesh != null ? srcMesh.GetComponent<MeshRenderer>() : null;
            if (srcMesh == null || srcRenderer == null)
                return;
            VfhLog.I(LogCat.Follow, "stone.look", ("crystalMesh", srcMesh.sharedMesh.bounds.size),
                ("clubParts", string.Join(";", prefab.GetComponentsInChildren<MeshFilter>(true).Select(m => $"{m.name}:{(m.sharedMesh != null ? m.sharedMesh.bounds.size.ToString() : "none")}@{m.transform.localScale}"))));
            float newSize = Mathf.Max(srcMesh.sharedMesh.bounds.size.x, srcMesh.sharedMesh.bounds.size.y, srcMesh.sharedMesh.bounds.size.z);
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                // Keep each part the size of the club part it replaces (the crystal's mesh is built far larger),
                // shrunk a little: it's a hand-held stone, not a club head.
                float oldSize = mf.sharedMesh != null ? Mathf.Max(mf.sharedMesh.bounds.size.x, mf.sharedMesh.bounds.size.y, mf.sharedMesh.bounds.size.z) : 0f;
                if (oldSize > 0f && newSize > 0f)
                    mf.transform.localScale *= oldSize / newSize * StoneScale;
                mf.sharedMesh = srcMesh.sharedMesh;
                if (mf.GetComponent<MeshRenderer>() is MeshRenderer mr)
                    mr.sharedMaterials = srcRenderer.sharedMaterials;
            }
        }

        /// <summary>Every material any quality uses, in a stable order.</summary>
        private static List<string> Materials() =>
            DataStore.Current.CommandStone.OrderBy(s => s.Quality).SelectMany(s => s.Cost.Keys).Distinct().ToList();

        /// <summary>How many of an item a quality needs (0 when that quality doesn't use it).</summary>
        public static int Amount(string item, int quality)
        {
            StoneLevelData? q = DataStore.Current.CommandStone.FirstOrDefault(s => s.Quality == quality);
            return q != null && q.Cost.TryGetValue(item, out int n) ? n : 0;
        }

        // A data change can add materials: rebuild the requirement list from the live tables.
        private static void RebuildRequirements()
        {
            Recipe? recipe = Recipe;
            if (recipe == null || ObjectDB.instance == null)
                return;
            var reqs = new List<Piece.Requirement>();
            foreach (string m in Materials())
            {
                ItemDrop? drop = ObjectDB.instance.GetItemPrefab(m)?.GetComponent<ItemDrop>();
                if (drop == null)
                {
                    VfhLog.W(LogCat.Follow, "stone.unknown_material", ("item", m));
                    continue;
                }
                reqs.Add(new Piece.Requirement { m_resItem = drop, m_amount = Amount(m, 1), m_amountPerLevel = 0, m_recover = true });
            }
            recipe.m_resources = reqs.ToArray();
            OurRequirements.Clear();
            foreach (Piece.Requirement r in reqs)
                OurRequirements.Add(r);
            VfhLog.I(LogCat.Follow, "stone.recipe_updated", ("materials", string.Join(",", Materials())));
        }

        [HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
        private static class AmountPatch
        {
            private static bool Prefix(Piece.Requirement __instance, int qualityLevel, ref int __result)
            {
                try
                {
                    if (!OurRequirements.Contains(__instance) || __instance.m_resItem == null)
                        return true;
                    __result = Amount(__instance.m_resItem.gameObject.name, Math.Max(1, qualityLevel));
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("CommandStone.GetAmount", e);
                    return true;
                }
            }
        }
    }
}
