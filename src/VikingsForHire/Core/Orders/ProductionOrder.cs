namespace VikingsForHire.Core.Orders
{
    /// <summary>
    /// What an order is about: seeds (always first, and a reserve), farm produce or kitchen food ("keep at least"), a
    /// Steward's station product ("stop at": no more is made once the chests hold Target), or the Rancher's: meat and
    /// eggs ("keep at least") and a breeding pool (Herd: keep Target of an animal kind alive, at least 2).
    /// </summary>
    public enum OrderKind
    {
        Seed,
        Crop,
        Kitchen,
        Station,
        Meat,
        Egg,
        Herd,
    }

    /// <summary>"Keep at least Target of Item in the chests" (a Station order: "make no more once there are Target").</summary>
    public sealed class ProductionOrder
    {
        public string Item { get; set; } = "";
        public int Target { get; set; }
        public bool Paused { get; set; }
        public OrderKind Kind { get; set; }

        public bool IsFarm => Kind is OrderKind.Seed or OrderKind.Crop;

        public bool IsRanch => Kind is OrderKind.Meat or OrderKind.Egg or OrderKind.Herd;

        /// <summary>Which list it's in, for moving up and down: farm, kitchen, Steward or ranch.</summary>
        public int Group => IsFarm ? 0 : Kind == OrderKind.Kitchen ? 1 : IsRanch ? 3 : 2;
    }
}
