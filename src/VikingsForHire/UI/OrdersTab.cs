using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Orders;
using VikingsForHire.Hirelings.Work;
using VikingsForHire.Hirelings.Work.Farm;
using VikingsForHire.Hirelings.Work.Kitchen;

namespace VikingsForHire.UI
{
    /// <summary>
    /// The board's production orders: "keep at least X of this in stock", worked by the board's Farmer (farm orders, seed
    /// orders first) and Cook (kitchen orders) top to bottom. A list view, and a picker for adding an order.
    /// </summary>
    internal sealed class OrdersTab : IBoardTab
    {
        private const int RowsPerPage = 9;
        private const int PicksPerPage = 14;
        private const int Step = 5;

        private bool _picking;
        private bool _pickKitchen;
        private int _page;
        private int _pickPage;

        public string Title => "$vfh_tab_orders";

        public string Signature(HiringBoard board)
        {
            OrderList list = BoardOrders.For(board);
            Stock stock = BoardOrders.Stock(board);
            string haves = string.Join(",", list.Orders.Select(o => stock.Have(o.Item)));
            return $"{_picking}|{_pickKitchen}|{_page}|{_pickPage}|{list.Serialize()}|{haves}|" +
                   $"{BoardOrders.WorkerLevel(board, JobType.Farmer)}|{BoardOrders.WorkerLevel(board, JobType.Cook)}|{CanEdit(board)}";
        }

        private static bool CanEdit(HiringBoard board) => PrivateArea.CheckAccess(board.transform.position, 0f, false, false);

        public void Build(RectTransform root, HiringBoard board)
        {
            if (_picking)
                BuildPicker(root, board);
            else
                BuildList(root, board);
        }

        // ---- the list ----

        private void BuildList(RectTransform root, HiringBoard board)
        {
            bool edit = CanEdit(board);
            OrderList list = BoardOrders.For(board);
            Stock stock = BoardOrders.Stock(board);
            Button addFarm = PanelUi.Button(root, "$vfh_orders_add_farm", -150f, -140f, 260f, 32f, () => { _picking = true; _pickKitchen = false; _pickPage = 0; });
            Button addKitchen = PanelUi.Button(root, "$vfh_orders_add_kitchen", 150f, -140f, 260f, 32f, () => { _picking = true; _pickKitchen = true; _pickPage = 0; });
            addFarm.interactable = addKitchen.interactable = edit;

            // Farm (seed orders first, as they're worked), then kitchen.
            List<ProductionOrder> shown = list.Orders.Where(o => o.Kind == OrderKind.Seed)
                .Concat(list.Orders.Where(o => o.Kind == OrderKind.Crop))
                .Concat(list.Orders.Where(o => o.Kind == OrderKind.Kitchen)).ToList();
            if (shown.Count == 0)
            {
                PanelUi.Text(root, "$vfh_orders_empty", 0f, -220f, 600f, 16, color: PanelUi.Dim);
                return;
            }
            int pages = (shown.Count + RowsPerPage - 1) / RowsPerPage;
            _page = Mathf.Clamp(_page, 0, pages - 1);
            float y = -185f;
            foreach (ProductionOrder o in shown.Skip(_page * RowsPerPage).Take(RowsPerPage))
            {
                Row(root, board, o, stock.Have(o.Item), y, edit);
                y -= 36f;
            }
            if (pages > 1)
            {
                PanelUi.Button(root, "<", -60f, -520f, 40f, 28f, () => _page = Mathf.Max(0, _page - 1));
                PanelUi.Text(root, $"{_page + 1} / {pages}", 0f, -520f, 60f, 15);
                PanelUi.Button(root, ">", 60f, -520f, 40f, 28f, () => _page = Mathf.Min(pages - 1, _page + 1));
            }
        }

