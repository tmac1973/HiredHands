using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// A dead hireling's cargo goes into a grave (the vanilla tombstone) named after it, instead of a loose pile that
    /// vanilla clears after a while. The grave floats and lasts like a player's and goes away once emptied. Its vanilla
    /// owner is left empty, so nobody gets the "corpse run" boost from it; who may open it is ours to decide: a
    /// follower's grave only its owner, a base worker's grave anyone with ward access there. The follower's owner gets a
    /// map pin on it for the session, removed once they've emptied it. HirelingTombstones off keeps the loose pile.
    /// </summary>
    internal static class Graves
    {
        private const string Prefab = "Player_tombstone";
        private const string OwnerKey = "vfh_grave_owner";
        private const string HidKey = "vfh_grave_hid";
        private static readonly Dictionary<Vector3, Minimap.PinData> Pins = new();
        private static float _nextPinCheck;

        /// <summary>Owner side, as it dies: put its cargo in a grave. Returns the stacks buried, or -1 to drop them instead.</summary>
        public static int Bury(Hireling h, Inventory cargo, Vector3 at)
        {
            if (!VfhConfig.HirelingTombstones.Value || cargo.NrOfItems() == 0)
                return -1;
            GameObject? prefab = ZNetScene.instance?.GetPrefab(Prefab);
            if (prefab == null)
                return -1;
            GameObject go = UnityEngine.Object.Instantiate(prefab, at + Vector3.up, Quaternion.identity);
            TombStone? stone = go.GetComponent<TombStone>();
            Container? box = go.GetComponent<Container>();
            ZDO? zdo = go.GetComponent<ZNetView>()?.GetZDO();
            if (stone == null || box == null || zdo == null)
            {
                UnityEngine.Object.Destroy(go);
                return -1;
            }
            long owner = h.Mode == Core.HirelingMode.Following ? h.OwnerId : 0L;
            stone.Setup(h.DisplayName, 0L);
            zdo.Set(OwnerKey, owner);
            zdo.Set(HidKey, h.Hid);
            Inventory into = box.GetInventory();
            int stacks = 0;
            foreach (ItemDrop.ItemData item in cargo.GetAllItems().ToList())
            {
                if (into.AddItem(item.Clone()))
                {
                    cargo.RemoveItem(item);
                    stacks++;
                }
            }
            VfhLog.I(LogCat.Hireling, "hireling.grave", ("hid", h.Hid), ("pos", at), ("stacks", stacks), ("owner", owner));
            if (owner != 0L && Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == owner && Minimap.instance != null)
                Pins[at] = Minimap.instance.AddPin(at, Minimap.PinType.Icon3, Localization.instance.Localize("$vfh_grave_pin", h.DisplayName), save: false, isChecked: false);
            return stacks;
        }

        /// <summary>Tests: our graves within a radius of a point.</summary>
        public static List<TombStone> Near(Vector3 at, float radius) =>
            UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None)
                .Where(t => Vector3.Distance(t.transform.position, at) <= radius && t.GetComponent<ZNetView>()?.GetZDO()?.GetString(HidKey).Length > 0)
                .ToList();

        /// <summary>Every frame (Plugin.Update): drop the pins of graves you're near that are gone (emptied).</summary>
        public static void Tick()
        {
            if (Pins.Count == 0 || Time.time < _nextPinCheck || Player.m_localPlayer == null || Minimap.instance == null)
                return;
            _nextPinCheck = Time.time + 5f;
            Vector3 me = Player.m_localPlayer.transform.position;
            foreach (Vector3 at in Pins.Keys.Where(p => Vector3.Distance(p, me) < 30f).ToList())
            {
                bool stillThere = UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None)
                    .Any(t => Vector3.Distance(t.transform.position, at) < 10f && t.GetComponent<ZNetView>()?.GetZDO()?.GetString(HidKey).Length > 0);
                if (stillThere)
                    continue;
                Minimap.instance.RemovePin(Pins[at]);
                Pins.Remove(at);
            }
        }

        // A follower's grave opens only for its owner; a base worker's for anyone with ward access there.
        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Interact))]
        private static class InteractPatch
        {
            private static bool Prefix(TombStone __instance, Humanoid character, ref bool __result)
            {
                try
                {
                    ZDO? zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
                    if (zdo == null || zdo.GetString(HidKey).Length == 0 || character is not Player player)
                        return true;
                    long owner = zdo.GetLong(OwnerKey);
                    bool allowed = owner != 0L ? player.GetPlayerID() == owner : PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: true);
                    if (allowed)
                        return true;
                    if (owner != 0L)
                        player.Message(MessageHud.MessageType.Center, "$vfh_grave_not_yours");
                    __result = false;
                    return false;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("Graves.Interact", e);
                    return true;
                }
            }
        }
    }
}
