using System.Collections.Generic;

namespace VikingsForHire.Core.Orders
{
    public enum StationKind
    {
        /// <summary>Cooking stations and the oven: raw item on, cooked item off.</summary>
        Stove,

        /// <summary>Cauldron, prep table, mead ketill: a recipe crafted from ingredients.</summary>
        Craft,
    }

    /// <summary>One way to make a kitchen item, described without game types.</summary>
    public sealed class KitchenInfo
    {
        public string Output { get; set; } = "";
        public int OutputAmount { get; set; } = 1;

        /// <summary>The station prefab (piece_cookingstation, piece_cauldron…).</summary>
        public string Station { get; set; } = "";

        public StationKind Kind { get; set; }

        /// <summary>Item -> amount for one batch (one raw item for a stove).</summary>
        public Dictionary<string, int> Inputs { get; set; } = new();

        /// <summary>Crafting: the station upgrade level the recipe needs.</summary>
        public int StationLevelNeeded { get; set; } = 1;

        /// <summary>The Cook level that may make it.</summary>
        public int Level { get; set; } = 1;

        /// <summary>Stoves: seconds until done.</summary>
        public float CookSeconds { get; set; }
    }

    /// <summary>The Cook's next job: make Batches of Info for the order of ForOrder; Then follows when this is an intermediate.</summary>
    public sealed class KitchenTask
    {
        public KitchenInfo Info { get; set; } = new();
        public int Batches { get; set; }
        public string ForOrder { get; set; } = "";
        public KitchenTask? Then { get; set; }
    }
}
