using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>
    /// Warnings before a hiring board runs dry (food and coins counted separately, against the daily upkeep of its active
    /// contracts):
    /// - the board's hover shows the daily cost and the days left, orange when low and red on the last day;
    /// - once a day, when the upkeep is paid and it's low, the players at the base are told;
    /// - the player who built a board gets a map pin on it while it's low (the server answers each player's game once a
    ///   minute, since boards far away aren't loaded there). Pins are local to each game and never saved;
    /// - when a day's wages go unpaid, the players at the base are told how many and what the board is short;
    /// - coming within reach of a board that can't pay the next day's wages, a player is told (again after 10 minutes),
    ///   with a hint when the board is full: cheap food fills its few slots without covering a big crew's wages.
    /// </summary>
    internal static class LowFunds
    {
        private const float PinSeconds = 60f;
        private const float TellRadius = 64f;
        private static CustomRPC _rpc = null!;
        private static float _nextPins;
        private static readonly Dictionary<string, Minimap.PinData> Pins = new();
        private static readonly Dictionary<HiringBoard, (float At, Cost Daily)> DailyCache = new();
        private static readonly Dictionary<string, float> LoggedLow = new();
        private const float ArrivalCheckSeconds = 5f;
        private const float ArrivalRepeatSeconds = 600f;
        private static float _nextArrivalCheck;
        private static readonly Dictionary<string, float> ToldCantPay = new();

        public static void Register() => _rpc = NetworkManager.Instance.AddRPC("VFH_LowFunds", OnServer, OnClient);

        public static Cost DailyUpkeep(Roster roster)
        {
            var costs = new CostCalculator(DataStore.Current, VfhConfig.RespawnCostFraction.Value);
            Cost total = Cost.Zero;
            foreach (ContractEntry e in roster.Entries.Where(e => e.State == ContractState.Active))
                total += costs.DailyUpkeep(e.Job, e.Level);
            return total;
        }

        public static bool IsLow(int days) => VfhConfig.LowFundsWarnDays.Value > 0 && days <= VfhConfig.LowFundsWarnDays.Value;

        /// <summary>The board hover's upkeep line (empty with nothing to pay). The daily cost is cached for a second.</summary>
        public static string HoverLines(HiringBoard board, Cost funds)
        {
            if (board.Zdo == null)
                return "";
            if (!DailyCache.TryGetValue(board, out var cached) || Time.time - cached.At > 1f)
                DailyCache[board] = cached = (Time.time, DailyUpkeep(BoardRosterOps.Read(board.Zdo)));
            Cost daily = cached.Daily;
            if (daily.FoodPoints == 0 && daily.Coins == 0)
                return "";
            (int foodDays, int coinDays) = FundsForecast.DaysLeft(funds, daily);
            int days = System.Math.Min(foodDays, coinDays);
            string color = days <= 1 ? "#ff5050" : IsLow(days) ? "orange" : "#c8c8c8";
            // Upkeep is food only by default; coins are mentioned only when the server's data file charges them daily.
            string lasts = daily.Coins > 0
                ? Localization.instance.Localize("$vfh_board_lasts", foodDays == int.MaxValue ? "—" : foodDays.ToString(), coinDays.ToString())
                : Localization.instance.Localize("$vfh_board_lasts_food", foodDays == int.MaxValue ? "—" : foodDays.ToString());
            string full = IsLow(days) && board.Inventory != null && board.Inventory.GetEmptySlots() == 0 && daily.FoodPoints > 0
                ? "\n<color=orange>$vfh_board_full_richer</color>" : "";
            return $"\n$vfh_board_upkeep {UI.ContractsTab.Price(daily)}\n<color={color}>{lasts}</color>{full}";
        }

        /// <summary>After a day's upkeep (on the machine running the board): tell the players at the base if it's low.</summary>
        public static void AfterUpkeep(HiringBoard board, Roster roster, Cost fundsLeft, int unpaid)
        {
            if (!VfhConfig.LowFundsMessages.Value)
                return;
            Cost daily = DailyUpkeep(roster);
            if (daily.FoodPoints == 0 && daily.Coins == 0)
                return;
            (int foodDays, int coinDays) = FundsForecast.DaysLeft(fundsLeft, daily);
            int days = System.Math.Min(foodDays, coinDays);
            string text;
            if (unpaid > 0)
                text = Localization.instance.Localize("$vfh_upkeep_missed", unpaid.ToString(), daily.FoodPoints.ToString(), fundsLeft.FoodPoints.ToString(),
                    VfhConfig.UnpaidDaysBeforeLeaving.Value.ToString());
            else if (IsLow(days))
                text = Localization.instance.Localize(foodDays <= coinDays ? "$vfh_low_funds_food" : "$vfh_low_funds_coins", days.ToString());
            else
                return;
            int told = TellAtBase(board.transform.position, text);
            VfhLog.I(LogCat.Payment, unpaid > 0 ? "funds.missed" : "funds.low", ("board", board.Id), ("unpaid", unpaid), ("foodDays", foodDays),
                ("coinDays", coinDays), ("told", told));
        }

        private static int TellAtBase(Vector3 at, string text)
        {
            var players = new List<Player>();
            Player.GetPlayersInRange(at, TellRadius, players);
            foreach (Player p in players)
            {
                if (p == Player.m_localPlayer)
                    p.Message(MessageHud.MessageType.Center, text);
                else if (p.GetComponent<ZNetView>()?.GetZDO() is ZDO pz)
                    ZRoutedRpc.instance.InvokeRoutedRPC(pz.GetOwner(), "ShowMessage", (int)MessageHud.MessageType.Center, text);
            }
            return players.Count;
        }

        /// <summary>
        /// Client side, every few seconds: the local player near a board (loaded here) that can't pay the next day's wages
        /// is told, once per board every 10 minutes.
        /// </summary>
        private static void ArrivalCheck()
        {
            if (Time.time < _nextArrivalCheck || !VfhConfig.LowFundsMessages.Value)
                return;
            _nextArrivalCheck = Time.time + ArrivalCheckSeconds;
            Player me = Player.m_localPlayer;
            foreach (HiringBoard board in HiringBoard.Loaded)
            {
                if (board == null || board.Zdo == null || board.Inventory == null || Vector3.Distance(board.transform.position, me.transform.position) > TellRadius)
                    continue;
                Cost daily = DailyUpkeep(BoardRosterOps.Read(board.Zdo));
                Cost funds = BoardStorage.Totals(board.Inventory);
                if (FundsForecast.Days(funds, daily) >= 1)
                {
                    ToldCantPay.Remove(board.Id);
                    continue;
                }
                if (ToldCantPay.TryGetValue(board.Id, out float at) && Time.time - at < ArrivalRepeatSeconds)
                    continue;
                ToldCantPay[board.Id] = Time.time;
                bool food = daily.FoodPoints > 0 && funds.FoodPoints < daily.FoodPoints;
                string text = food
                    ? Localization.instance.Localize("$vfh_board_cant_pay_food", funds.FoodPoints.ToString(), daily.FoodPoints.ToString())
                    : Localization.instance.Localize("$vfh_board_cant_pay_coins", funds.Coins.ToString(), daily.Coins.ToString());
                if (food && board.Inventory.GetEmptySlots() == 0)
                    text += "\n" + Localization.instance.Localize("$vfh_board_full_richer");
                me.Message(MessageHud.MessageType.Center, text);
                VfhLog.I(LogCat.Payment, "funds.cant_pay_told", ("board", board.Id), ("funds", funds.ToString()), ("daily", daily.ToString()),
                    ("full", board.Inventory.GetEmptySlots() == 0));
            }
        }

        /// <summary>Every frame (Plugin.Update), client side: ask the server once a minute which of your boards are low.</summary>
        public static void Tick()
        {
            if (Player.m_localPlayer != null)
                ArrivalCheck();
            if (Time.time < _nextPins || ZNet.instance == null || Player.m_localPlayer == null || Minimap.instance == null)
                return;
            _nextPins = Time.time + PinSeconds;
            if (!VfhConfig.LowFundsMapPins.Value || VfhConfig.LowFundsWarnDays.Value <= 0)
            {
                Show(new List<(string, Vector3, string)>());
                return;
            }
            var pkg = new ZPackage();
            pkg.Write(Player.m_localPlayer.GetPlayerID());
            if (ZNet.instance.IsServer())
                Show(LowBoardsOf(Player.m_localPlayer.GetPlayerID()));
            else
                _rpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        // Server: the boards this player built that are low, with a label for the pin.
        private static List<(string Id, Vector3 Pos, string Label)> LowBoardsOf(long pid)
        {
            var list = new List<(string, Vector3, string)>();
            foreach (ZDO zdo in WorldIndex.AllBoards())
            {
                if (zdo.GetLong(ZDOVars.s_creator) != pid)
                    continue;
                Cost daily = DailyUpkeep(BoardRosterOps.Read(zdo));
                if (daily.FoodPoints == 0 && daily.Coins == 0)
                    continue;
                var wallet = new BoardLedger.Wallet(zdo);
                Cost funds = wallet.Funds;
                (int foodDays, int coinDays) = FundsForecast.DaysLeft(funds, daily);
                int days = System.Math.Min(foodDays, coinDays);
                if (!IsLow(days))
                    continue;
                string boardId = zdo.GetString(BoardZdo.Id);
                if (!LoggedLow.TryGetValue(boardId, out float at) || Time.time - at > 600f)
                {
                    LoggedLow[boardId] = Time.time;
                    VfhLog.I(LogCat.Payment, "funds.pin_low", ("board", boardId), ("funds", $"{funds.FoodPoints}fp+{funds.Coins}c"),
                        ("daily", $"{daily.FoodPoints}fp+{daily.Coins}c"), ("stacks", wallet.Inventory.GetAllItems().Count),
                        ("items", string.Join(",", wallet.Inventory.GetAllItems().Select(i => $"{(i.m_dropPrefab != null ? i.m_dropPrefab.name : i.m_shared.m_name)}x{i.m_stack}"))),
                        ("savedLength", zdo.GetByteArray(ZDOVars.s_items)?.Length ?? 0), ("dataRev", zdo.DataRevision));
                }
                string label = Localization.instance.Localize(foodDays <= coinDays ? "$vfh_pin_low_food" : "$vfh_pin_low_coins", days.ToString());
                list.Add((zdo.GetString(BoardZdo.Id), zdo.GetPosition(), label));
            }
            return list;
        }

        private static List<(string Id, Vector3 Pos, string Label)> Local(List<(string Id, Vector3 Pos, string Label)> low)
        {
            if (Player.m_localPlayer == null || low.Count == 0)
                return low;
            long me = Player.m_localPlayer.GetPlayerID();
            var result = new List<(string, Vector3, string)>();
            foreach ((string id, Vector3 pos, string label) in low)
            {
                HiringBoard? board = HiringBoard.Loaded.FirstOrDefault(b => b != null && b.Id == id);
                Inventory? inv = board != null ? board.Inventory : null;
                if (board == null || board.Zdo == null || inv == null || board.Zdo.GetLong(ZDOVars.s_creator) != me)
                {
                    result.Add((id, pos, label));
                    continue;
                }
                Cost daily = DailyUpkeep(BoardRosterOps.Read(board.Zdo));
                (int foodDays, int coinDays) = FundsForecast.DaysLeft(BoardStorage.Totals(inv), daily);
                int days = System.Math.Min(foodDays, coinDays);
                if (IsLow(days))
                    result.Add((id, pos, Localization.instance.Localize(foodDays <= coinDays ? "$vfh_pin_low_food" : "$vfh_pin_low_coins", days.ToString())));
            }
            return result;
        }

        private static IEnumerator OnServer(long sender, ZPackage pkg)
        {
            VfhLog.Guard(LogCat.Board, "funds.pins_failed", () =>
            {
                long pid = pkg.ReadLong();
                // Only for the player asking.
                if (FollowerServer.PlayerIdOf(sender) != pid)
                    return;
                var reply = new ZPackage();
                List<(string Id, Vector3 Pos, string Label)> low = LowBoardsOf(pid);
                reply.Write(low.Count);
                foreach ((string id, Vector3 pos, string label) in low)
                {
                    reply.Write(id);
                    reply.Write(pos);
                    reply.Write(label);
                }
                _rpc.SendPackage(sender, reply);
            });
            yield break;
        }

        private static IEnumerator OnClient(long sender, ZPackage pkg)
        {
            VfhLog.Guard(LogCat.Board, "funds.pins_failed", () =>
            {
                int n = pkg.ReadInt();
                var low = new List<(string, Vector3, string)>();
                for (int i = 0; i < n; i++)
                    low.Add((pkg.ReadString(), pkg.ReadVector3(), pkg.ReadString()));
                Show(low);
            });
            yield break;
        }

        // Keep exactly one pin per low board; remove the rest. A board loaded here is judged on what this game sees in it
        // (as its hover is), not on the server's copy.
        private static void Show(List<(string Id, Vector3 Pos, string Label)> low)
        {
            if (Minimap.instance == null)
                return;
            low = Local(low);
            var keep = new HashSet<string>(low.Select(l => l.Id));
            foreach (string id in Pins.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                Minimap.instance.RemovePin(Pins[id]);
                Pins.Remove(id);
            }
            foreach ((string id, Vector3 pos, string label) in low)
            {
                if (Pins.TryGetValue(id, out Minimap.PinData pin))
                {
                    if (pin.m_name == label)
                        continue;
                    Minimap.instance.RemovePin(pin);
                }
                Pins[id] = Minimap.instance.AddPin(pos, Minimap.PinType.Icon3, label, save: false, isChecked: false);
            }
        }
    }
}
