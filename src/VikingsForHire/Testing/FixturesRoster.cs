using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Testing
{
    internal static class FixturesRoster
    {
        public static void Register()
        {
            Fixtures.Add("stock_board", "<foodPoints> <coins> - put cooked meat and coins in the nearest board", StockBoard);
            Fixtures.Add("board_clear", "- empty the nearest board's storage", _ => BoardClear());
            Fixtures.Add("post", "<job> <level> [radius=20] - post a contract on the nearest board (pays like the panel) and wait for the answer", Post);
            Fixtures.Add("contract", "<cancel|dismiss|promote> - act on the contract last posted, through the real op", Contract);
            Fixtures.Add("skip_days", "<n> - advance the clock n days (like skiptime 1800) and let the board charge upkeep", SkipDays);
            Fixtures.Add("cfg_set", "<key> <value> - set a config value (stays set: put it back after the test)", CfgSet);

            TestHarness.RegisterCheck("roster", "<Pending|Active|Leaving|all> - contracts on the nearest board", args =>
            {
                Roster r = BoardRosterOps.Read(Board().Zdo!);
                string state = args.FirstOrDefault() ?? "all";
                return (state.Equals("all", StringComparison.OrdinalIgnoreCase) ? r.Count
                    : r.Entries.Count(e => e.State.ToString().Equals(state, StringComparison.OrdinalIgnoreCase))).ToString();
            });
            TestHarness.RegisterCheck("roster_entry", "<last|hid-prefix> <state|level|unpaid|radius|stance|respawn|name> - a contract on the nearest board", args =>
            {
                Roster r = BoardRosterOps.Read(Board().Zdo!);
                string sel = args.ElementAtOrDefault(0) ?? "last";
                string hid = sel == "last" ? BoardContracts.LastPostedHid : sel;
                ContractEntry e = r.Entries.FirstOrDefault(x => x.Hid.StartsWith(hid, StringComparison.OrdinalIgnoreCase))
                                  ?? throw new InvalidOperationException($"no contract for {sel}");
                return (args.ElementAtOrDefault(1) ?? "state").ToLowerInvariant() switch
                {
                    "state" => e.State.ToString(),
                    "level" => e.Level.ToString(),
                    "unpaid" => e.UnpaidDays.ToString(),
                    "radius" => e.Radius.ToString(CultureInfo.InvariantCulture),
                    "stance" => e.Stance.ToString(),
                    "respawn" => e.RespawnPending ? "true" : "false",
                    "name" => e.Name,
                    _ => throw new ArgumentException("unknown field"),
                };
            });
            TestHarness.RegisterCheck("contract_gone", "<last|hid-prefix> - true when that contract is no longer on the nearest board", args =>
            {
                string sel = args.ElementAtOrDefault(0) ?? "last";
                string hid = sel == "last" ? BoardContracts.LastPostedHid : sel;
                return BoardRosterOps.Read(Board().Zdo!).Entries.Any(x => x.Hid.StartsWith(hid, StringComparison.OrdinalIgnoreCase)) ? "false" : "true";
            });
            TestHarness.RegisterCheck("last_op", "- outcome of the last contract op: Ok, CapReached, InsufficientFunds…", _ => BoardContracts.LastOutcome);
            TestHarness.RegisterCheck("last_upkeep_day", "- the day the nearest board last charged upkeep", _ => Board().Zdo!.GetInt(BoardZdo.LastUpkeepDay).ToString());
            TestHarness.RegisterCheck("today", "- the current in-game day", _ => EnvMan.instance.GetDay().ToString());
            TestHarness.RegisterCheck("index", "<last|hid> exists - (server) whether the server's index has that hireling", args =>
            {
                string sel = args.ElementAtOrDefault(0) ?? "last";
                string hid = sel == "last" ? BoardContracts.LastPostedHid : sel;
                return WorldIndex.Hireling(hid) != null ? "true" : "false";
            }, serverSide: true);
            TestHarness.RegisterCheck("posted_hireling", "<field> - the hireling from the last posted contract: present, mode, status, level", args =>
            {
                Hireling? h = Hireling.Loaded.FirstOrDefault(x => x != null && x.Hid == BoardContracts.LastPostedHid);
                return (args.ElementAtOrDefault(0) ?? "present").ToLowerInvariant() switch
                {
                    "present" => h != null ? "true" : "false",
                    "mode" => h?.Mode.ToString() ?? "none",
                    "status" => h?.Zdo?.GetString(HirelingZdo.Status) is string s && s.Length > 0 ? s : "none",
                    "level" => h?.Level.ToString() ?? "none",
                    _ => throw new ArgumentException("unknown field"),
                };
            });
        }

        private static HiringBoard Board() =>
            HiringBoard.Nearest(Player.m_localPlayer.transform.position, 50f) ?? throw new InvalidOperationException("no hiring board within 50m");

        private static IEnumerator StockBoard(string[] args)
        {
            HiringBoard board = Board();
            int food = int.Parse(args.ElementAtOrDefault(0) ?? "0", CultureInfo.InvariantCulture);
            int coins = int.Parse(args.ElementAtOrDefault(1) ?? "0", CultureInfo.InvariantCulture);
            GameObject meat = ObjectDB.instance.GetItemPrefab("CookedMeat");
            int perItem = BoardStorage.PointsPerItem(meat.GetComponent<ItemDrop>().m_itemData);
            board.GetComponent<ZNetView>().ClaimOwnership();
            int meats = Mathf.CeilToInt(food / (float)Mathf.Max(1, perItem));
            if (meats > 0)
                board.Inventory!.AddItem(meat, meats);
            if (coins > 0)
                board.Inventory!.AddItem(ObjectDB.instance.GetItemPrefab("Coins"), coins);
            VfhLog.I(LogCat.Test, "fixture.stock_board", ("board", board.Id), ("meat", meats), ("coins", coins), ("funds", BoardStorage.Totals(board.Inventory!).ToString()));
            yield return null;
        }

        private static IEnumerator BoardClear()
        {
            HiringBoard board = Board();
            board.GetComponent<ZNetView>().ClaimOwnership();
            board.Inventory!.RemoveAll();
            VfhLog.I(LogCat.Test, "fixture.board_clear", ("board", board.Id));
            yield return null;
        }

        private static IEnumerator Post(string[] args)
        {
            if (args.Length < 2 || !Enum.TryParse(args[0], true, out JobType job) || !int.TryParse(args[1], out int level))
                throw new ArgumentException("usage: post <job> <level> [radius]");
            float radius = args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 20f;
            bool answered = false;
            BoardContracts.Post(Board(), job, level, radius, StanceRules.Default(job), _ => answered = true);
            for (float t = 0f; !answered && t < 5f; t += Time.deltaTime)
                yield return null;
        }

        private static IEnumerator Contract(string[] args)
        {
            HiringBoard board = Board();
            Roster r = BoardRosterOps.Read(board.Zdo!);
            ContractEntry e = r.Entries.FirstOrDefault(x => x.Hid == BoardContracts.LastPostedHid) ?? throw new InvalidOperationException("no last-posted contract on this board");
            bool answered = false;
            void Done(OpResult _) => answered = true;
            switch (args.ElementAtOrDefault(0))
            {
                case "cancel": BoardContracts.Cancel(board, e.ContractId, Done); break;
                case "dismiss": BoardContracts.Dismiss(board.Id, e.Hid, Done); break;
                case "promote": BoardContracts.Promote(board, e.Hid, e.Level + 1, Done); break;
                default: throw new ArgumentException("usage: contract <cancel|dismiss|promote>");
            }
            for (float t = 0f; !answered && t < 5f; t += Time.deltaTime)
                yield return null;
        }

        private static IEnumerator SkipDays(string[] args)
        {
            int days = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            for (int i = 0; i < days; i++)
            {
                ZNet.instance.SetNetTime(ZNet.instance.GetTimeSeconds() + 1800.0);
                VfhLog.I(LogCat.Test, "fixture.skip_day", ("day", EnvMan.instance.GetDay()));
                // The board charges on its 5s owner tick (inside its 2s poll).
                yield return new WaitForSeconds(7.5f);
            }
        }

        private static IEnumerator CfgSet(string[] args)
        {
            var entry = VfhConfig.Find(args.ElementAtOrDefault(0) ?? "") ?? throw new ArgumentException($"no config key {args.ElementAtOrDefault(0)}");
            entry.SetSerializedValue(args.ElementAtOrDefault(1) ?? "");
            VfhLog.I(LogCat.Test, "fixture.cfg_set", ("key", entry.Definition.Key), ("value", entry.GetSerializedValue()));
            yield return null;
        }
    }
}
