using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>
    /// What a pickable gives, worked out the way Pickable does it but without a player: its own pick RPC reads the local
    /// player, which a dedicated server doesn't have.
    /// </summary>
    internal static class PickableYield
    {
        public static int Main(Pickable p) =>
            p.m_itemPrefab == null ? 0
            : p.m_dontScale ? p.m_amount
            : Mathf.Max(p.m_minAmountScaled, Game.instance != null ? Game.instance.ScaleDrops(p.m_itemPrefab, p.m_amount) : p.m_amount);

        /// <summary>The extra drops' expected count per item (for planning; picking rolls them for real).</summary>
        public static Dictionary<string, double> ExpectedExtras(Pickable p)
        {
            var result = new Dictionary<string, double>();
            DropTable t = p.m_extraDrops;
            if (t == null || t.m_drops == null || t.m_drops.Count == 0)
                return result;
            float total = t.m_drops.Sum(d => d.m_weight);
            double rolls = t.m_dropChance * (t.m_dropMin + t.m_dropMax) / 2.0;
            foreach (DropTable.DropData d in t.m_drops.Where(d => d.m_item != null))
            {
                double share = t.m_oneOfEach || total <= 0f ? 1.0 : d.m_weight / total;
                double n = rolls * share * (d.m_stackMin + d.m_stackMax) / 2.0;
                result[d.m_item.name] = (result.TryGetValue(d.m_item.name, out double x) ? x : 0d) + n;
            }
            return result;
        }

        public static bool Ripe(Pickable p) => p != null && !p.m_picked && p.m_enabled != 0;
    }
}
