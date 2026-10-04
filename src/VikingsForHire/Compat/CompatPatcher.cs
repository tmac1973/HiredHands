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
    /// Keeps AzuAutoStore, AzuCraftyBoxes (and PullMats, which finds chests through CraftyBoxes) and PetPantry away from our
    /// containers. Both mods keep a list of every Container, filled through Boxes.AddContainer; refusing ours there keeps
    /// them out of every store, pull and craft. CanItemBeStored/CanItemBePulled are patched as a second line of defence.
    /// Everything is found by reflection: a changed or missing mod logs a warning and never stops Hired Hands loading.
    /// </summary>
    internal static class CompatPatcher
    {
        public static readonly List<string> Status = new();

        public static void Apply(Harmony harmony)
        {
            Patch(harmony, "Azumatt.AzuAutoStore", "AzuAutoStore.Util.Boxes", "CanItemBeStored", "Azumatt.AzuAutoStore.yml");
            Patch(harmony, "Azumatt.AzuCraftyBoxes", "AzuCraftyBoxes.Util.Functions.Boxes", "CanItemBePulled", "Azumatt.AzuCraftyBoxes.yml");
            PatchPetPantry(harmony);
            PatchCreatureLevelControl(harmony);
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

        /// <summary>PetPantry feeds tamed animals from every player-built container; refusing ours keeps them off the board's food.</summary>
        private static void PatchPetPantry(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.TryGetValue("Azumatt.PetPantry", out var info) || info.Instance == null)
            {
                Report("PetPantry", false, 0, 0, "not installed");
                return;
            }
            int patched = 0;
            try
            {
                Type? type = info.Instance.GetType().Assembly.GetType("PetPantry.UtilityMethods");
                MethodInfo? register = type == null ? null : AccessTools.Method(type, "RegisterContainer", new[] { typeof(Container) });
                if (register != null)
                {
                    harmony.Patch(register, prefix: new HarmonyMethod(typeof(CompatPatcher), nameof(AddContainerPrefix)));
                    patched++;
                }
            }
            catch (Exception ex)
            {
                VfhLog.Exception(LogCat.Compat, "compat.patch_failed", ex, ("mod", "PetPantry"));
            }
            Report("PetPantry", true, patched, 1, patched == 1 ? "ok" : "incomplete: tamed animals may eat hiring board food");
            if (patched == 0)
                VfhLog.W(LogCat.Compat, "compat.petpantry_unpatched", ("effect", "tamed animals may eat food stored on hiring boards"));
        }

        /// <summary>
        /// CreatureLevelAndLootControl gives every non-player character extra effects and infusions in a Character.Awake
        /// postfix. Hirelings must stay plain level-1 characters whose stats come from our tables, so that postfix is
        /// skipped for them. (Its health/damage factors follow the character level, which stays 1.)
        /// </summary>
        private static void PatchCreatureLevelControl(Harmony harmony)
        {
            const string guid = "org.bepinex.plugins.creaturelevelcontrol";
            if (!Chainloader.PluginInfos.TryGetValue(guid, out var info) || info.Instance == null)
            {
                Report("CreatureLevelControl", false, 0, 0, "not installed");
                return;
            }
            int patched = 0;
            try
            {
                Type? nested = info.Instance.GetType().Assembly.GetTypes().FirstOrDefault(t => t.Name == "AttachLevelBehaviorToCharacters");
                MethodInfo? postfix = nested == null ? null : AccessTools.Method(nested, "Postfix");
                if (postfix != null)
                {
                    harmony.Patch(postfix, prefix: new HarmonyMethod(typeof(CompatPatcher), nameof(SkipForHireling)));
                    patched++;
                }
            }
            catch (Exception ex)
            {
                VfhLog.Exception(LogCat.Compat, "compat.patch_failed", ex, ("mod", "CreatureLevelControl"));
            }
            Report("CreatureLevelControl", true, patched, 1, patched == 1 ? "ok" : "incomplete: hirelings may get extra effects");
        }

        private static bool SkipForHireling(Character __0)
        {
            long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                return __0 == null || __0.GetComponent<Hirelings.Hireling>() == null;
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("CompatPatcher.SkipForHireling", e);
                return true;
            }
            finally
            {
                VikingsForHire.Diagnostics.PerfCounters.Patch("CompatPatcher.SkipForHireling", vfhStarted);
            }
        }

        private static void Report(string mod, bool loaded, int patched, int wanted, string note)
        {
            Status.Add($"{mod}:{(loaded ? $"{patched}/{wanted}" : "absent")}");
            VfhLog.I(LogCat.Compat, "compat", ("mod", mod), ("loaded", loaded), ("patched", $"{patched}/{wanted}"), ("note", note));
        }

        private static bool AddContainerPrefix(Container container)
        {
            long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (!ExcludedContainers.IsExcluded(container))
                    return true;
                VfhLog.D(LogCat.Compat, "compat.container_hidden", ("prefab", Utils.GetPrefabName(container.m_rootObjectOverride != null ? container.m_rootObjectOverride.gameObject : container.gameObject)));
                return false;
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("CompatPatcher.AddContainer", e);
                return true;
            }
            finally
            {
                VikingsForHire.Diagnostics.PerfCounters.Patch("CompatPatcher.AddContainer", vfhStarted);
            }
        }

        // Shared by CanItemBeStored(string container, string prefab) and CanItemBePulled(string container, string prefab, ...).
        private static bool CanUsePrefix(string __0, ref bool __result)
        {
            long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (!ExcludedContainers.IsExcluded(__0))
                    return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("CompatPatcher.CanUse", e);
                return true;
            }
            finally
            {
                VikingsForHire.Diagnostics.PerfCounters.Patch("CompatPatcher.CanUse", vfhStarted);
            }
        }
    }
}
