using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Keeps its own board's food stocked: when the board has fewer than StewardBoardRefillDays of upkeep left (the
    /// hover's count), it fetches food from the chests, cheapest first (as the board pays upkeep, so the best food stays
    /// for players), up to StewardBoardFillDays, and puts it on the board. Only food the board accepts; never the last of
    /// a food in a chest, and keepInStorage reserves are left alone.
    /// </summary>
    internal sealed class BoardChore : IChore
    {
        private const float StuckSeconds = 20f;
        private const float ChestSkipSeconds = 60f;
        private const int MaxChests = 4;

        private readonly WorkSteps _walk = new();
        private HiringBoard? _board;
        private Dictionary<string, int> _want = new();
        private Container? _chest;
        private int _chestsVisited;
        private bool _fetching;

        public ChoreKind Kind => ChoreKind.Board;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        private static HiringBoard? BoardOf(WorkContext ctx) =>
            HiringBoard.Loaded.FirstOrDefault(b => b != null && b.Zdo != null && b.Id == ctx.Hireling.BoardId && b.Inventory != null && b.Storage != null);

        // Food points a day the roster costs, what the board holds, and how many days that lasts.
        private static (int Daily, int Funds, int Days) Stock(HiringBoard board)
        {
            if (board.Zdo == null || board.Inventory == null)
                return (0, 0, int.MaxValue);
            Cost daily = LowFunds.DailyUpkeep(BoardRosterOps.Read(board.Zdo));
            Cost funds = BoardStorage.Totals(board.Inventory);
            return (daily.FoodPoints, funds.FoodPoints, FundsForecast.DaysLeft(funds, daily).FoodDays);
        }

        // Foods the chests can spare (above reserves) with their points each, cheapest first.
        private static List<(string Prefab, int Points, int Have)> Foods(WorkContext ctx)
        {
            var seen = new Dictionary<string, int>();
            foreach (Container c in ctx.Chests)
                foreach (ItemDrop.ItemData i in c.GetInventory().GetAllItems())
                    if (i.m_dropPrefab != null && !BoardStorage.IsCoins(i) && BoardStorage.IsFood(i) && !seen.ContainsKey(i.m_dropPrefab.name))
                        seen[i.m_dropPrefab.name] = BoardStorage.PointsPerItem(i);
            return seen.Where(kv => kv.Value > 0)
                .Select(kv => (kv.Key, kv.Value, ctx.Available(kv.Key) + (ctx.Carried.TryGetValue(kv.Key, out int c) ? c : 0)))
                .Where(f => f.Item3 > 0)
                .OrderBy(f => f.Item2).ThenBy(f => f.Key)
                .ToList();
        }

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            HiringBoard? board = BoardOf(ctx);
            if (board == null || Reservations.IsSkipped(board))
                return Enumerable.Empty<ChoreJob>();
            (int daily, int funds, int days) = Stock(board);
            float u = ChoreUrgency.Board(daily, days, VfhConfig.StewardBoardRefillDays.Value);
            if (u <= 0f)
                return Enumerable.Empty<ChoreJob>();
            if (Foods(ctx).Count == 0)
            {
                Missing = "$vfh_need_board_food";
                return Enumerable.Empty<ChoreJob>();
            }
            return new[]
            {
                new ChoreJob
                {
                    Kind = Kind, Target = board, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, board.transform.position), ctx.Radius),
                    Label = "$vfh_steward_board",
                },
            };
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            RestAfter = 0f;
            _walk.Reset();
            _board = (HiringBoard)job.Target;
            _chestsVisited = 0;
            ctx.Hireling.HoldDeliveries = true;
            (int daily, int funds, _) = Stock(_board);
            int points = Mathf.Max(0, VfhConfig.StewardBoardFillDays.Value * daily - funds);
            _want = Plan(ctx, points);
            _chest = NextChest(ctx);
            _fetching = _chest != null;
            VfhLog.D(LogCat.Smelter, "steward.board_plan", ("hid", ctx.Hireling.Hid), ("daily", daily), ("funds", funds), ("points", points),
                ("want", string.Join(",", _want.Select(kv => $"{kv.Key}x{kv.Value}"))));
        }

        // Cheapest first until the points are covered, as far as cargo slots go (whole stacks of what it already carries count).
        private static Dictionary<string, int> Plan(WorkContext ctx, int points)
        {
            var want = new Dictionary<string, int>();
            int slots = ctx.FreeSlots;
            foreach ((string prefab, int each, int have) in Foods(ctx))
            {
                if (points <= 0)
                    break;
                int carried = ctx.Carried.TryGetValue(prefab, out int c) ? c : 0;
                int stack = Mathf.Max(1, WorkSteps.MaxStack(prefab));
                int fit = (carried % stack == 0 ? 0 : stack - carried % stack) + slots * stack;
                int n = Mathf.Min(have - carried, Mathf.Min(fit, Mathf.CeilToInt(points / (float)each)));
                int total = carried + Mathf.Max(0, n);
                if (total <= 0)
                    continue;
                want[prefab] = total;
                points -= total * each;
                slots -= Mathf.CeilToInt(Mathf.Max(0, n - (carried % stack == 0 ? 0 : stack - carried % stack)) / (float)stack);
                if (slots <= 0 && points > 0)
                    break;
            }
            return want;
        }

        // The nearest chest holding any food still to fetch, up to a few chests a trip.
        private Container? NextChest(WorkContext ctx)
        {
            if (_chestsVisited >= MaxChests)
                return null;
            var short_ = _want.Where(kv => (ctx.Hireling.CargoInventory!.CountItems(WorkSteps.SharedName(kv.Key)) < kv.Value)).Select(kv => kv.Key).ToList();
            return short_.Count == 0 ? null : ctx.NearestChestWith(short_);
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            HiringBoard? board = _board;
            if (board == null || board.Storage == null || board.Inventory == null)
                return End(h, 0f);
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "steward.board_stuck", ("hid", h.Hid), ("pos", h.transform.position), ("fetching", _fetching));
                if (_fetching && _chest != null)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                return End(h, 5f, ChoreProgress.Failed);
            }
            if (_fetching)
            {
                Container? chest = _chest;
                if (chest == null)
                    return End(h, 0f);
                if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                var need = _want.ToDictionary(kv => kv.Key, kv => Mathf.Max(0, kv.Value - h.CargoInventory!.CountItems(WorkSteps.SharedName(kv.Key))));
                if (WorkSteps.TakeFromChest(chest, h, need.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value), VfhConfig.ChestReserve) == 0)
                    Reservations.Skip(chest, ChestSkipSeconds);
                _chestsVisited++;
                _chest = NextChest(new WorkContext(h));
                _fetching = _chest != null;
                _walk.Reset();
                return ChoreProgress.Running;
            }
            if (!_want.Keys.Any(p => h.CargoInventory!.CountItems(WorkSteps.SharedName(p)) > 0))
                return End(h, 3f);
            if (!_walk.Approach(ai, dt, board, board.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(board.transform.position);
            int moved = 0;
            foreach (KeyValuePair<string, int> kv in _want)
                moved += ContainerAccess.Deposit(board.Storage, h.CargoInventory!, kv.Key, kv.Value, h.Hid);
            VfhLog.I(LogCat.Smelter, "steward.board_stocked", ("hid", h.Hid), ("board", board.Id), ("items", moved),
                ("food", string.Join(",", _want.Select(kv => $"{kv.Key}x{kv.Value}"))));
            // Whatever didn't fit (a full board) stays in cargo; the leftover rule takes it back to a chest.
            return End(h, 1f, moved > 0 ? ChoreProgress.Done : ChoreProgress.Failed);
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            h.HoldDeliveries = false;
            _board = null;
            _chest = null;
            _fetching = false;
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h) => End(h, 0f);
    }
}
