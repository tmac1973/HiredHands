using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jotunn.Managers;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Builds VFH_Hireling from a clone of the Player prefab: the same body, animations and equipment visuals, with the
    /// player-only machinery swapped out. The Player component is replaced by a plain Humanoid carrying every inherited
    /// Character/Humanoid setting (speeds, effects, unarmed weapon…) copied across, and a HirelingAI drives it.
    /// </summary>
    internal static class HirelingPrefab
    {
        public const string CargoInventoryName = "$vfh_hireling_cargo";
        public const int CargoWidth = 8;
        public const int CargoHeight = 4;

        /// <summary>Player-only components removed from the clone (by type name, wherever they are in the hierarchy).</summary>
        private static readonly HashSet<string> PlayerOnly = new()
        {
            "Player", "PlayerController", "Talker", "Skills", "AudioListener", "Camera", "PlayerCustomizaton",
        };

        private static GameObject? _prefab;

        public static void Register() =>
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Hireling, "hireling.prefab_failed", Build);

        private static void Build()
        {
            if (_prefab != null)
                return;

            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(HirelingZdo.PrefabName, "Player");
            Player player = prefab.GetComponent<Player>();
            Dictionary<FieldInfo, object?> inherited = InheritedFields(player);

            // Dependents first: Unity won't remove Player while a [RequireComponent(typeof(Player))] component remains.
            var removed = new List<string>();
            foreach (Component c in prefab.GetComponentsInChildren<Component>(true)
                         .Where(c => c != null && PlayerOnly.Contains(c.GetType().Name))
                         .OrderBy(c => c is Player ? 1 : 0).ToList())
            {
                removed.Add(c.GetType().Name + "@" + c.gameObject.name);
                Object.DestroyImmediate(c);
            }
            if (prefab.GetComponent<Player>() != null)
            {
                VfhLog.E(LogCat.Hireling, "hireling.prefab_player_kept", ("components", string.Join(",", prefab.GetComponents<Component>().Select(c => c.GetType().Name))));
                return;
            }

            Humanoid humanoid = prefab.AddComponent<Humanoid>();
            foreach (KeyValuePair<FieldInfo, object?> f in inherited)
                f.Key.SetValue(humanoid, f.Value);
            humanoid.m_name = "$vfh_hireling";
            humanoid.m_group = "VFH_Hireling";
            humanoid.m_faction = Character.Faction.Players;
            humanoid.m_boss = false;
            humanoid.m_tolerateWater = true;
            humanoid.m_defaultItems = Array.Empty<GameObject>();
            humanoid.m_randomWeapon = Array.Empty<GameObject>();
            humanoid.m_randomArmor = Array.Empty<GameObject>();
            humanoid.m_randomShield = Array.Empty<GameObject>();
            humanoid.m_randomSets = Array.Empty<Humanoid.ItemSet>();
            humanoid.m_randomItems = Array.Empty<Humanoid.RandomItem>();

            HirelingAI ai = prefab.AddComponent<HirelingAI>();
            ai.m_viewRange = 30f;
            ai.m_viewAngle = 90f;
            ai.m_hearRange = 20f;
            ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
            ai.m_randomMoveRange = 5f;
            ai.m_randomMoveInterval = 6f;
            ai.m_moveMinAngle = 10f;
            ai.m_avoidFire = true;
            ai.m_afraidOfFire = false;
            ai.m_avoidWater = true;
            ai.m_jumpInterval = 0f;
            ai.m_circulateWhileCharging = false;

            AddCargo(prefab);
            prefab.AddComponent<Hireling>();

            ZNetView nview = prefab.GetComponent<ZNetView>();
            nview.m_persistent = true;
            nview.m_type = ZDO.ObjectType.Default;
            // Players sit on the "player" layer, which the interact raycast ignores; NPCs live on "character".
            prefab.layer = LayerMask.NameToLayer("character");

            PrefabManager.Instance.AddPrefab(prefab);
            _prefab = prefab;

            VfhLog.I(LogCat.Hireling, "hireling.prefab", ("removed", string.Join(",", removed)), ("copiedFields", inherited.Count),
                ("components", string.Join(",", prefab.GetComponents<Component>().Select(c => c.GetType().Name))));
            VfhLog.D(LogCat.Hireling, "hireling.prefab_children", ("tree", string.Join(",", prefab.GetComponentsInChildren<Component>(true)
                .Where(c => c.gameObject != prefab && c is not Transform).Select(c => $"{c.gameObject.name}:{c.GetType().Name}").Distinct().Take(80))));
        }

        /// <summary>Every serialized Character and Humanoid field on the player, to copy onto the replacement Humanoid.</summary>
        private static Dictionary<FieldInfo, object?> InheritedFields(Player player)
        {
            var result = new Dictionary<FieldInfo, object?>();
            foreach (Type t in new[] { typeof(Character), typeof(Humanoid) })
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsInitOnly || f.IsLiteral || f.IsNotSerialized)
                        continue;
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null)
                        continue;
                    result[f] = f.GetValue(player);
                }
            }
            return result;
        }

        /// <summary>The cargo lives on a child, like the board's storage, so hovering the hireling finds Hireling.</summary>
        private static void AddCargo(GameObject prefab)
        {
            var child = new GameObject("VFH_Cargo");
            child.transform.SetParent(prefab.transform, false);
            Container cargo = child.AddComponent<Container>();
            cargo.m_name = CargoInventoryName;
            cargo.m_width = CargoWidth;
            cargo.m_height = CargoHeight;
            cargo.m_privacy = Container.PrivacySetting.Public;
            cargo.m_checkGuardStone = false;
            cargo.m_rootObjectOverride = prefab.GetComponent<ZNetView>();
            Container? chest = PrefabManager.Instance.GetPrefab("piece_chest_wood")?.GetComponent<Container>();
            if (chest != null)
            {
                cargo.m_bkg = chest.m_bkg;
                cargo.m_openEffects = chest.m_openEffects;
                cargo.m_closeEffects = chest.m_closeEffects;
            }
        }
    }
}
