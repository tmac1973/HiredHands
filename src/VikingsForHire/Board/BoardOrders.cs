using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Orders;
using VikingsForHire.Hirelings.Work;
using VikingsForHire.Net;

namespace VikingsForHire.Board
{
    /// <summary>A board's production orders: read from its ZDO (parsed once per change), edited through the board's owner.</summary>
    internal static class BoardOrders
    {
        private static readonly Dictionary<string, (string Raw, OrderList List)> Cache = new();

        public static OrderList For(ZDO? zdo)
        {
            if (zdo == null)
                return new OrderList();
            string raw = zdo.GetString(BoardZdo.Orders);
            string id = BoardZdo.GetId(zdo);
            if (Cache.TryGetValue(id, out var hit) && hit.Raw == raw)
                return hit.List;
            OrderList list = OrderList.Parse(raw);
            Cache[id] = (raw, list);
            return list;
        }

        public static OrderList For(HiringBoard board) => For(board.Zdo);

        /// <summary>The board for a hireling (its home board), if loaded here.</summary>
        public static HiringBoard? BoardOf(string boardId) => HiringBoard.Loaded.FirstOrDefault(b => b != null && b.Id == boardId);

        /// <summary>Stock as orders count it, over the board's largest work radius.</summary>
        public static Stock Stock(HiringBoard board) =>
            StockCounter.Count(board.transform.position, new LevelRules(DataStore.Current).MaxWorkRadius(board.Level));

        /// <summary>The level of the board's Farmer or Cook (0 when it has none).</summary>
        public static int WorkerLevel(HiringBoard board, JobType job) =>
            board.Zdo == null ? 0 : BoardRosterOps.Read(board.Zdo).Entries.Where(e => e.Job == job && e.State != ContractState.Leaving)
                .Select(e => e.Level).DefaultIfEmpty(0).Max();

        public static void Submit(HiringBoard board, OrderEdit edit, string item, OrderKind kind = OrderKind.Crop, int target = 0, bool paused = false) =>
            MutationService.SubmitBoard(board.Id, new RosterOp
            {
                Type = RosterOpType.Orders, Edit = edit, OrderItem = item, OrderKind = (int)kind, OrderTarget = target, OrderPaused = paused,
            }, r => BoardContracts.LastOutcome = r.Outcome.ToString());
    }
}
