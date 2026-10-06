namespace VikingsForHire.Core.Orders
{
    /// <summary>What an order keeps in stock: seeds (always first, and a reserve), farm produce, or kitchen food.</summary>
    public enum OrderKind
    {
        Seed,
        Crop,
        Kitchen,
    }

    /// <summary>"Keep at least Target of Item in the chests."</summary>
    public sealed class ProductionOrder
    {
        public string Item { get; set; } = "";
        public int Target { get; set; }
        public bool Paused { get; set; }
        public OrderKind Kind { get; set; }

        public bool IsFarm => Kind != OrderKind.Kitchen;
    }
}
