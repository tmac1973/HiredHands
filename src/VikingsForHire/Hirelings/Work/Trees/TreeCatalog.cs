using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Trees
{
    /// <summary>A tree the cultivator can plant: its sapling, the seed it takes, and what it needs to grow.</summary>
    internal sealed class TreeKind
    {
        public GameObject Sapling = null!;
        public string SaplingName = "";
        public string Seed = "";
        public int SeedAmount = 1;
        public string Grown = "";
        public Heightmap.Biome Biome;
        public float GrowRadius = 2f;
        public bool NeedCultivated;
        public bool TolerateCold, TolerateHeat;

        /// <summary>The axe tier it takes to fell the grown tree.</summary>
        public int ToolTier;
    }

    /// <summary>
    /// Every tree sapling in the cultivator's piece table (vanilla and modded, e.g. PlantEverything's), read when first
    /// needed in a world.
    /// </summary>
    internal static class TreeCatalog
    {
        private static readonly List<TreeKind> _all = new();
        private static readonly HashSet<int> _saplingHashes = new();
        private static float _nextTry;

        public static IReadOnlyList<TreeKind> All
        {
            get
            {
                if (_all.Count == 0 && Time.time >= _nextTry)
                {
                    _nextTry = Time.time + 5f;
                    Build();
                }
                return _all;
            }
        }

        public static TreeKind? BySapling(string prefab) => All.FirstOrDefault(t => t.SaplingName == prefab);

        public static bool IsSaplingHash(int hash)
        {
            _ = All;
            return _saplingHashes.Contains(hash);
        }

        private static void Build()
        {
            ItemDrop? cultivator = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Cultivator")?.GetComponent<ItemDrop>() : null;
            if (cultivator == null || ZNetScene.instance == null)
                return;
            _all.Clear();
            _saplingHashes.Clear();
            foreach (GameObject go in cultivator.m_itemData.m_shared.m_buildPieces?.m_pieces ?? new List<GameObject>())
            {
                if (go == null || go.GetComponent<Plant>() is not Plant plant)
                    continue;
                GameObject? grown = plant.m_grownPrefabs?.FirstOrDefault(g => g != null);
                TreeBase? tree = grown != null ? grown.GetComponent<TreeBase>() : null;
                Piece.Requirement? seed = go.GetComponent<Piece>()?.m_resources?.FirstOrDefault(r => r?.m_resItem != null);
                if (tree == null || seed == null)
                    continue;
                _all.Add(new TreeKind
                {
                    Sapling = go, SaplingName = go.name, Seed = seed.m_resItem.name, SeedAmount = Mathf.Max(1, seed.m_amount),
                    Grown = grown!.name, Biome = plant.m_biome, GrowRadius = plant.m_growRadius, NeedCultivated = plant.m_needCultivatedGround,
                    TolerateCold = plant.m_tolerateCold, TolerateHeat = plant.m_tolerateHeat, ToolTier = tree.m_minToolTier,
                });
                _saplingHashes.Add(go.name.GetStableHashCode());
            }
            VfhLog.I(LogCat.Work, "trees.built", ("kinds", string.Join(",", _all.Select(t => $"{t.SaplingName}({t.Seed},tier {t.ToolTier})"))));
        }
    }
}
