using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Orders;
using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// The Steward's limits, set in its Shift+E panel and kept on its board (so a new Steward keeps them): "make no more
    /// once the chests hold this many" of what its stations, beehives, sap collectors and fermenters make. An item with
    /// no limit is made as before.
    /// </summary>
    internal static class StewardLimits
    {
        /// <summary>Something a Steward can be limited on, and the chore key or station that gates it (for its level).</summary>
        internal sealed class Product
        {
            public string Item = "";
            public string Gate = "";
        }

        private static List<Product>? _products;
        private static int _prefabs = -1;

        /// <summary>The limit on an item, if the chests in the Steward's area already hold that many (0 while paused).</summary>
        public static bool Reached(WorkContext ctx, string item, out int cap)
        {
            cap = 0;
            OrderList orders = BoardOrders.For(BoardOrders.BoardOf(ctx.Hireling.BoardId)?.Zdo);
            if (orders.StationCap(item) is not int limit)
                return false;
            cap = limit;
            return Held(ctx.AllChests, item) >= limit;
        }

        /// <summary>How many of an item the chests hold, all of it (a limit counts reserves too).</summary>
        public static int Held(IEnumerable<Container> chests, string item)
        {
            string shared = WorkSteps.SharedName(item);
            return chests.Sum(c => c.GetInventory().CountItems(shared));
        }

        /// <summary>Everything a Steward makes: station products (modded stations too), honey, sap, meads.</summary>
        public static IReadOnlyList<Product> Products()
        {
            if (ZNetScene.instance == null)
                return new List<Product>();
            if (_products != null && _prefabs == ZNetScene.instance.m_prefabs.Count)
                return _products;
            var found = new Dictionary<string, Product>();
            void Add(GameObject? item, string gate)
            {
                if (item != null && !found.ContainsKey(item.name))
                    found[item.name] = new Product { Item = item.name, Gate = gate };
            }
            foreach (GameObject go in ZNetScene.instance.m_prefabs)
            {
                if (go == null || go.GetComponent<Piece>() == null)
                    continue;
                if (go.GetComponent<Smelter>() is Smelter s)
                    foreach (Smelter.ItemConversion c in s.m_conversion)
                        Add(c?.m_to != null ? c.m_to.gameObject : null, go.name);
                if (go.GetComponentInChildren<Beehive>() is Beehive b && b.m_honeyItem != null)
                    Add(b.m_honeyItem.gameObject, ChoreKeys.Key(ChoreKind.Beehives));
                if (go.GetComponentInChildren<SapCollector>() is SapCollector sap && sap.m_spawnItem != null)
                    Add(sap.m_spawnItem.gameObject, ChoreKeys.Key(ChoreKind.Sap));
                if (go.GetComponentInChildren<Fermenter>() is Fermenter f)
                    foreach (Fermenter.ItemConversion c in f.m_conversion)
                        Add(c?.m_to != null ? c.m_to.gameObject : null, ChoreKeys.Key(ChoreKind.Fermenters));
            }
            _prefabs = ZNetScene.instance.m_prefabs.Count;
            JobData steward = DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out JobData? j) ? j : new JobData();
            _products = found.Values.OrderBy(p => ChoreRules.MinLevel(steward, p.Gate)).ThenBy(p => Localization.instance.Localize(WorkSteps.SharedName(p.Item))).ToList();
            return _products;
        }
    }
}
