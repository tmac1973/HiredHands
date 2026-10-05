using System.Linq;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// A smelter delivers the finished products it collected (bars). Ore and fuel it carries, including coal from a
    /// kiln, stay with it for the stations, unless nothing has needed them for 5 minutes, or it's asked to deliver
    /// everything. (Delivering something it also fetches would make it carry the same coal back and forth forever.)
    /// </summary>
    internal sealed class SmelterDeliveryPolicy : IDeliveryPolicy
    {
        public const float LeftoverSeconds = 300f;

        /// <summary>What the Steward's chores collect (honey, sap…): delivered like the stations' products.</summary>
        public static readonly System.Collections.Generic.HashSet<string> StewardOutputs = new();

        public bool NeedsDelivery(Hireling h) =>
            h.CargoInventory != null && !(h.HoldDeliveries && h.CargoInventory.NrOfItems() < h.CargoSlots) && h.CargoInventory.GetAllItems().Any(i => i.m_dropPrefab != null && Delivers(h, i.m_dropPrefab.name));

        public bool Delivers(Hireling h, string prefab) =>
            h.DeliverPending || StationSurvey.Products().Contains(prefab) || (StewardOutputs.Contains(prefab) && !StationSurvey.Consumed().Contains(prefab)) || LeftoversExpired(h);

        private static bool LeftoversExpired(Hireling h) => h.LeftoverSince > 0f && Time.time - h.LeftoverSince > LeftoverSeconds;
    }
}
