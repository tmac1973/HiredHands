using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// A puff of dust at a chest when a hireling puts something in or takes something out, so you can see who's at your
    /// chests. The chest's own network object tells every game that has it loaded (everyone nearby) to show it; each game
    /// shows it unless its player turned ChestPuffs off. The puff is the chest's own "placed" dust (its visual effect,
    /// not the sound), or a wood-placing puff for a chest without one. At most one a second per chest.
    /// </summary>
    internal static class ChestPuff
    {
        private const string Rpc = "VFH_ChestPuff";
        private const float MinSeconds = 1f;
        private static readonly string[] Fallbacks = { "vfx_Place_wood_wall", "vfx_Place_wood_floor", "vfx_Place_chest" };
        private static readonly Dictionary<Container, float> LastPuff = new();

        /// <summary>A hireling just moved items in or out of this chest (on the game simulating it).</summary>
        public static void Play(Container chest)
        {
            ZNetView? view = chest != null ? chest.m_nview : null;
            if (view == null || !view.IsValid())
                return;
            if (LastPuff.TryGetValue(chest!, out float at) && Time.time - at < MinSeconds)
                return;
            LastPuff[chest!] = Time.time;
            if (LastPuff.Count > 200)
                foreach (Container stale in LastPuff.Keys.Where(k => k == null || Time.time - LastPuff[k] > 60f).ToList())
                    LastPuff.Remove(stale);
            view.InvokeRPC(ZNetView.Everybody, Rpc);
        }

        [HarmonyPatch(typeof(Container), "Awake")]
        private static class RegisterPatch
        {
            private static void Postfix(Container __instance)
            {
                try
                {
                    ZNetView? view = __instance.m_nview;
                    // Not for a placement ghost (no ZDO), and once per network object (a piece with two chests on one).
                    if (view == null || view.GetZDO() == null || view.m_functions.ContainsKey(Rpc.GetStableHashCode()))
                        return;
                    view.Register(Rpc, _ => Show(__instance));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("ChestPuff.Container.Awake", e);
                }
            }
        }

        private static void Show(Container chest)
        {
            if (chest == null || !VfhConfig.ChestPuffs.Value)
                return;
            Transform at = chest.m_rootObjectOverride != null ? chest.m_rootObjectOverride.transform : chest.transform;
            List<GameObject> puffs = chest.GetComponentInParent<Piece>()?.m_placeEffect?.m_effectPrefabs?
                .Where(e => e != null && e.m_enabled && e.m_prefab != null && e.m_prefab.name.StartsWith("vfx", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.m_prefab).ToList() ?? new List<GameObject>();
            if (puffs.Count == 0 && ZNetScene.instance != null)
                puffs = Fallbacks.Select(n => ZNetScene.instance.GetPrefab(n)).Where(p => p != null).Take(1).ToList()!;
            foreach (GameObject p in puffs)
                UnityEngine.Object.Instantiate(p, at.position, at.rotation);
        }
    }
}
