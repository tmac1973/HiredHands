using System.Linq;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// A smelter delivers the finished output it collected (bars, coal). Ore and fuel it carries stay with it for the
    /// stations, unless nothing has needed them for 5 minutes, or it's asked to deliver everything.
    /// </summary>
    internal sealed class SmelterDeliveryPolicy : IDeliveryPolicy
    {
        public const float LeftoverSeconds = 300f;

        public bool NeedsDelivery(Hireling h) =>
            h.CargoInventory != null && h.CargoInventory.GetAllItems().Any(i => i.m_dropPrefab != null && Delivers(h, i.m_dropPrefab.name));

        public bool Delivers(Hireling h, string prefab) =>
            h.DeliverPending || StationSurvey.AllOutputs().Contains(prefab) || LeftoversExpired(h);

        private static bool LeftoversExpired(Hireling h) => h.LeftoverSince > 0f && Time.time - h.LeftoverSince > LeftoverSeconds;
    }
}
