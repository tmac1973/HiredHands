using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Commands;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Testing
{
    internal static class FixturesHireling
    {
        public static void Register()
        {
            Fixtures.Add("hirelings", "<job> <level> <n=1> - spawn hirelings 4m ahead, linked to the nearest board", Hirelings);
            Fixtures.Add("snapshot_test", "- run vfh_snapshot_test on the nearest hireling and wait for it", _ => SnapshotTest.Run(HirelingCommands.NearestOrThrow()));
            Fixtures.Add("cargo_put", "<item> <stacks> [per stack] - move stacks of an item (full ones unless given) from you into the nearest hireling's cargo, like the UI", CargoPut);
            Fixtures.Add("kill_hirelings", "<radius=50> [graves] - kill hirelings near you; their cargo is emptied first (no graves or map pins) unless 'graves'", args =>
            {
                float radius = args.Length > 0 && float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : 50f;
                // Test cleanup: an emptied hireling leaves no grave, so test runs don't pile up graves and their map pins.
                if (!args.Contains("graves") && Player.m_localPlayer != null)
                    foreach (Hireling h in Hireling.Loaded.Where(h => h != null && h.CargoInventory != null &&
                                                                     Vector3.Distance(h.transform.position, Player.m_localPlayer.transform.position) <= radius))
                        h.CargoInventory!.RemoveAll();
                HirelingCommands.Kill(radius);
                return Wait(1f);
            });

            TestHarness.RegisterCheck("hireling_count", "[job] - loaded hirelings within 50m (optionally of one job)", args =>
                Hireling.Loaded.Count(h => h != null && h.Zdo != null && Vector3.Distance(h.transform.position, Player.m_localPlayer.transform.position) <= 50f &&
                                           (args.Length == 0 || string.Equals(h.Job.ToString(), args[0], StringComparison.OrdinalIgnoreCase))).ToString());
            Fixtures.Add("chatter_now", "<last|posted|nearest> - that hireling says something now", args =>
            {
                VikingsForHire.Hirelings.HirelingChatter.Say(Select(args.ElementAtOrDefault(0) ?? "nearest"), Player.m_localPlayer);
                return Delay(0.2f);
            });
            Fixtures.Add("regear", "- dress the hireling from your last contract again now, from the current data (as when it loads)", _ => Regear());
            TestHarness.RegisterCheck("hireling", "<last|posted|nearest|short-hid> <field> - last = most recently spawned, posted = from your last contract; name, job, level, health, maxhealth, charlevel, tamed, faction, armor, behaviour, post_dist, posted, follow_mode, retreat, owner_dist (from you), order, gear.right|left|helmet|chest|legs|ammo|sidearm (none when empty), cargo.<item>, cargo_used, cargo_slots, cargo_weight, or any vfh_ key",
                args => Field(Select(args.ElementAtOrDefault(0) ?? "nearest"), args.ElementAtOrDefault(1) ?? ""));
            TestHarness.RegisterCheck("snapshot_roundtrip", "- result of the last snapshot test: true, or false with the differing values", _ => SnapshotTest.LastResult);
        }

        private static IEnumerator Delay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        private static IEnumerator Regear()
        {
            Hireling h = Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == Board.BoardContracts.LastPostedHid)
                         ?? throw new InvalidOperationException("the hireling from your last contract isn't loaded");
            GearApplier.Regear(h.Humanoid, h.Job, h.Level);
            VfhLog.I(LogCat.Test, "fixture.regear", ("hid", h.Hid), ("gear", GearApplier.Describe(h.Humanoid)));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator Hirelings(string[] args)
        {
            Player p = Player.m_localPlayer;
            Vector3? beside = FixturesWork.AtTag(ref args);
            Vector3 at = beside ?? p.transform.position + Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized * 4f;
            if (beside == null)
                at.y = ZoneSystem.instance.GetGroundHeight(at);
            HirelingCommands.Spawn(new[] { args.ElementAtOrDefault(0) ?? "Woodcutter", args.ElementAtOrDefault(1) ?? "1", args.ElementAtOrDefault(2) ?? "1" }, at);
            // Give ZNetScene a moment to instantiate them and Start to dress them.
            yield return new WaitForSeconds(1.5f);
        }

        private static IEnumerator CargoPut(string[] args)
        {
            Hireling h = HirelingCommands.NearestOrThrow();
            string prefab = args.ElementAtOrDefault(0) ?? "Wood";
            int stacks = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(prefab) ?? throw new InvalidOperationException($"no item {prefab}");
            int size = Math.Max(1, itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize);
            // A smaller stack when asked: a full stack of ore alone weighs a level 1 hireling's whole limit.
            if (args.Length > 2)
                size = Math.Min(size, Math.Max(1, int.Parse(args[2], CultureInfo.InvariantCulture)));
            Inventory player = Player.m_localPlayer.GetInventory();
            h.GetComponent<ZNetView>().ClaimOwnership();
            int moved = 0;
            for (int i = 0; i < stacks; i++)
            {
                ItemDrop.ItemData? item = player.AddItem(prefab, size, 1, 0, 0L, "", false);
                if (item == null)
                    break;
                h.CargoInventory!.MoveItemToThis(player, item);
                if (player.ContainsItem(item))
                {
                    player.RemoveItem(item);
                    break;
                }
                moved++;
            }
            VfhLog.I(LogCat.Test, "fixture.cargo_put", ("hid", h.Hid), ("item", prefab), ("asked", stacks), ("moved", moved), ("slots", h.CargoSlots));
            yield return null;
        }

        internal static Hireling Select(string selector)
        {
            if (selector == "nearest")
                return HirelingCommands.NearestOrThrow();
            if (selector == "posted")
            {
                string posted = Board.BoardContracts.LastPostedHid;
                return Hireling.Loaded.FirstOrDefault(h => h != null && h.Hid == posted)
                       ?? throw new InvalidOperationException($"the hireling from the last posted contract ({posted}) isn't here yet");
            }
            if (selector == "last")
            {
                string hid = HirelingCommands.LastSpawned.LastOrDefault() ?? throw new InvalidOperationException("nothing spawned yet");
                return Hireling.Loaded.FirstOrDefault(h => h != null && h.Hid == hid)
                       ?? throw new InvalidOperationException($"last spawned hireling {hid} isn't loaded");
            }
            return Hireling.Loaded.FirstOrDefault(h => h != null && h.Hid.StartsWith(selector, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"no loaded hireling with id {selector}…");
        }

        private static string Field(Hireling h, string field)
        {
            ZDO z = h.Zdo!;
            Humanoid hum = h.Humanoid;
            string f = field.ToLowerInvariant();
            if (f.StartsWith("gear."))
            {
                string gear = GearField(hum, h, f);
                return gear.Length == 0 ? "none" : gear;
            }
            if (f.StartsWith("cargo."))
                return (h.CargoInventory?.GetAllItems().Where(i => GearApplier.Name(i).Equals(field.Substring(6), StringComparison.OrdinalIgnoreCase)).Sum(i => i.m_stack) ?? 0).ToString();
            switch (f)
            {
                case "name": return h.DisplayName;
                case "job": return h.Job.ToString();
                case "level": return h.Level.ToString();
                case "mode": return h.Mode.ToString();
                case "stance": return ((Stance)z.GetInt(HirelingZdo.Stance)).ToString();
                case "health": return hum.GetHealth().ToString("0.#", CultureInfo.InvariantCulture);
                case "health_pct": return hum.GetHealthPercentage().ToString("0.00", CultureInfo.InvariantCulture);
                case "maxhealth": return hum.GetMaxHealth().ToString("0.#", CultureInfo.InvariantCulture);
                case "charlevel": return hum.GetLevel().ToString();
                case "tamed": return hum.IsTamed() ? "true" : "false";
                case "faction": return hum.GetFaction().ToString();
                case "armor": return hum.GetBodyArmor().ToString("0.#", CultureInfo.InvariantCulture);
                case "behaviour": return h.Ai.CurrentBehaviour;
                case "post_dist": return h.HasPost ? Utils.DistanceXZ(h.transform.position, h.PostPos).ToString("0.0", CultureInfo.InvariantCulture) : "none";
                case "follow_mode": return h.FollowMode.ToString();
                case "retreat": return h.Ai.RetreatOrdered ? "true" : "false";
                case "owner_dist": return Utils.DistanceXZ(h.transform.position, Player.m_localPlayer.transform.position).ToString("0.0", CultureInfo.InvariantCulture);
                case "posted": return h.HasPost ? "true" : "false";
                case "order": return h.Ai.Order?.Kind.ToString() ?? "none";
                case "said": return VikingsForHire.Hirelings.HirelingChatter.Said(h.Hid).Length > 0 ? "true" : "false";
                case "target": return h.Ai.CombatTarget != null ? h.Ai.CombatTarget.m_name : "none";
                case "cast_dist": return h.Ai.LastCastDistance < 0f ? "none" : h.Ai.LastCastDistance.ToString("0.0", CultureInfo.InvariantCulture);
                case "retreating": return h.Ai.Retreating ? "true" : "false";
                case "alive": return h.Humanoid.IsDead() ? "false" : "true";
                case "cargo_used": return (h.CargoInventory?.GetAllItems().Count ?? 0).ToString();
                case "cargo_slots": return h.CargoSlots.ToString();
                case "cargo_weight": return (h.CargoInventory?.GetTotalWeight() ?? 0f).ToString("0.#", CultureInfo.InvariantCulture);
            }
            string key = f.StartsWith("vfh_") ? f : "vfh_" + f;
            int hash = key.GetStableHashCode();
            if (ZDOExtraData.s_strings.TryGetValue(z.m_uid, out var s) && s.TryGetValue(hash, out string sv)) return sv;
            if (ZDOExtraData.s_ints.TryGetValue(z.m_uid, out var i32) && i32.TryGetValue(hash, out int iv)) return iv.ToString();
            if (ZDOExtraData.s_floats.TryGetValue(z.m_uid, out var fl) && fl.TryGetValue(hash, out float fv)) return fv.ToString(CultureInfo.InvariantCulture);
            if (ZDOExtraData.s_longs.TryGetValue(z.m_uid, out var lo) && lo.TryGetValue(hash, out long lv)) return lv.ToString();
            if (ZDOExtraData.s_vec3.TryGetValue(z.m_uid, out var v3) && v3.TryGetValue(hash, out Vector3 vv)) return vv.ToString();
            return $"error: unknown field {field}";
        }

        private static string GearField(Humanoid hum, Hireling h, string f) => f switch
        {
            "gear.right" => GearApplier.Name(hum.GetRightItem()),
            "gear.left" => GearApplier.Name(hum.GetLeftItem()),
            "gear.helmet" => GearApplier.Name(hum.m_helmetItem),
            "gear.chest" => GearApplier.Name(hum.m_chestItem),
            "gear.legs" => GearApplier.Name(hum.m_legItem),
            "gear.cape" => GearApplier.Name(hum.m_shoulderItem),
            "gear.ammo" => GearApplier.Name(hum.GetAmmoItem()),
            "gear.sidearm" => GearApplier.Name(GearApplier.Sidearm(hum, h.Job, h.Level)),
            _ => throw new ArgumentException($"unknown gear slot {f}"),
        };

        private static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
