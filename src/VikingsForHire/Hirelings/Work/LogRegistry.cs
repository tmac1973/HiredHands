using System;
using System.Collections.Generic;
using HarmonyLib;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Every fallen log (TreeLog) in the loaded world. Woodcutters look logs up here instead of through a physics
    /// query: logs aren't on the layers that query covers, so woodcutters used to chop trees and stumps and walk past
    /// the logs, which hold most of a tree's wood.
    /// </summary>
    internal static class LogRegistry
    {
        private static readonly HashSet<TreeLog> Logs = new();

        public static IEnumerable<TreeLog> Within(Vector3 center, float radius)
        {
            Logs.RemoveWhere(l => l == null);
            float sq = radius * radius;
            foreach (TreeLog log in Logs)
                if ((log.transform.position - center).sqrMagnitude <= sq)
                    yield return log;
        }

        [HarmonyPatch(typeof(TreeLog), "Awake")]
        private static class AwakePatch
        {
            private static void Postfix(TreeLog __instance)
            {
                try
                {
                    Logs.Add(__instance);
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("LogRegistry.Awake", e);
                }
            }
        }
    }
}
