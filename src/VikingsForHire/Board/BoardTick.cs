using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Net;
using LogLevel = VikingsForHire.Core.Diagnostics.LogLevel;

namespace VikingsForHire.Board
{
    /// <summary>
    /// The board owner's housekeeping every few seconds: bring in hirelings whose arrival time has come (charging the
    /// respawn fee for returning dead ones) and charge one day of upkeep when a new day starts. Only runs while the board
    /// is loaded, so arrivals wait and days away are free.
    /// </summary>
    internal static class BoardTick
    {
        public static void Run(HiringBoard board)
        {
            ZDO zdo = board.Zdo!;
            int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (zdo.GetInt(BoardZdo.LastUpkeepDay) == 0)
                zdo.Set(BoardZdo.LastUpkeepDay, today);

            Roster roster = BoardRosterOps.Read(zdo);
            bool changed = Arrivals(board, roster);
            changed |= Upkeep(board, roster, today);
            if (changed)
                BoardRosterOps.Write(zdo, roster);
        }

        private static bool Arrivals(HiringBoard board, Roster roster)
        {
            double now = ZNet.instance.GetTimeSeconds();
            bool changed = false;
            var costs = new CostCalculator(DataStore.Current, VfhConfig.RespawnCostFraction.Value);
            foreach (ContractEntry e in roster.Entries.Where(e => e.State == ContractState.Pending && e.ArriveAt <= now).ToList())
            {
                if (e.RespawnPending && !BoardLedger.TryPay(new BoardLedger.Wallet(board.Zdo!), costs.RespawnCost(e.Job, e.Level), out _))
                {
                    VfhLog.Throttled("respawn_wait_" + e.ContractId, 60f, LogLevel.Info, LogCat.Roster, "contract.respawn_waiting",
                        ("board", board.Id), ("contract", e.ContractId), ("name", e.Name), ("cost", costs.RespawnCost(e.Job, e.Level).ToString()));
                    continue;
                }
                string hid = ArrivalSpawner.Spawn(board, e);
                roster.Activate(e.ContractId, hid);
                changed = true;
            }
            return changed;
        }

        private static bool Upkeep(HiringBoard board, Roster roster, int today)
        {
            ZDO zdo = board.Zdo!;
            int last = zdo.GetInt(BoardZdo.LastUpkeepDay);
            if (today <= last)
                return false;
            zdo.Set(BoardZdo.LastUpkeepDay, today);
            if (roster.Active == 0)
                return false;

            var costs = new CostCalculator(DataStore.Current, VfhConfig.RespawnCostFraction.Value);
            var wallet = new BoardLedger.Wallet(zdo);
            int limit = VfhConfig.UnpaidDaysBeforeLeaving.Value;
            UpkeepResult result = roster.ApplyUpkeepDay(e => BoardLedger.TryPay(wallet, costs.DailyUpkeep(e.Job, e.Level), out _), limit);

            foreach (string hid in result.Paid)
                MutationService.SubmitHireling(hid, new HirelingOp { Status = "" });
            foreach ((string hid, int days) in result.Unpaid.Where(u => !result.NowLeaving.Contains(u.Hid)))
                MutationService.SubmitHireling(hid, new HirelingOp { Status = $"$vfh_status_unpaid ({days}/{limit})" });
            foreach (string hid in result.NowLeaving)
                BoardRosterOps.SendAway(hid, "$vfh_status_quit");

            VfhLog.I(LogCat.Payment, "upkeep.day", ("board", board.Id), ("day", today), ("missedDays", today - last - 1), ("paid", result.Paid.Count),
                ("unpaid", result.Unpaid.Count), ("leaving", result.NowLeaving.Count), ("fundsLeft", wallet.Funds.ToString()));
            return true;
        }
    }
}
