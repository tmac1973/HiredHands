using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Cosmetic gear by level and job. It goes in as the Humanoid's default items, the same way vanilla NPCs get their
    /// weapons: every client equips them locally, and the owner's equipment visuals sync through VisEquipment. There's no
    /// CharacterDrop on hirelings, so none of it can ever drop or be looted.
    /// </summary>
    internal static class GearApplier
    {
        public const int AmmoStack = 100;
        public const int AmmoRefillBelow = 20;

        public static GameObject[] GearFor(JobType job, int level)
        {
            VfhData data = DataStore.Current;
            var rules = new LevelRules(data);
            var names = new List<string>();
            // The job's own outfit (a wizard's robes) where it has one for the level, piece by piece over the shared armour.
            names.AddRange(GearRules.Armor(level, rules.Outfit(job), data.ArmorSets, Exists));
            WeaponSetData? weapons = rules.Gear(job).FirstOrDefault(g => g.Level == level);
            if (job == JobType.Wizard)
            {
                // A staff whatever happens: a removed mod's staff falls back to the vanilla one for the level.
                string staff = GearRules.Staff(level, rules.Gear(job), rules.GearWithoutMods(job), Exists, out bool fellBack);
                if (fellBack)
                    VfhLog.I(LogCat.Hireling, "gear.fallback", ("job", job), ("level", level), ("wanted", weapons?.Main ?? ""), ("used", staff));
                names.Add(staff);
            }
            else if (weapons != null)
                names.AddRange(new[] { weapons.Main, weapons.Offhand, weapons.Ammo });
            return names.Where(n => !string.IsNullOrEmpty(n))
                .Select(n => ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(n) : null)
                .Where(p => p != null).Select(p => p!).ToArray();
        }

        private static bool Exists(string prefab) => ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(prefab) != null;

        /// <summary>Before Humanoid.Start: vanilla then equips these exactly like a monster's weapons.</summary>
        public static void Prepare(Humanoid humanoid, JobType job, int level) => humanoid.m_defaultItems = GearFor(job, level);

        /// <summary>Re-dresses a live hireling after a promotion or job change.</summary>
        public static void Regear(Humanoid humanoid, JobType job, int level)
        {
            humanoid.UnequipAllItems();
            humanoid.GetInventory().RemoveAll();
            humanoid.m_defaultItems = GearFor(job, level);
            humanoid.GiveDefaultItems();
            // Vanilla doesn't always equip a two-handed weapon straight after unequipping everything (a staff stayed in the
            // inventory): equip the main weapon now rather than wait for the hireling's next armed check.
            if (humanoid.GetRightItem() == null && humanoid.GetInventory().GetAllItems()
                    .FirstOrDefault(i => i.IsWeapon() && i.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield) is ItemDrop.ItemData weapon)
                humanoid.EquipItem(weapon, triggerEquipEffects: false);
            AddSidearm(humanoid, job, level);
        }

        /// <summary>
        /// The sidearm (an archer's club) is carried, not equipped: vanilla equips every default item, so it goes into the
        /// inventory separately. Combat (phase 07) swaps to it when an enemy gets close.
        /// </summary>
        public static void AddSidearm(Humanoid humanoid, JobType job, int level)
        {
            WeaponSetData? weapons = DataStore.Current.Jobs.TryGetValue(job, out JobData? j) ? j.Gear.FirstOrDefault(g => g.Level == level) : null;
            if (weapons == null || string.IsNullOrEmpty(weapons.Sidearm) || ObjectDB.instance == null)
                return;
            GameObject? prefab = ObjectDB.instance.GetItemPrefab(weapons.Sidearm);
            if (prefab == null || humanoid.GetInventory().GetAllItems().Any(i => i.m_dropPrefab == prefab))
                return;
            humanoid.GetInventory().AddItem(prefab, 1);
        }

        public static ItemDrop.ItemData? Sidearm(Humanoid humanoid, JobType job, int level)
        {
            WeaponSetData? weapons = DataStore.Current.Jobs.TryGetValue(job, out JobData? j) ? j.Gear.FirstOrDefault(g => g.Level == level) : null;
            return weapons == null || string.IsNullOrEmpty(weapons.Sidearm)
                ? null
                : humanoid.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == weapons.Sidearm);
        }

        /// <summary>Ranged guards never run out of arrows.</summary>
        public static void RefillAmmo(Humanoid humanoid)
        {
            ItemDrop.ItemData? ammo = humanoid.GetAmmoItem();
            if (ammo != null && ammo.m_stack < AmmoRefillBelow)
                ammo.m_stack = AmmoStack;
        }

        public static string Describe(Humanoid h) => string.Join(",", new[]
        {
            Name(h.GetRightItem()), Name(h.GetLeftItem()), Name(h.m_helmetItem), Name(h.m_chestItem), Name(h.m_legItem), Name(h.m_shoulderItem), Name(h.GetAmmoItem()),
        }.Where(n => n.Length > 0));

        public static string Name(ItemDrop.ItemData? item) => item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";
    }
}
