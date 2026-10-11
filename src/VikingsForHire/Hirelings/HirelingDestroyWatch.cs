using System;
using HarmonyLib;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Logs any deletion of a living hireling's world object, with what called it. A follower vanished in a crypt with no
    /// death and nothing in the logs (2026-10-10); deaths, departures and our own packing are expected, anything else is
    /// what we need to see.
    /// </summary>
    internal static class HirelingDestroyWatch
    {
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.DestroyZDO))]
        private static class Patch
        {
            private static void Prefix(ZDO zdo)
            {
                try
                {
                    if (zdo == null || zdo.GetPrefab() != HirelingZdo.PrefabHash)
                        return;
                    var mode = (HirelingMode)zdo.GetInt(HirelingZdo.Mode);
                    float health = zdo.GetFloat(ZDOVars.s_health, 1f);
                    if (health <= 0f || mode == HirelingMode.Leaving)
                        return; // a death or a departure: logged where they happen
                    string trace = Environment.StackTrace;
                    VfhLog.W(LogCat.Hireling, "hireling.zdo_destroyed", ("hid", zdo.GetString(HirelingZdo.Hid)), ("name", zdo.GetString(HirelingZdo.Name)),
                        ("mode", mode), ("pos", zdo.GetPosition()), ("owner", zdo.GetOwner()), ("health", health),
                        ("by", trace.Length > 1500 ? trace.Substring(0, 1500) : trace));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("HirelingDestroyWatch.DestroyZDO", e);
                }
            }
        }
    }
}
