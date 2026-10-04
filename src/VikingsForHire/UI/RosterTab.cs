using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Hirelings;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.UI
{
    /// <summary>The board's hirelings on the left; the selected one's details and actions on the right.</summary>
    internal sealed class RosterTab : IBoardTab
    {
        private const float RadiusStep = 5f;
        private string _selected = "";
        private string _confirmDismiss = "";

        public string Title => "$vfh_tab_roster";

        public string Signature(HiringBoard board)
        {
            if (board.Zdo == null)
                return "none";
            byte[]? bytes = board.Zdo.GetByteArray(BoardZdo.Roster);
            int hash = bytes == null ? 0 : bytes.Aggregate(17, (h, b) => h * 31 + b);
            Cost funds = board.Inventory != null ? BoardStorage.Totals(board.Inventory) : Cost.Zero;
            long tick = (long)(ZNet.instance.GetTimeSeconds() / 5); // refresh countdowns every 5s
            return $"{hash}|{_selected}|{_confirmDismiss}|{funds}|{tick}|{board.Level}";
        }

        public void Build(RectTransform root, HiringBoard board)
        {
            Roster roster = BoardRosterOps.Read(board.Zdo!);
            if (roster.Count == 0)
            {
                PanelUi.Text(root, "$vfh_tab_roster_empty", 0f, -220f, 560f, 18, color: PanelUi.Dim);
                return;
            }
            if (roster.Entries.All(e => e.ContractId != _selected))
                _selected = roster.Entries[0].ContractId;

            float y = -150f;
            foreach (ContractEntry e in roster.Entries.Take(12))
            {
                ContractEntry entry = e;
                string label = $"{e.Name} — $vfh_job_{e.Job.ToString().ToLowerInvariant()} {e.Level}";
                Button b = PanelUi.Button(root, label, -175f, y, 320f, 28f, () =>
                {
                    _selected = entry.ContractId;
                    _confirmDismiss = "";
                });
                b.GetComponentInChildren<Text>().color = e.ContractId == _selected ? Color.yellow : StateColor(e);
                y -= 31f;
            }

            Details(root, board, roster, roster.Entries.First(e => e.ContractId == _selected));
        }

        private void Details(RectTransform root, HiringBoard board, Roster roster, ContractEntry e)
        {
            const float x = 185f;
            var rules = new LevelRules(DataStore.Current);
            var costs = new CostCalculator(DataStore.Current, VfhConfig.RespawnCostFraction.Value);
            Hireling? live = Hireling.Loaded.FirstOrDefault(h => h != null && e.Hid.Length > 0 && h.Hid == e.Hid);

            PanelUi.Text(root, e.Name, x, -150f, 320f, 22, bold: true);
            PanelUi.Text(root, $"$vfh_job_{e.Job.ToString().ToLowerInvariant()} — $vfh_level {e.Level}", x, -180f, 320f, 18);
            PanelUi.Text(root, Status(e, live), x, -208f, 320f, 16, color: StateColor(e));
            if (live != null)
                PanelUi.Text(root, $"$vfh_health {Mathf.CeilToInt(live.Humanoid.GetHealth())}/{Mathf.CeilToInt(live.Humanoid.GetMaxHealth())}", x, -234f, 320f, 16, color: PanelUi.Dim);
            PanelUi.Text(root, Localization.instance.Localize("$vfh_contract_upkeep") + " " + ContractsTab.Price(costs.DailyUpkeep(e.Job, e.Level)), x, -260f, 320f, 15, color: PanelUi.Dim);

            if (e.State == ContractState.Pending)
            {
                PanelUi.Button(root, "$vfh_roster_cancel", x, -320f, 200f, 38f, () => BoardContracts.Cancel(board, e.ContractId));
                return;
            }
            if (e.State != ContractState.Active)
                return;

            // Stance and radius
            IReadOnlyList<Stance> stances = StanceRules.Allowed(e.Job);
            int si = Mathf.Max(0, stances.ToList().IndexOf(e.Stance));
            PanelUi.Button(root, $"$vfh_contract_stance $vfh_stance_{e.Stance.ToString().ToLowerInvariant()}", x, -300f, 280f, 34f,
                () => BoardContracts.Edit(board, e.Hid, e.Radius, stances[(si + 1) % stances.Count]));
            PanelUi.Button(root, "-", x - 100f, -342f, 44f, 34f, () => BoardContracts.Edit(board, e.Hid, e.Radius - RadiusStep, e.Stance));
            PanelUi.Text(root, $"$vfh_contract_radius {e.Radius:0} m", x, -342f, 160f, 16);
            PanelUi.Button(root, "+", x + 100f, -342f, 44f, 34f, () => BoardContracts.Edit(board, e.Hid, e.Radius + RadiusStep, e.Stance));

            // Promote one level at a time, up to the board's max
            int max = rules.MaxHirelingLevel(board.Level);
            if (e.Level < max)
            {
                Cost cost = costs.PromotionCost(e.Job, e.Level, e.Level + 1);
                Cost funds = board.Inventory != null ? BoardStorage.Totals(board.Inventory) : Cost.Zero;
                Button promote = PanelUi.Button(root, Localization.instance.Localize("$vfh_roster_promote", (e.Level + 1).ToString()), x, -390f, 280f, 34f,
                    () => BoardContracts.Promote(board, e.Hid, e.Level + 1));
                promote.interactable = cost.CoveredBy(funds);
                PanelUi.Text(root, ContractsTab.Price(cost), x, -420f, 300f, 15, color: cost.CoveredBy(funds) ? PanelUi.Good : PanelUi.Bad);
            }

            // Confirm inside the panel: vanilla's popup draws on the HUD layer, underneath this panel.
            if (_confirmDismiss == e.ContractId)
            {
                PanelUi.Text(root, Localization.instance.Localize("$vfh_confirm_dismiss_short", e.Name), x, -448f, 330f, 15, color: PanelUi.Bad);
                PanelUi.Button(root, "$vfh_yes", x - 60f, -480f, 100f, 32f, () =>
                {
                    _confirmDismiss = "";
                    BoardContracts.Dismiss(board.Id, e.Hid);
                });
                PanelUi.Button(root, "$vfh_no", x + 60f, -480f, 100f, 32f, () => _confirmDismiss = "");
                return;
            }
            PanelUi.Button(root, "$vfh_roster_dismiss", x, -465f, 200f, 34f, () => _confirmDismiss = e.ContractId);
            if (e.Post != null)
                PanelUi.Button(root, "$vfh_orders_clear_post", x, -505f, 280f, 34f, () => BoardContracts.ClearPost(board, e.Hid));
        }

        private static string Status(ContractEntry e, Hireling? live)
        {
            switch (e.State)
            {
                case ContractState.Pending:
                    double wait = e.ArriveAt - ZNet.instance.GetTimeSeconds();
                    string key = e.RespawnPending ? "$vfh_status_returning" : "$vfh_status_arriving";
                    // "$1s" would read as an unknown token; localize the label alone and append the seconds.
                    return wait > 0 ? Localization.instance.Localize(key) + $" ({Mathf.CeilToInt((float)wait)}s)"
                        : Localization.instance.Localize(e.RespawnPending ? "$vfh_status_awaiting_payment" : key);
                case ContractState.Leaving:
                    return "$vfh_status_leaving";
                default:
                    if (e.Post != null && (live == null || live.Mode == HirelingMode.Working))
                        return "$vfh_roster_posted";
                    if (live != null && live.Mode == HirelingMode.Returning && live.Zdo != null)
                    {
                        float left = Followers.HomeReturn.SecondsLeft(live.Zdo);
                        return Localization.instance.Localize("$vfh_status_returning_home") + (left > 0f ? $" ({Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00})" : "");
                    }
                    if (live != null && live.Mode == HirelingMode.Following)
                        return Localization.instance.Localize("$vfh_roster_following", live.OwnerName);
                    string status = live?.Zdo?.GetString(HirelingZdo.Status) ?? "";
                    if (e.UnpaidDays > 0 && status.Length == 0)
                        status = $"$vfh_status_unpaid ({e.UnpaidDays}/{VfhConfig.UnpaidDaysBeforeLeaving.Value})";
                    return status.Length > 0 ? status : live != null ? "$vfh_status_working" : "$vfh_status_away";
            }
        }

        private static Color StateColor(ContractEntry e) => e.State switch
        {
            ContractState.Pending => PanelUi.Dim,
            ContractState.Leaving => PanelUi.Bad,
            _ => e.UnpaidDays > 0 ? PanelUi.Bad : Color.white,
        };
    }
}
