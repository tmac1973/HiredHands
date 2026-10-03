using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;

namespace VikingsForHire.Diagnostics
{
    /// <summary>The block at the top of each session (and of vfh_dump_state) that makes a log self-describing.</summary>
    internal static class SessionInfo
    {
        public static void LogHeader(string reason)
        {
            VfhLog.I(LogCat.Core, "session.header", ("reason", reason), ("mod", Plugin.Version), ("game", Version.GetVersionString()),
                ("role", VfhLog.Role()), ("world", WorldName()), ("logFile", VfhLog.FilePath ?? "off"));
            VfhLog.I(LogCat.Core, "session.mods", ("count", Chainloader.PluginInfos.Count),
                ("list", string.Join(",", Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.GUID).Select(p => $"{p.Metadata.GUID}@{p.Metadata.Version}"))));
            VfhLog.I(LogCat.Core, "session.config", ("debug", VfhConfig.DebugCategories.Value), ("trace", VfhConfig.TraceCategories.Value),
                ("fastTimers", VfhConfig.FastTimers), ("dataSource", DataStore.Source), ("dataHash", DataStore.Hash),
                ("compat", string.Join(",", Compat.CompatPatcher.Status)));
            VfhLog.Raw("[VFH] config: " + string.Join(" ", VfhConfig.AllEffective().Select(c => c.Key.Split('/')[1] + "=" + c.Value)));
        }

        private static string WorldName()
        {
            if (ZNet.instance == null)
                return "none";
            return ZNet.instance.IsServer() ? ZNet.instance.GetWorldName() : "remote";
        }

        /// <summary>A header each time a world starts (single-player, hosting, joining or dedicated server start).</summary>
        [HarmonyPatch(typeof(ZNet), "Start")]
        private static class ZNetStartPatch
        {
            private static void Postfix() => VfhLog.Guard(LogCat.Core, "session.header_failed", () => LogHeader("world start"));
        }

        [HarmonyPatch(typeof(ZNet), "OnDestroy")]
        private static class ZNetDestroyPatch
        {
            private static void Postfix() => VfhLog.Guard(LogCat.Net, "session.leave_failed", () =>
            {
                VfhLog.I(LogCat.Core, "session.leave");
                Testing.TestHarness.Abort("left the world");
                DataStore.OnDisconnected();
                if (VfhConfig.FastTimers)
                    VfhConfig.SetFastTimers(false);
            });
        }
    }
}
