using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Compat
{
    /// <summary>
    /// With AzuCraftyBoxes installed, board upgrades count and take materials from the chests CraftyBoxes would pull from
    /// near the board (its own range, pull toggle and per-chest rules). Our board and cargo are never among them (see
    /// CompatPatcher). Everything is reached by reflection: if the API changes, upgrades fall back to the inventory only.
    /// </summary>
    internal static class CraftyBoxesCompat
    {
        public const string Guid = "Azumatt.AzuCraftyBoxes";

        private static bool _resolved;
        private static MethodInfo? _nearby, _canPull, _itemCount, _removeItem, _save, _prefabName;
        private static Func<float>? _range;

        public static bool Active => VfhConfig.UpgradeFromNearbyChests.Value && Resolve();

        private static bool Resolve()
        {
            if (_resolved)
                return _nearby != null;
            _resolved = true;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
                return false;
            try
            {
                Assembly asm = info.Instance.GetType().Assembly;
                Type? api = asm.GetType("AzuCraftyBoxes.API");
                Type? box = asm.GetType("AzuCraftyBoxes.IContainers.IContainer");
                FieldInfo? range = info.Instance.GetType().GetField("mRange", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo? nearby = api?.GetMethod("GetNearbyContainers", BindingFlags.Static | BindingFlags.Public)?.MakeGenericMethod(typeof(Component));
                _canPull = api == null ? null : AccessTools.Method(api, "CanItemBePulled", new[] { typeof(string), typeof(string) });
                _itemCount = box == null ? null : AccessTools.Method(box, "ItemCount", new[] { typeof(string) });
                _removeItem = box == null ? null : AccessTools.Method(box, "RemoveItem", new[] { typeof(string), typeof(int) });
                _save = box == null ? null : AccessTools.Method(box, "Save");
                _prefabName = box == null ? null : AccessTools.Method(box, "GetPrefabName");
                if (range?.GetValue(null) is BepInEx.Configuration.ConfigEntry<float> entry)
                    _range = () => entry.Value;
                if (nearby != null && _canPull != null && _itemCount != null && _removeItem != null && _save != null && _prefabName != null && _range != null)
                    _nearby = nearby;
            }
            catch (Exception e)
            {
                VfhLog.W(LogCat.Core, "compat.craftyboxes_failed", ("error", e.Message));
            }
            VfhLog.I(LogCat.Core, "compat.craftyboxes", ("ok", _nearby != null));
            return _nearby != null;
        }

        /// <summary>The chests near <paramref name="at"/> that may give up <paramref name="itemPrefab"/>.</summary>
        private static List<object> Sources(Component at, string itemPrefab)
        {
            var list = new List<object>();
            if (!Active || at == null)
                return list;
            try
            {
                // CraftyBoxes hands back a list it reuses, so copy it before touching anything.
                if (_nearby!.Invoke(null, new object[] { at, _range!() }) is not IEnumerable found)
                    return list;
                foreach (object c in found)
                {
                    string prefab = (string)_prefabName!.Invoke(c, null);
                    if (!ExcludedContainers.IsExcluded(prefab) && _canPull!.Invoke(null, new object[] { prefab, itemPrefab }) is true)
                        list.Add(c);
                }
            }
            catch (Exception e)
            {
                VfhLog.W(LogCat.Core, "compat.craftyboxes_query_failed", ("error", e.Message));
                list.Clear();
            }
            return list;
        }

        /// <summary>How many of the item the nearby chests hold (0 without CraftyBoxes).</summary>
        public static int Count(Component at, string sharedName, string itemPrefab) =>
            Sources(at, itemPrefab).Sum(c => (int)_itemCount!.Invoke(c, new object[] { sharedName }));

        /// <summary>Takes up to <paramref name="amount"/> from the nearby chests. Returns how many were taken.</summary>
        public static int Take(Component at, string sharedName, string itemPrefab, int amount)
        {
            int taken = 0;
            foreach (object c in Sources(at, itemPrefab))
            {
                if (taken >= amount)
                    break;
                int n = Math.Min(amount - taken, (int)_itemCount!.Invoke(c, new object[] { sharedName }));
                if (n <= 0)
                    continue;
                _removeItem!.Invoke(c, new object[] { sharedName, n });
                _save!.Invoke(c, null);
                taken += n;
            }
            return taken;
        }
    }
}
