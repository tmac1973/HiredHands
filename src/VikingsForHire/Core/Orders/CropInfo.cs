using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Orders
{
    /// <summary>One thing a Farmer can grow or pick, described without game types (the game side fills it in).</summary>
    public sealed class CropInfo
    {
        /// <summary>The sapling prefab planted, or for a regrowing plant its pickable prefab.</summary>
        public string Plant { get; set; } = "";

        /// <summary>The item planting it uses up (a seed, or a carrot for seed carrots); empty for regrowing plants.</summary>
        public string Consumes { get; set; } = "";

        public int ConsumesAmount { get; set; } = 1;

        /// <summary>The main item a ripe plant gives, and how many.</summary>
        public string Yields { get; set; } = "";

        public int YieldPerPlant { get; set; } = 1;

        /// <summary>Other items it gives, with the expected number per plant (Poteitr's seeds).</summary>
        public Dictionary<string, double> ExtraYields { get; set; } = new();

        /// <summary>A bush, mushroom and the like: picked again and again, never planted by the Farmer.</summary>
        public bool Regrowing { get; set; }

        public int Level { get; set; } = 1;

        /// <summary>How many of an item one plant gives (0 when it doesn't give it).</summary>
        public double YieldOf(string item) =>
            string.Equals(Yields, item, StringComparison.Ordinal) ? YieldPerPlant : ExtraYields.TryGetValue(item, out double x) ? x : 0d;

        /// <summary>
        /// A seed: some crop uses it up to grow a different item, and it isn't food (CarrotSeeds, TurnipSeeds, KaleSeeds…).
        /// Carrots (food), Barley and OatSeeds (each only grows more of itself) are crop items.
        /// </summary>
        public static bool IsSeedItem(string item, IEnumerable<CropInfo> crops, Func<string, bool> isFood) =>
            !isFood(item) && crops.Any(c => !c.Regrowing && c.Consumes == item && c.Yields != item);
    }
}
