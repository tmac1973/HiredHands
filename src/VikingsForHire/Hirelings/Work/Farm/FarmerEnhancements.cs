using System;
using System.Collections.Generic;
using HarmonyLib;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;
using static VikingsForHire.Core.Enhancements.EnhancementId;
using Random = UnityEngine.Random;

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>
    /// The Farmer's enhancements (plan/enhancements phase 10), used by HarvestChore and PlantChore: faster steps (Green
    /// thumb, Master farmer), bigger harvests (Bountiful, Seed keeper, Double harvest, Master farmer), more planted per
    /// field (Close planting), crops that grow sooner (Tender care) or put up with cold and shade (Hardy stock).
    /// </summary>
    internal static class FarmerEnhancements
    {
        /// <summary>A plant it places that survives a little cold or shade (Hardy stock).</summary>
        public const string HardyKey = "vfh_hardy";

        /// <summary>Its planting pace multiplier (Green thumb, Master farmer).</summary>
        public static float StepMult(Hireling h) =>
            (1f - h.Enh.Value(GreenThumb, "speed")) * (1f - h.Enh.Value(MasterFarmer, "speed"));

        /// <summary>On a crop it planted with Close planting: the share less room it needs to grow.</summary>
        public const string CloseKey = "vfh_close";

        /// <summary>Close planting: the share of its crops' usual grow room it plants them at (1 without).</summary>
        public static float SpaceMult(Hireling h) => 1f - h.Enh.Value(ClosePlanting, "space");

        /// <summary>The same, from the board's contract (the Orders tab's free-spot count, without the hireling loaded).</summary>
        public static float SpaceMult(VikingsForHire.Core.ContractEntry e)
        {
            if (!Config.VfhConfig.Enhancements.Value || !Core.Enhancements.EnhancementPicks.Parse(e.Enhancements).Has(e.Job, ClosePlanting))
                return 1f;
            return 1f - Core.Enhancements.EnhancementNumbers.Get(Config.DataStore.Current, e.Job, ClosePlanting, "space");
        }

        /// <summary>
        /// What a pick gives, with the Farmer's harvest picks applied: one more of the crop now and then (Bountiful), one
        /// more seed (Seed keeper), a share more of everything (Master farmer), everything twice (Double harvest).
        /// </summary>
        public static void Harvest(Hireling h, Crop crop, List<(GameObject Prefab, int Amount)> items)
        {
            if (!h.Enh.Effects.AnyPicks || items.Count == 0)
                return;
            int before = Count(items);
            float share = h.Enh.Value(MasterFarmer, "share");
            for (int i = 0; i < items.Count; i++)
            {
                (GameObject prefab, int amount) = items[i];
                int extra = 0;
                for (int n = 0; n < amount; n++)
                    if (Random.value < share)
                        extra++;
                if (i == 0 && h.Enh.Has(Bountiful) && Random.value < h.Enh.Value(Bountiful, "chance"))
                    extra++;
                items[i] = (prefab, amount + extra);
            }
            if (h.Enh.Has(SeedKeeper) && crop.Info.Consumes.Length > 0 && Random.value < h.Enh.Value(SeedKeeper, "chance") &&
                ObjectDB.instance.GetItemPrefab(crop.Info.Consumes) is GameObject seed)
                items.Add((seed, 1));
            if (h.Enh.Has(DoubleHarvest) && Random.value < h.Enh.Value(DoubleHarvest, "chance"))
                for (int i = 0; i < items.Count; i++)
                    items[i] = (items[i].Prefab, items[i].Amount * 2);
            int after = Count(items);
            if (after > before)
                EnhancementEvents.Fire(h, "Harvest", LogCat.Work, ("crop", crop.Info.Yields), ("extra", after - before));
        }

        private static int Count(List<(GameObject Prefab, int Amount)> items)
        {
            int n = 0;
            foreach ((GameObject _, int a) in items)
                n += a;
            return n;
        }

        /// <summary>A plant it just placed: Tender care sets its clock forward, Hardy stock marks it.</summary>
        public static void Tend(Hireling h, GameObject planted)
        {
            if (!h.Enh.Effects.AnyPicks || planted.GetComponent<Plant>() is not Plant plant || plant.m_nview == null || !plant.m_nview.IsValid())
                return;
            ZDO z = plant.m_nview.GetZDO();
            if (h.Enh.Has(TenderCare) && ZNet.instance != null)
            {
                long start = z.GetLong(ZDOVars.s_plantTime, ZNet.instance.GetTime().Ticks);
                long back = TimeSpan.FromSeconds(plant.m_growTimeMax * h.Enh.Value(TenderCare, "share")).Ticks;
                z.Set(ZDOVars.s_plantTime, start - back);
                EnhancementEvents.Fire(h, "TenderCare", LogCat.Work, ("plant", plant.name));
            }
            if (h.Enh.Has(ClosePlanting))
                z.Set(CloseKey, h.Enh.Value(ClosePlanting, "space"));
            if (h.Enh.Has(HardyStock))
            {
                z.Set(HardyKey, true);
                EnhancementEvents.Fire(h, "HardyStock", LogCat.Work, ("plant", plant.name));
            }
        }

        /// <summary>
        /// Hardy stock: a marked plant grows where the game finds it a little too cold or too shaded. Close planting: a
        /// marked plant needs only its share of the usual room around it.
        /// </summary>
        [HarmonyPatch(typeof(Plant), nameof(Plant.UpdateHealth))]
        private static class HardyPatch
        {
            private static void Postfix(Plant __instance)
            {
                try
                {
                    if (__instance.m_status == Plant.Status.NoSpace && __instance.m_nview != null && __instance.m_nview.IsValid() &&
                        __instance.m_nview.GetZDO().GetFloat(CloseKey) is float close && close > 0f)
                    {
                        float was = __instance.m_growRadius;
                        __instance.m_growRadius = was * (1f - close);
                        try
                        {
                            if (__instance.HaveGrowSpace())
                                __instance.m_status = Plant.Status.Healthy;
                        }
                        finally
                        {
                            __instance.m_growRadius = was;
                        }
                    }
                    if (__instance.m_status is not (Plant.Status.TooCold or Plant.Status.NoSun))
                        return;
                    if (__instance.m_nview != null && __instance.m_nview.IsValid() && __instance.m_nview.GetZDO().GetBool(HardyKey))
                        __instance.m_status = Plant.Status.Healthy;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("FarmerEnhancements.Hardy", e);
                }
            }
        }
    }
}
