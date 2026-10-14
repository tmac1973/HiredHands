using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Orders;
using VikingsForHire.Hirelings.Work;
using VikingsForHire.Hirelings.Work.Farm;
using VikingsForHire.Hirelings.Work.Kitchen;

namespace VikingsForHire.UI
{
    /// <summary>
    /// What a worker can be ordered to keep (the Farmer's crops and seeds, the Cook's dishes, the Rancher's meat, eggs and
    /// breeding pools), which of the board's orders are whose, and how they read: shared by each worker's Orders page in
    /// its Shift+E panel and the board's read-only overview.
    /// </summary>
    internal static class OrderPicks
    {
        public sealed class Pick
        {
            public string Item = "";
            public OrderKind Kind;
            public int Level;
        }

        /// <summary>The jobs that take orders, in the order the overview shows them.</summary>
        public static readonly JobType[] Jobs = { JobType.Farmer, JobType.Cook, JobType.Rancher, JobType.Smelter };

        public static bool TakesOrders(JobType job) => job is JobType.Farmer or JobType.Cook or JobType.Rancher;

        /// <summary>This job's orders on the board, as it works them (a Farmer's seed orders first).</summary>
        public static List<ProductionOrder> For(OrderList list, JobType job) => job switch
        {
            JobType.Farmer => list.Orders.Where(o => o.Kind == OrderKind.Seed).Concat(list.Orders.Where(o => o.Kind == OrderKind.Crop)).ToList(),
            JobType.Cook => list.Orders.Where(o => o.Kind == OrderKind.Kitchen).ToList(),
            JobType.Rancher => list.Orders.Where(o => o.IsRanch).ToList(),
            JobType.Smelter => list.Orders.Where(o => o.Kind == OrderKind.Station).ToList(),
            _ => new List<ProductionOrder>(),
        };

        /// <summary>How much there is toward an order: chests and the board's pile, or for a breeding pool the herd's adults.</summary>
        public static int Have(HiringBoard board, Stock stock, ProductionOrder o) => o.Kind == OrderKind.Herd ? HerdHave(board, o.Item) : stock.Have(o.Item);

        private static int HerdHave(HiringBoard board, string kind) =>
            Hirelings.Work.Ranch.RanchState.For(board).Adults.TryGetValue(kind, out int n) ? n : 0;

        public static string KindLabel(OrderKind kind) => kind switch
        {
            OrderKind.Seed => "$vfh_orders_kind_seed",
            OrderKind.Crop => "$vfh_orders_kind_farm",
            OrderKind.Meat => "$vfh_orders_kind_meat",
            OrderKind.Egg => "$vfh_orders_kind_egg",
            OrderKind.Herd => "$vfh_orders_kind_herd",
            OrderKind.Station => "$vfh_orders_kind_limit",
            _ => "$vfh_orders_kind_kitchen",
        };

        /// <summary>The target a new order starts with.</summary>
        public static int StartTarget(OrderKind kind) => kind switch { OrderKind.Seed => 10, OrderKind.Meat => 40, OrderKind.Herd => 4, _ => 20 };

        /// <summary>Everything a job can be ordered to keep, easiest first.</summary>
        public static List<Pick> Picks(JobType job)
        {
            if (job == JobType.Rancher)
            {
                var ranch = new List<Pick>();
                foreach (AnimalKind k in Hirelings.Work.Ranch.AnimalCatalog.Kinds)
                {
                    ranch.Add(new Pick { Item = k.Prefab, Kind = OrderKind.Herd, Level = 1 });
                    foreach (string meat in k.Meats.Keys.Where(m => ranch.All(p => p.Item != m)))
                        ranch.Add(new Pick { Item = meat, Kind = OrderKind.Meat, Level = 1 });
                    if (k.EggItem.Length > 0 && ranch.All(p => p.Item != k.EggItem))
                        ranch.Add(new Pick { Item = k.EggItem, Kind = OrderKind.Egg, Level = 1 });
                }
                return ranch.OrderBy(p => p.Kind).ThenBy(p => Name(p.Item)).ToList();
            }
            if (job == JobType.Cook)
                return KitchenCatalog.All.GroupBy(k => k.Output)
                    .Select(g => new Pick { Item = g.Key, Kind = OrderKind.Kitchen, Level = g.Min(k => k.Level) })
                    .OrderBy(p => p.Level).ThenBy(p => Name(p.Item)).ToList();
            List<CropInfo> crops = CropCatalog.Infos.ToList();
            var picks = new Dictionary<string, Pick>();
            foreach (CropInfo c in crops)
            {
                AddPick(picks, c.Yields, c.Level, crops);
                if (!c.Regrowing)
                    AddPick(picks, c.Consumes, c.Level, crops);
            }
            return picks.Values.OrderBy(p => p.Level).ThenBy(p => p.Kind).ThenBy(p => Name(p.Item)).ToList();
        }

        private static void AddPick(Dictionary<string, Pick> picks, string item, int level, List<CropInfo> crops)
        {
            if (string.IsNullOrEmpty(item))
                return;
            bool seed = CropInfo.IsSeedItem(item, crops, i => ObjectDB.instance.GetItemPrefab(i) is GameObject go && CropCatalog.IsFood(go) ||
                                                              KitchenCatalog.All.Any(k => k.Inputs.ContainsKey(i)));
            if (picks.TryGetValue(item, out Pick p))
                p.Level = Mathf.Min(p.Level, level);
            else
                picks[item] = new Pick { Item = item, Kind = seed ? OrderKind.Seed : OrderKind.Crop, Level = level };
        }

        /// <summary>A picker button's label: the item, "(seed)" for a seed order, or "Keep breeding: Boar" for a pool.</summary>
        public static string PickLabel(Pick p) => p.Kind == OrderKind.Herd ? Localization.instance.Localize("$vfh_orders_pick_herd", Name(p.Item))
            : Name(p.Item) + (p.Kind == OrderKind.Seed ? " (" + Localization.instance.Localize("$vfh_orders_kind_seed") + ")" : "");

        /// <summary>An item's name, or an animal kind's (a breeding pool order is for a creature).</summary>
        public static string Name(string item) =>
            ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>() is ItemDrop d ? Localization.instance.Localize(d.m_itemData.m_shared.m_name)
            : ZNetScene.instance?.GetPrefab(item)?.GetComponent<Character>() is Character c ? Localization.instance.Localize(c.m_name) : item;

        public static Sprite? Icon(string item) =>
            ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon();
    }
}
