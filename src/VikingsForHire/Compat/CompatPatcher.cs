using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Compat
{
    /// <summary>
    /// Keeps AzuAutoStore and AzuCraftyBoxes (and PullMats, which finds chests through CraftyBoxes) away from our
    /// containers. Both mods keep a list of every Container, filled through Boxes.AddContainer; refusing ours there keeps
    /// them out of every store, pull and craft. CanItemBeStored/CanItemBePulled are patched as a second line of defence.
    /// Everything is found by reflection: a changed or missing mod logs a warning and never stops VikingsForHire loading.
    /// </summary>
    internal static class CompatPatcher
    {
        public static readonly List<string> Status = new();

        public static void Apply(Harmony harmony)
        {
            Patch(harmony, "Azumatt.AzuAutoStore", "AzuAutoStore.Util.Boxes", "CanItemBeStored", "Azumatt.AzuAutoStore.yml");
            Patch(harmony, "Azumatt.AzuCraftyBoxes", "AzuCraftyBoxes.Util.Functions.Boxes", "CanItemBePulled", "Azumatt.AzuCraftyBoxes.yml");
            if (Chainloader.PluginInfos.ContainsKey("Spronglehump.PullMats"))
                Report("PullMats", true, 0, 0, "covered by the AzuCraftyBoxes exclusion");
        }

        private static void Patch(Harmony harmony, string guid, string boxesType, string canMethod, string yamlFile)
        {
            string mod = guid.Split('.').Last();
            if (!Chainloader.PluginInfos.TryGetValue(guid, out var info) || info.Instance == null)
            {
                Report(mod, false, 0, 0, "not installed");
                return;
            }

            int patched = 0;
            const int wanted = 2;
            try
            {
                Type? type = info.Instance.GetType().Assembly.GetType(boxesType);
                MethodInfo? add = type == null ? null : AccessTools.Method(type, "AddContainer", new[] { typeof(Container) });
                MethodInfo? can = type?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == canMethod && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(string));

                if (add != null)
                {
                    harmony.Patch(add, prefix: new HarmonyMethod(typeof(CompatPatcher), nameof(AddContainerPrefix)));
                    patched++;
                }
                if (can != null)
                {
                    harmony.Patch(can, prefix: new HarmonyMethod(typeof(CompatPatcher), nameof(CanUsePrefix)));
                    patched++;
                }
            }
            catch (Exception ex)
            {
                VfhLog.Exception(LogCat.Compat, "compat.patch_failed", ex, ("mod", mod));
            }

            Report(mod, true, patched, wanted, patched == wanted ? "ok" : "incomplete");
            if (patched < wanted)
                VfhLog.W(LogCat.Compat, "compat.manual_exclusion_needed", ("mod", mod), ("file", yamlFile),
                    ("add", string.Join(" ", ExcludedContainers.Prefabs.Select(p => $"'{p}: {{exclude: [All]}}'"))));
        }

        private static void Report(string mod, bool loaded, int patched, int wanted, string note)
        {
            Status.Add($"{mod}:{(loaded ? $"{patched}/{wanted}" : "absent")}");
            VfhLog.I(LogCat.Compat, "compat", ("mod", mod), ("loaded", loaded), ("patched", $"{patched}/{wanted}"), ("note", note));
        }

        private static bool AddContainerPrefix(Container container)
        {
            if (!ExcludedContainers.IsExcluded(container))
                return true;
            VfhLog.D(LogCat.Compat, "compat.container_hidden", ("prefab", Utils.GetPrefabName(container.m_rootObjectOverride != null ? container.m_rootObjectOverride.gameObject : container.gameObject)));
            return false;
        }

        // Shared by CanItemBeStored(string container, string prefab) and CanItemBePulled(string container, string prefab, ...).
        private static bool CanUsePrefix(string __0, ref bool __result)
        {
            if (!ExcludedContainers.IsExcluded(__0))
                return true;
            __result = false;
            return false;
        }
    }
}
