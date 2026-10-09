using System;
using System.Collections.Generic;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.UI
{
    /// <summary>Post a contract: job, level, work radius and stance, with the hire fee and daily upkeep shown live.</summary>
    internal sealed class ContractsTab : IBoardTab
    {
        private static readonly JobType[] Jobs = (JobType[])Enum.GetValues(typeof(JobType));
        private const float RadiusStep = 5f;

        private int _job;
        private int _level = 1;
        private float _radius = 20f;
        // The job/board level the radius was last defaulted for: a new job (or an upgraded board) starts at its maximum.
        private string _radiusDefaultedFor = "";
        private int _stance = -1;

        public string Title => "$vfh_tab_contracts";

        public string Signature(HiringBoard board)
        {
            Clamp(board);
            Cost funds = board.Inventory != null ? BoardStorage.Totals(board.Inventory) : Cost.Zero;
            int count = board.Zdo != null ? BoardRosterOps.Read(board.Zdo).Count : 0;
            return $"{_job}|{_level}|{_radius}|{_stance}|{board.Level}|{funds}|{count}|{DataStore.Hash}";
        }

        public void Build(RectTransform root, HiringBoard board)
        {
            Clamp(board);
            JobType job = Jobs[_job];
            IReadOnlyList<Stance> stances = StanceRules.Allowed(job);
            Stance stance = stances[_stance];
            var rules = new LevelRules(DataStore.Current);
            var costs = new CostCalculator(DataStore.Current, VfhConfig.RespawnCostFraction.Value);

            Row(root, -150f, "$vfh_contract_job", $"$vfh_job_{job.ToString().ToLowerInvariant()}", () => _job = (_job + Jobs.Length - 1) % Jobs.Length, () => _job = (_job + 1) % Jobs.Length);
            Row(root, -195f, "$vfh_contract_level", _level.ToString(), () => _level--, () => _level++);
            Row(root, -240f, "$vfh_contract_radius", $"{_radius:0} m", () => _radius -= RadiusStep, () => _radius += RadiusStep);
            Row(root, -285f, "$vfh_contract_stance", $"$vfh_stance_{stance.ToString().ToLowerInvariant()}", () => _stance = (_stance + stances.Count - 1) % stances.Count, () => _stance = (_stance + 1) % stances.Count);

            Cost fee = costs.HireCost(job, _level);
            Cost upkeep = costs.DailyUpkeep(job, _level);
            Cost funds = board.Inventory != null ? BoardStorage.Totals(board.Inventory) : Cost.Zero;
            Roster roster = board.Zdo != null ? BoardRosterOps.Read(board.Zdo) : new Roster();
            int count = roster.Count;
            int cap = rules.CombatCap(board.Level) + rules.WorkerCap(board.Level);
            bool canPost = roster.CanPost(job, rules, board.Level) == OpOutcome.Ok;
            bool affordable = fee.CoveredBy(funds);
            bool unlocked = rules.JobUnlocked(board.Level, job);

            PanelUi.Text(root, Localization.instance.Localize("$vfh_contract_fee") + "  " + Price(fee), 0f, -335f, 600f, 18, color: affordable ? PanelUi.Good : PanelUi.Bad);
            PanelUi.Text(root, Localization.instance.Localize("$vfh_contract_upkeep") + "  " + Price(upkeep), 0f, -365f, 600f, 18);
            PanelUi.Text(root, Localization.instance.Localize("$vfh_contract_funds") + "  " + $"{funds.FoodPoints} {Localization.instance.Localize("$vfh_food_points")} + {funds.Coins} {Localization.instance.Localize("$vfh_coins")}", 0f, -395f, 600f, 18, color: PanelUi.Dim);
            PanelUi.Text(root, Localization.instance.Localize("$vfh_contract_count") + $"  {count} / {cap}", 0f, -425f, 600f, 18,
                color: canPost ? PanelUi.Dim : PanelUi.Bad);

            Button post = PanelUi.Button(root, "$vfh_contract_post", 0f, -475f, 240f, 44f,
                () => BoardContracts.Post(board, job, _level, _radius, stance));
            post.interactable = affordable && canPost && unlocked;
            if (!unlocked)
                PanelUi.Text(root, Localization.instance.Localize("$vfh_contract_job_locked", rules.MinBoardLevel(job).ToString()), 0f, -515f, 600f, 18, color: PanelUi.Bad);
        }

        /// <summary>
        /// "50 coins", "40 food pts", "40 food pts + 50 coins", or "free": only the parts that cost something. (Built here,
        /// not as "$1 …" text: the localizer reads "$1" as an unknown key and shows "[1]".)
        /// </summary>
        public static string Price(Cost c)
        {
            var parts = new List<string>();
            if (c.FoodPoints > 0)
                parts.Add($"{c.FoodPoints} {Localization.instance.Localize("$vfh_food_points")}");
            if (c.Coins > 0)
                parts.Add($"{c.Coins} {Localization.instance.Localize("$vfh_coins")}");
            return parts.Count > 0 ? string.Join(" + ", parts) : Localization.instance.Localize("$vfh_free");
        }

        private void Clamp(HiringBoard board)
        {
            var rules = new LevelRules(DataStore.Current);
            _level = Mathf.Clamp(_level, 1, rules.MaxHirelingLevel(board.Level));
            float max = rules.MaxWorkRadius(board.Level, Jobs[_job]);
            string key = $"{Jobs[_job]}|{board.Level}";
            if (key != _radiusDefaultedFor)
            {
                _radiusDefaultedFor = key;
                _radius = max;
            }
            _radius = Mathf.Clamp(_radius, LevelRules.MinWorkRadius, max);
            int stances = StanceRules.Allowed(Jobs[_job]).Count;
            if (_stance < 0 || _stance >= stances)
                _stance = IndexOf(StanceRules.Allowed(Jobs[_job]), StanceRules.Default(Jobs[_job]));
        }

        private static int IndexOf(IReadOnlyList<Stance> list, Stance s)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == s)
                    return i;
            return 0;
        }

        private static void Row(RectTransform root, float y, string label, string value, Action prev, Action next)
        {
            PanelUi.Text(root, label, -200f, y, 180f, 20, TextAnchor.MiddleLeft);
            PanelUi.Button(root, "<", -30f, y, 44f, 36f, prev);
            PanelUi.Text(root, value, 90f, y, 180f, 20, bold: true);
            PanelUi.Button(root, ">", 210f, y, 44f, 36f, next);
        }
    }
}
