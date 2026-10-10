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
    /// The board's production orders, worked top to bottom: farm orders (seed orders first) by the board's Farmer and
    /// kitchen orders by its Cook, "keep at least X in the chests"; Steward orders are limits, "make no more once the
    /// chests hold X" of a smelter's, kiln's or mill's product. A list view, and a picker for adding an order.
    /// </summary>
    internal sealed class OrdersTab : IBoardTab
    {
        private enum PickFor { Farm, Kitchen, Steward }

        private const int RowsPerPage = 10;
        private const float RowStep = 46f;
        private const int PicksPerPage = 18;
        private const int Step = 5;

        private bool _picking;
        private PickFor _pickFor;
        private int _page;
        private int _pickPage;

        public string Title => "$vfh_tab_orders";

        public string Signature(HiringBoard board)
        {
            OrderList list = BoardOrders.For(board);
            Stock stock = BoardOrders.Stock(board);
            string haves = string.Join(",", list.Orders.Select(o => stock.Have(o.Item)));
            return $"{_picking}|{_pickFor}|{_page}|{_pickPage}|{list.Serialize()}|{haves}|" +
                   $"{BoardOrders.WorkerLevel(board, JobType.Farmer)}|{BoardOrders.WorkerLevel(board, JobType.Cook)}|" +
                   $"{BoardOrders.WorkerLevel(board, JobType.Smelter)}|{CanEdit(board)}";
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
            var adds = new[]
            {
                PanelUi.Button(root, "$vfh_orders_add_farm", -310f, -140f, 280f, 34f, () => StartPick(PickFor.Farm)),
                PanelUi.Button(root, "$vfh_orders_add_kitchen", 0f, -140f, 280f, 34f, () => StartPick(PickFor.Kitchen)),
                PanelUi.Button(root, "$vfh_orders_add_steward", 310f, -140f, 280f, 34f, () => StartPick(PickFor.Steward)),
            };
            foreach (Button b in adds)
                b.interactable = edit;

            // Farm (seed orders first, as they're worked), then kitchen, then the Steward's limits.
            List<ProductionOrder> shown = list.Orders.Where(o => o.Kind == OrderKind.Seed)
                .Concat(list.Orders.Where(o => o.Kind == OrderKind.Crop))
                .Concat(list.Orders.Where(o => o.Kind == OrderKind.Kitchen))
                .Concat(list.Orders.Where(o => o.Kind == OrderKind.Station)).ToList();
            if (shown.Count == 0)
            {
                PanelUi.Text(root, "$vfh_orders_empty", 0f, -230f, 760f, 17, color: PanelUi.Dim);
                return;
            }
            int pages = (shown.Count + RowsPerPage - 1) / RowsPerPage;
            _page = Mathf.Clamp(_page, 0, pages - 1);
            float y = -195f;
            foreach (ProductionOrder o in shown.Skip(_page * RowsPerPage).Take(RowsPerPage))
            {
                Row(root, board, o, stock.Have(o.Item), y, edit);
                y -= RowStep;
            }
            Pager(root, _page, pages, p => _page = p);
        }

        private void StartPick(PickFor what)
        {
            _picking = true;
            _pickFor = what;
            _pickPage = 0;
        }

        private static void Pager(RectTransform root, int page, int pages, System.Action<int> go)
        {
            if (pages <= 1)
                return;
            PanelUi.Button(root, "<", -70f, -665f, 46f, 32f, () => go(Mathf.Max(0, page - 1)));
            PanelUi.Text(root, $"{page + 1} / {pages}", 0f, -665f, 80f, 17);
            PanelUi.Button(root, ">", 70f, -665f, 46f, 32f, () => go(Mathf.Min(pages - 1, page + 1)));
        }

        private static void Row(RectTransform root, HiringBoard board, ProductionOrder o, int have, float y, bool edit)
        {
            ItemDrop? item = ObjectDB.instance.GetItemPrefab(o.Item)?.GetComponent<ItemDrop>();
            PanelUi.Icon(root, item?.m_itemData.GetIcon(), -410f, y, 36f);
            string kind = o.Kind switch
            {
                OrderKind.Seed => "$vfh_orders_kind_seed",
                OrderKind.Crop => "$vfh_orders_kind_farm",
                OrderKind.Kitchen => "$vfh_orders_kind_kitchen",
                _ => "$vfh_orders_kind_steward",
            };
            PanelUi.Text(root, (item != null ? item.m_itemData.m_shared.m_name : o.Item), -250f, y + 8f, 260f, 18, TextAnchor.MiddleLeft, o.Paused ? PanelUi.Dim : (Color?)null);
            PanelUi.Text(root, kind, -250f, y - 11f, 260f, 13, TextAnchor.MiddleLeft, PanelUi.Dim);
            // A limit that's reached is the Steward stopping as asked, not a shortfall.
            Color count = have >= o.Target ? PanelUi.Good : o.Kind == OrderKind.Station ? PanelUi.Dim : PanelUi.Bad;
            PanelUi.Text(root, $"{have} / {o.Target}", -50f, y, 130f, 18, color: count);
            var buttons = new List<Button>
            {
                PanelUi.Button(root, "-", 45f, y, 36f, 32f, () => BoardOrders.Submit(board, OrderEdit.Target, o.Item, target: o.Target - StepFor())),
                PanelUi.Button(root, "+", 87f, y, 36f, 32f, () => BoardOrders.Submit(board, OrderEdit.Target, o.Item, target: o.Target + StepFor())),
                PanelUi.Button(root, "^", 145f, y, 36f, 32f, () => BoardOrders.Submit(board, OrderEdit.Up, o.Item)),
                PanelUi.Button(root, "v", 187f, y, 36f, 32f, () => BoardOrders.Submit(board, OrderEdit.Down, o.Item)),
                PanelUi.Button(root, o.Paused ? "$vfh_orders_resume" : "$vfh_orders_pause", 280f, y, 110f, 32f,
                    () => BoardOrders.Submit(board, OrderEdit.Pause, o.Item, paused: !o.Paused)),
                PanelUi.Button(root, "X", 370f, y, 36f, 32f, () => BoardOrders.Submit(board, OrderEdit.Remove, o.Item)),
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

        private static List<Pick> Picks(PickFor what)
        {
            if (what == PickFor.Kitchen)
                return KitchenCatalog.All.GroupBy(k => k.Output)
                    .Select(g => new Pick { Item = g.Key, Kind = OrderKind.Kitchen, Level = g.Min(k => k.Level) })
                    .OrderBy(p => p.Level).ThenBy(p => Name(p.Item)).ToList();
            if (what == PickFor.Steward)
                return StationPicks();
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

        // What the Steward's stations make (smelters, kilns, furnaces, refineries, mills, modded ones too), at the level
        // the station unlocks.
        private static List<Pick> StationPicks()
        {
            var picks = new Dictionary<string, Pick>();
            if (ZNetScene.instance == null || !Config.DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out Core.Data.JobData? steward))
                return new List<Pick>();
            foreach (GameObject go in ZNetScene.instance.m_prefabs)
            {
                if (go == null || go.GetComponent<Piece>() == null || go.GetComponent<Smelter>() is not Smelter station)
                    continue;
                int level = Core.Chores.ChoreRules.MinLevel(steward, go.name);
                foreach (Smelter.ItemConversion c in station.m_conversion)
                {
                    if (c?.m_to == null)
                        continue;
                    string item = c.m_to.gameObject.name;
                    if (picks.TryGetValue(item, out Pick p))
                        p.Level = Mathf.Min(p.Level, level);
                    else
                        picks[item] = new Pick { Item = item, Kind = OrderKind.Station, Level = level };
                }
            }
            return picks.Values.OrderBy(p => p.Level).ThenBy(p => Name(p.Item)).ToList();
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
            JobType job = _pickFor switch { PickFor.Kitchen => JobType.Cook, PickFor.Steward => JobType.Smelter, _ => JobType.Farmer };
            int worker = BoardOrders.WorkerLevel(board, job);
            string title = _pickFor switch { PickFor.Kitchen => "$vfh_orders_pick_kitchen", PickFor.Steward => "$vfh_orders_pick_steward", _ => "$vfh_orders_pick_farm" };
            string needs = _pickFor switch { PickFor.Kitchen => "$vfh_orders_needs_cook", PickFor.Steward => "$vfh_orders_needs_steward", _ => "$vfh_orders_needs_farmer" };
            PanelUi.Text(root, title, 0f, -140f, 600f, 19, bold: true);
            PanelUi.Button(root, "$vfh_orders_back", 380f, -140f, 110f, 32f, () => _picking = false);
            if (_pickFor == PickFor.Steward)
                PanelUi.Text(root, "$vfh_orders_steward_hint", 0f, -165f, 760f, 14, color: PanelUi.Dim);
            List<Pick> picks = Picks(_pickFor).Where(p => list.Find(p.Item) == null).ToList();
            int pages = Mathf.Max(1, (picks.Count + PicksPerPage - 1) / PicksPerPage);
            _pickPage = Mathf.Clamp(_pickPage, 0, pages - 1);
            int i = 0;
            foreach (Pick p in picks.Skip(_pickPage * PicksPerPage).Take(PicksPerPage))
            {
                float x = i % 2 == 0 ? -225f : 225f;
                float y = -200f - (i / 2) * 50f;
                bool canDo = worker >= p.Level;
                string label = Name(p.Item) + (p.Kind == OrderKind.Seed ? " (" + Localization.instance.Localize("$vfh_orders_kind_seed") + ")" : "");
                string note = canDo ? Localization.instance.Localize("$vfh_orders_level", p.Level.ToString())
                    : Localization.instance.Localize(needs, p.Level.ToString());
                Pick chosen = p;
                Button b = PanelUi.Button(root, label, x, y, 420f, 32f, () =>
                {
                    int target = chosen.Kind switch { OrderKind.Seed => 10, OrderKind.Station => 100, _ => 20 };
                    BoardOrders.Submit(board, OrderEdit.Add, chosen.Item, chosen.Kind, target);
                    _picking = false;
                });
                PanelUi.Text(root, note, x, y - 21f, 420f, 13, color: canDo ? PanelUi.Dim : PanelUi.Bad);
                i++;
            }
            if (picks.Count == 0)
                PanelUi.Text(root, "$vfh_orders_pick_none", 0f, -230f, 600f, 17, color: PanelUi.Dim);
            Pager(root, _pickPage, pages, p => _pickPage = p);
        }
    }
}
