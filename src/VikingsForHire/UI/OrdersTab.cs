using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Orders;
using VikingsForHire.Hirelings.Work;

namespace VikingsForHire.UI
{
    /// <summary>
    /// The board's orders at a glance, read-only: each worker's orders (the Farmer's, the Cook's, the Rancher's, the
    /// Steward's limits) under its name, with how far along each one is. They're set in each worker's own Shift+E panel.
    /// </summary>
    internal sealed class OrdersTab : IBoardTab
    {
        private const float RowStep = 26f;
        private const float Top = -150f;
        private const float Bottom = -640f;

        private int _page;

        public string Title => "$vfh_tab_orders";

        public string Signature(HiringBoard board)
        {
            OrderList list = BoardOrders.For(board);
            Stock stock = BoardOrders.Stock(board);
            return $"{_page}|{list.Serialize()}|" + string.Join(",", list.Orders.Select(o => OrderPicks.Have(board, stock, o))) + "|" +
                   string.Join(",", OrderPicks.Jobs.Select(j => Worker(board, j)?.Name ?? "-"));
        }

        // The board's worker of that job (one per board), if it has one.
        private static ContractEntry? Worker(HiringBoard board, JobType job) =>
            board.Zdo == null ? null
                : BoardRosterOps.Read(board.Zdo).Entries.FirstOrDefault(e => e.Job == job && e.State != ContractState.Leaving);

        public void Build(RectTransform root, HiringBoard board)
        {
            PanelUi.Text(root, "$vfh_orders_overview_hint", 0f, -125f, 800f, 15, color: PanelUi.Dim);
            OrderList list = BoardOrders.For(board);
            Stock stock = BoardOrders.Stock(board);
            // Every line in order (job headings and their rows), then paged to fit.
            var lines = new List<(JobType Job, ContractEntry? Worker, ProductionOrder? Order)>();
            foreach (JobType job in OrderPicks.Jobs)
            {
                List<ProductionOrder> orders = OrderPicks.For(list, job);
                ContractEntry? worker = Worker(board, job);
                if (orders.Count == 0 && worker == null)
                    continue;
                lines.Add((job, worker, null));
                if (orders.Count == 0)
                    lines.Add((job, worker, new ProductionOrder { Item = "" }));
                foreach (ProductionOrder o in orders)
                    lines.Add((job, worker, o));
            }
            if (lines.Count == 0)
            {
                PanelUi.Text(root, "$vfh_orders_empty", 0f, -230f, 760f, 17, color: PanelUi.Dim);
                return;
            }
            int perPage = (int)((Top - Bottom) / RowStep);
            int pages = (lines.Count + perPage - 1) / perPage;
            _page = Mathf.Clamp(_page, 0, pages - 1);
            float y = Top - 20f;
            foreach ((JobType job, ContractEntry? worker, ProductionOrder? o) in lines.Skip(_page * perPage).Take(perPage))
            {
                if (o == null)
                {
                    string who = worker != null
                        ? Localization.instance.Localize("$vfh_orders_overview_worker", $"$vfh_job_{job.ToString().ToLowerInvariant()}", worker.Name, worker.Level.ToString())
                        : Localization.instance.Localize("$vfh_orders_overview_none", $"$vfh_job_{job.ToString().ToLowerInvariant()}");
                    // Centred on the panel, written from its left edge.
                    PanelUi.Text(root, who, 0f, y - 4f, 760f, 17, TextAnchor.MiddleLeft, bold: true);
                }
                else if (o.Item.Length == 0)
                    PanelUi.Text(root, "$vfh_orders_overview_no_orders", 20f, y, 720f, 14, TextAnchor.MiddleLeft, PanelUi.Dim);
                else
                {
                    int have = OrderPicks.Have(board, stock, o);
                    if (OrderPicks.Icon(o.Item) is Sprite icon)
                        PanelUi.Icon(root, icon, -360f, y, 22f);
                    PanelUi.Text(root, OrderPicks.Name(o.Item), -200f, y, 280f, 15, TextAnchor.MiddleLeft, o.Paused ? PanelUi.Dim : (Color?)null);
                    PanelUi.Text(root, OrderPicks.KindLabel(o.Kind), 100f, y, 300f, 13, TextAnchor.MiddleLeft, PanelUi.Dim);
                    PanelUi.Text(root, o.Paused ? Localization.instance.Localize("$vfh_limits_paused") : $"{have} / {o.Target}", 320f, y, 120f, 15,
                        color: o.Paused ? PanelUi.Dim : have >= o.Target ? PanelUi.Good : PanelUi.Bad);
                }
                y -= RowStep;
            }
            Pager(root, _page, pages, p => _page = p);
        }

        internal static void Pager(RectTransform root, int page, int pages, System.Action<int> go)
        {
            if (pages <= 1)
                return;
            PanelUi.Button(root, "<", -70f, -665f, 46f, 32f, () => go(Mathf.Max(0, page - 1)));
            PanelUi.Text(root, $"{page + 1} / {pages}", 0f, -665f, 80f, 17);
            PanelUi.Button(root, ">", 70f, -665f, 46f, 32f, () => go(Mathf.Min(pages - 1, page + 1)));
        }
    }
}
