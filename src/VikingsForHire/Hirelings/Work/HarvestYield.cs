using System;
using System.Collections.Generic;
using HarmonyLib;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Enhancements;
using VikingsForHire.Diagnostics;
using UnityEngine;
using static VikingsForHire.Core.Enhancements.EnhancementId;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// The gatherers' yield enhancements, applied to what a hireling's own hit drops (inside HarvestPatches' Direct
    /// window, so only when this game applies the hit): each dropped unit gets another copy with the pick's chance —
    /// Lumberjack on wood, Fine eye on fine and core wood, Master woodsman on everything it chops, Rich veins on ore,
    /// Master miner on everything it breaks — plus Seed saver's seed from a felled tree and Gem eye's find from a chunk.
    /// </summary>
    internal static class HarvestYield
    {
        [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new Type[0])]
        private static class DropListPatch
        {
            private static void Postfix(List<GameObject> __result) => Apply(__result);
        }

        [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), typeof(int))]
        private static class DropListAmountPatch
        {
            private static void Postfix(List<GameObject> __result) => Apply(__result);
        }

        private static void Apply(List<GameObject> drops)
        {
            Hireling? h = HarvestPatches.DirectBy;
            if (!HarvestPatches.Direct || h == null || drops == null || !h.Enh.Effects.AnyPicks)
                return;
            try
            {
                Extra(h, drops);
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("HarvestYield.GetDropList", e);
            }
        }

        private static void Extra(Hireling h, List<GameObject> drops)
        {
            int before = drops.Count;
            for (int i = 0; i < before; i++)
            {
                GameObject d = drops[i];
                if (d == null)
                    continue;
                float chance = Bonus(h, d.name);
                while (chance > 0f)
                {
                    if (UnityEngine.Random.value < chance)
                        drops.Add(d);
                    chance -= 1f;
                }
            }
            Component? target = HarvestPatches.DirectTarget;
            if (h.Enh.Has(SeedSaver) && target is TreeBase tree &&
                YieldKinds.Seed(Utils.GetPrefabName(tree.gameObject)) is { Length: > 0 } seed && ObjectDB.instance.GetItemPrefab(seed) is GameObject s)
            {
                for (int n = Mathf.Max(1, Mathf.RoundToInt(h.Enh.Value(SeedSaver, "extra"))); n > 0; n--)
                    drops.Add(s);
                EnhancementEvents.Fire(h, "SeedSaver", LogCat.Work, ("seed", seed));
            }
            if (h.Enh.Has(GemEye) && h.Job == JobType.Miner && UnityEngine.Random.value < h.Enh.Value(GemEye, "chance"))
            {
                string biome = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(h.transform.position).ToString() : "Meadows";
                if (ObjectDB.instance.GetItemPrefab(GemTable.Pick(biome, UnityEngine.Random.value)) is GameObject gem)
                {
                    drops.Add(gem);
                    EnhancementEvents.Fire(h, "GemEye", LogCat.Work, ("gem", gem.name), ("biome", biome));
                }
            }
            if (drops.Count > before)
                EnhancementEvents.Fire(h, "Yield", LogCat.Work, ("extra", drops.Count - before), ("of", before));
        }

        /// <summary>The chance of one more of this item for this hireling (above 1: that many more, the rest by chance).</summary>
        private static float Bonus(Hireling h, string prefab)
        {
            float b = 0f;
            if (h.Job == JobType.Woodcutter)
            {
                if (YieldKinds.Wood(prefab))
                    b += h.Enh.Value(Lumberjack, "share");
                if (YieldKinds.FineWood(prefab))
                    b += h.Enh.Value(FineEye, "share");
                b += h.Enh.Value(MasterWoodsman, "share");
            }
            else if (h.Job == JobType.Miner)
            {
                if (YieldKinds.Ore(prefab))
                    b += h.Enh.Value(RichVeins, "share");
                b += h.Enh.Value(MasterMiner, "share");
            }
            return b;
        }
    }
}
