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
            {
                // Sidearm first so the main weapon is the one left equipped; ammo last.
                names.AddRange(new[] { weapons.Sidearm, weapons.Main, weapons.Offhand, weapons.Ammo });
            }
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
