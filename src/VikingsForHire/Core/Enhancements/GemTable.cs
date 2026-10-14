using System;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>
    /// What a miner with Gem eye finds now and then in what it breaks, by the biome it's in (the game's biome names):
    /// Meadows and Black Forest flint and amber; Swamp amber and amber pearls; Mountain crystal and obsidian; Plains amber
    /// pearls and rubies; Mistlands and further rubies.
    /// </summary>
    public static class GemTable
    {
        public static string[] For(string biome) => biome switch
        {
            "Meadows" or "BlackForest" => new[] { "Flint", "Amber" },
            "Swamp" => new[] { "Amber", "AmberPearl" },
            "Mountain" => new[] { "Crystal", "Obsidian" },
            "Plains" => new[] { "AmberPearl", "Ruby" },
            "Mistlands" or "AshLands" or "DeepNorth" => new[] { "Ruby" },
            _ => new[] { "Flint", "Amber" },
        };

        /// <summary>One of the biome's finds, from a roll in [0, 1).</summary>
        public static string Pick(string biome, double roll)
        {
            string[] all = For(biome);
            int i = Math.Min(all.Length - 1, (int)(Math.Max(0.0, roll) * all.Length));
            return all[i];
        }
    }

    /// <summary>Which dropped items count as wood, fine wood and ore for the yield enhancements (by prefab name).</summary>
    public static class YieldKinds
    {
        public static bool Wood(string prefab) => prefab.EndsWith("Wood", StringComparison.Ordinal) || prefab == "RoundLog";

        public static bool FineWood(string prefab) => prefab == "FineWood" || prefab == "RoundLog";

        public static bool Ore(string prefab) =>
            prefab.EndsWith("Ore", StringComparison.Ordinal) || prefab.EndsWith("OreNew", StringComparison.Ordinal) || prefab.EndsWith("Scrap", StringComparison.Ordinal);

        /// <summary>The seed a felled tree gives back (Seed saver), from its prefab name; empty for a tree without one.</summary>
        public static string Seed(string tree)
        {
            if (tree.StartsWith("Beech", StringComparison.Ordinal)) return "BeechSeeds";
            if (tree.StartsWith("Birch", StringComparison.Ordinal)) return "BirchSeeds";
            if (tree.StartsWith("Oak", StringComparison.Ordinal)) return "Acorn";
            if (tree.StartsWith("FirTree", StringComparison.Ordinal)) return "FirCone";
            if (tree.StartsWith("Pinetree", StringComparison.Ordinal)) return "PineCone";
            return "";
        }
    }
}
