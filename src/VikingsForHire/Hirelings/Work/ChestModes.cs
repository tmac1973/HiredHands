using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>What workers may do with a chest.</summary>
    internal enum ChestMode
    {
        /// <summary>Put in and take out; its contents count as the base's stock.</summary>
        Normal,

        /// <summary>Workers store into it but never take from it, and it isn't stock (feed for animals in a pen).</summary>
        PutOnly,

        /// <summary>Workers take from it but never store into it (a supply chest a player fills).</summary>
        TakeOnly,

        /// <summary>Workers leave it alone.</summary>
        Off,
        /// <summary>Only the Rancher puts in and takes out (a pen's feed chest); everyone else leaves it alone, and it isn't stock.</summary>
        RancherOnly,
    }

    /// <summary>
    /// A chest's worker mode, kept on the chest (so everyone sees the same) and set by a player with Shift+E on it, under
    /// the ward rules. Shown in the chest's hover text near a hiring board.
    /// </summary>
    internal static class ChestModes
    {
        public const string Key = "vfh_chest_mode";
        private const float BoardNear = 80f;

        public static ChestMode Of(Container c)
        {
            ZDO? zdo = c != null && c.m_nview != null && c.m_nview.IsValid() ? c.m_nview.GetZDO() : null;
            int v = zdo?.GetInt(Key) ?? 0;
            return v is >= 0 and <= 4 ? (ChestMode)v : ChestMode.Normal;
        }

        /// <summary>The mode as this hireling sees it: a Rancher-only chest is a normal one to the Rancher, off to the rest.</summary>
        public static ChestMode For(Container c, Hireling h)
        {
            ChestMode m = Of(c);
            return m != ChestMode.RancherOnly ? m : h.Job == Core.JobType.Rancher ? ChestMode.Normal : ChestMode.Off;
        }

        public static bool CanPut(Container c, Hireling h) => For(c, h) is ChestMode.Normal or ChestMode.PutOnly;
        public static bool CanTake(Container c, Hireling h) => For(c, h) is ChestMode.Normal or ChestMode.TakeOnly;
        public static IEnumerable<Container> Put(IEnumerable<Container> chests, Hireling h) => chests.Where(c => CanPut(c, h));

        public static bool CanPut(Container c) => Of(c) is ChestMode.Normal or ChestMode.PutOnly;
        public static bool CanTake(Container c) => Of(c) is ChestMode.Normal or ChestMode.TakeOnly;
        public static bool IsStock(Container c) => Of(c) is ChestMode.Normal or ChestMode.TakeOnly;

        public static IEnumerable<Container> Put(IEnumerable<Container> chests) => chests.Where(CanPut);
        public static IEnumerable<Container> Take(IEnumerable<Container> chests) => chests.Where(CanTake);
        public static IEnumerable<Container> Stock(IEnumerable<Container> chests) => chests.Where(IsStock);

        private static string Label(ChestMode m) => m switch
        {
            ChestMode.PutOnly => "$vfh_chest_put_only",
            ChestMode.TakeOnly => "$vfh_chest_take_only",
            ChestMode.Off => "$vfh_chest_off",
            ChestMode.RancherOnly => "$vfh_chest_rancher",
            _ => "$vfh_chest_normal",
        };

        private static bool NearABoard(Vector3 at) => HiringBoard.Loaded.Any(b => b != null && Vector3.Distance(b.transform.position, at) <= BoardNear);

        private static void Cycle(Container c, Humanoid user)
        {
            if (!PrivateArea.CheckAccess(c.transform.position, 0f, flash: true))
                return;
            // Round: put and take → put only → take only → the Rancher only → leave alone.
            ChestMode next = Of(c) switch
            {
                ChestMode.Normal => ChestMode.PutOnly,
                ChestMode.PutOnly => ChestMode.TakeOnly,
                ChestMode.TakeOnly => ChestMode.RancherOnly,
                ChestMode.RancherOnly => ChestMode.Off,
                _ => ChestMode.Normal,
            };
            c.m_nview.ClaimOwnership();
            c.m_nview.GetZDO().Set(Key, (int)next);
            user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$vfh_chest_mode_set", Localization.instance.Localize(Label(next))));
            VfhLog.I(LogCat.Work, "chest.mode", ("chest", Utils.GetPrefabName(c.transform.root.gameObject)), ("pos", c.transform.position), ("mode", next));
        }

        /// <summary>Shift+E on a chest near a hiring board cycles its worker mode instead of opening it.</summary>
        [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
        private static class InteractPatch
        {
            private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
            {
                try
                {
                    if (hold || !MarkKeys.Held(VfhConfig.ChestMarkKey, alt) || character != Player.m_localPlayer ||
                        __instance.m_nview == null || !__instance.m_nview.IsValid() || !NearABoard(__instance.transform.position))
                        return true;
                    Cycle(__instance, character);
                    __result = true;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("ChestModes.Interact", e);
                    return true;
                }
            }
        }

        /// <summary>The chest's hover text says its worker mode (and how to change it) near a hiring board.</summary>
        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        private static class HoverPatch
        {
            private static void Postfix(Container __instance, ref string __result)
            {
                try
                {
                    if (!MarkKeys.On(VfhConfig.ChestMarkKey) || __instance.m_nview == null || !__instance.m_nview.IsValid() ||
                        !NearABoard(__instance.transform.position))
                        return;
                    __result += Localization.instance.Localize(MarkKeys.Hint(VfhConfig.ChestMarkKey) + "$vfh_chest_workers",
                        Localization.instance.Localize(Label(Of(__instance))));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("ChestModes.Hover", e);
                }
            }
        }
    }
}
