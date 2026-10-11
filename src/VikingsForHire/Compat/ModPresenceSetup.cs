using System;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Compat
{
    /// <summary>
    /// At the main menu, when every plugin has loaded: records all loaded plugin GUIDs in ModPresence (so a data file
    /// synced from a server later can name any of them), and logs the ones any job's withMods names.
    /// </summary>
    internal static class ModPresenceSetup
    {
        [HarmonyPatch(typeof(FejdStartup), "Awake")]
        private static class MenuPatch
        {
            private static void Postfix()
            {
                try
                {
                    ModPresence.SetLoaded(Chainloader.PluginInfos.Keys);
                    string named = string.Join(",", DataStore.Current.Jobs.Values.SelectMany(j => j.WithMods).Select(m => m.Guid).Distinct()
                        .Select(g => $"{g}:{(ModPresence.IsLoaded(g) ? "loaded" : "absent")}"));
                    VfhLog.I(LogCat.Core, "compat.mods", ("plugins", Chainloader.PluginInfos.Count), ("named", named));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("ModPresenceSetup.FejdStartup.Awake", e);
                }
            }
        }
    }
}