        private static void Row(RectTransform root, HiringBoard board, ProductionOrder o, int have, float y, bool edit)
        {
            ItemDrop? item = ObjectDB.instance.GetItemPrefab(o.Item)?.GetComponent<ItemDrop>();
            PanelUi.Icon(root, item?.m_itemData.GetIcon(), -315f, y, 28f);
            string kind = o.Kind == OrderKind.Seed ? "$vfh_orders_kind_seed" : o.Kind == OrderKind.Crop ? "$vfh_orders_kind_farm" : "$vfh_orders_kind_kitchen";
            PanelUi.Text(root, (item != null ? item.m_itemData.m_shared.m_name : o.Item), -205f, y + 6f, 170f, 15, TextAnchor.MiddleLeft, o.Paused ? PanelUi.Dim : (Color?)null);
            PanelUi.Text(root, kind, -205f, y - 9f, 170f, 11, TextAnchor.MiddleLeft, PanelUi.Dim);
            PanelUi.Text(root, $"{have} / {o.Target}", -55f, y, 100f, 15, color: have >= o.Target ? PanelUi.Good : PanelUi.Bad);
            var buttons = new List<Button>
            {
                PanelUi.Button(root, "-", 15f, y, 30f, 28f, () => BoardOrders.Submit(board, OrderEdit.Target, o.Item, target: o.Target - StepFor())),
                PanelUi.Button(root, "+", 50f, y, 30f, 28f, () => BoardOrders.Submit(board, OrderEdit.Target, o.Item, target: o.Target + StepFor())),
                PanelUi.Button(root, "^", 95f, y, 30f, 28f, () => BoardOrders.Submit(board, OrderEdit.Up, o.Item)),
                PanelUi.Button(root, "v", 130f, y, 30f, 28f, () => BoardOrders.Submit(board, OrderEdit.Down, o.Item)),
                PanelUi.Button(root, o.Paused ? "$vfh_orders_resume" : "$vfh_orders_pause", 200f, y, 90f, 28f,
                    () => BoardOrders.Submit(board, OrderEdit.Pause, o.Item, paused: !o.Paused)),
                PanelUi.Button(root, "X", 270f, y, 30f, 28f, () => BoardOrders.Submit(board, OrderEdit.Remove, o.Item)),
            };
            foreach (Button b in buttons)
                b.interactable = edit;
        }

        // Shift for single steps.
        private static int StepFor() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 1 : Step;

        // ---- the picker ----

        private sealed class Pick
        {
            public string Item = "";
            public OrderKind Kind;
            public int Level;
        }

        private static List<Pick> Picks(bool kitchen)
        {
            if (kitchen)
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
            bool seed = CropInfo.IsSeedItem(item, crops, i => ObjectDB.instance.GetItemPrefab(i) is GameObject go && CropCatalog.IsFood(go));
            if (picks.TryGetValue(item, out Pick p))
                p.Level = Mathf.Min(p.Level, level);
            else
                picks[item] = new Pick { Item = item, Kind = seed ? OrderKind.Seed : OrderKind.Crop, Level = level };
        }

        private static string Name(string item) =>
            ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>() is ItemDrop d ? Localization.instance.Localize(d.m_itemData.m_shared.m_name) : item;

        private void BuildPicker(RectTransform root, HiringBoard board)
        {
            OrderList list = BoardOrders.For(board);
            JobType job = _pickKitchen ? JobType.Cook : JobType.Farmer;
            int worker = BoardOrders.WorkerLevel(board, job);
            PanelUi.Text(root, _pickKitchen ? "$vfh_orders_pick_kitchen" : "$vfh_orders_pick_farm", 0f, -140f, 600f, 17, bold: true);
            PanelUi.Button(root, "$vfh_orders_back", 280f, -140f, 100f, 28f, () => _picking = false);
            List<Pick> picks = Picks(_pickKitchen).Where(p => list.Find(p.Item) == null).ToList();
            int pages = Mathf.Max(1, (picks.Count + PicksPerPage - 1) / PicksPerPage);
            _pickPage = Mathf.Clamp(_pickPage, 0, pages - 1);
            int i = 0;
            foreach (Pick p in picks.Skip(_pickPage * PicksPerPage).Take(PicksPerPage))
            {
                float x = i % 2 == 0 ? -165f : 165f;
                float y = -185f - (i / 2) * 44f;
                bool canDo = worker >= p.Level;
                string label = Name(p.Item) + (p.Kind == OrderKind.Seed ? " (" + Localization.instance.Localize("$vfh_orders_kind_seed") + ")" : "");
                string note = canDo ? Localization.instance.Localize("$vfh_orders_level", p.Level.ToString())
                    : Localization.instance.Localize(_pickKitchen ? "$vfh_orders_needs_cook" : "$vfh_orders_needs_farmer", p.Level.ToString());
                Pick chosen = p;
                Button b = PanelUi.Button(root, label, x, y, 310f, 30f, () =>
                {
                    BoardOrders.Submit(board, OrderEdit.Add, chosen.Item, chosen.Kind, chosen.Kind == OrderKind.Seed ? 10 : 20);
                    _picking = false;
                });
                PanelUi.Text(root, note, x, y - 19f, 310f, 11, color: canDo ? PanelUi.Dim : PanelUi.Bad);
                i++;
            }
            if (picks.Count == 0)
                PanelUi.Text(root, "$vfh_orders_pick_none", 0f, -220f, 600f, 16, color: PanelUi.Dim);
            if (pages > 1)
            {
                PanelUi.Button(root, "<", -60f, -520f, 40f, 28f, () => _pickPage = Mathf.Max(0, _pickPage - 1));
                PanelUi.Text(root, $"{_pickPage + 1} / {pages}", 0f, -520f, 60f, 15);
                PanelUi.Button(root, ">", 60f, -520f, 40f, 28f, () => _pickPage = Mathf.Min(pages - 1, _pickPage + 1));
            }
        }
    }
}
