using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
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
            var names = new List<string>();
            ArmorSetData? armor = data.ArmorSets.FirstOrDefault(a => a.Level == level);
            if (armor != null)
                names.AddRange(new[] { armor.Helmet, armor.Chest, armor.Legs });
            WeaponSetData? weapons = data.Jobs.TryGetValue(job, out JobData? j) ? j.Gear.FirstOrDefault(g => g.Level == level) : null;
            if (weapons != null)
                names.AddRange(new[] { weapons.Main, weapons.Offhand, weapons.Ammo });
            return names.Where(n => !string.IsNullOrEmpty(n))
                .Select(n => ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(n) : null)
                .Where(p => p != null).Select(p => p!).ToArray();
        }

        /// <summary>Before Humanoid.Start: vanilla then equips these exactly like a monster's weapons.</summary>
        public static void Prepare(Humanoid humanoid, JobType job, int level) => humanoid.m_defaultItems = GearFor(job, level);

        /// <summary>Re-dresses a live hireling after a promotion or job change.</summary>
        public static void Regear(Humanoid humanoid, JobType job, int level)
        {
            humanoid.UnequipAllItems();
            humanoid.GetInventory().RemoveAll();
            humanoid.m_defaultItems = GearFor(job, level);
            humanoid.GiveDefaultItems();
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
            Name(h.GetRightItem()), Name(h.GetLeftItem()), Name(h.m_helmetItem), Name(h.m_chestItem), Name(h.m_legItem), Name(h.GetAmmoItem()),
        }.Where(n => n.Length > 0));

        public static string Name(ItemDrop.ItemData? item) => item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";
    }
}
