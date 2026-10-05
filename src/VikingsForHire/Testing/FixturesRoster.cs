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
        private static Jotunn.Entities.CustomRPC _skipTimeRpc = null!;

        public static void Register()
        {
            _skipTimeRpc = Jotunn.Managers.NetworkManager.Instance.AddRPC("VFH_TestSkipTime", OnServerSkipTime, OnClientSkipTimeIgnored);
            Fixtures.Add("stock_board", "<foodPoints> <coins> - put cooked meat and coins in the nearest board", StockBoard);
            Fixtures.Add("board_clear", "- empty the nearest board's storage", _ => BoardClear());
            Fixtures.Add("post", "<job> <level> [radius=20] [free] - post a contract on the nearest board (pays like the panel, or nothing with free) and wait for the answer", Post);
            Fixtures.Add("contract", "<cancel|dismiss|promote> - act on the contract last posted, through the real op", Contract);
            Fixtures.Add("skip_time", "<seconds> - move the world clock on (up to 1500 s, under a day so no upkeep is charged): stations, fermenters and beehives catch up as after sleeping", SkipTime);
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
            TestHarness.RegisterCheck("upkeep_charged_today", "- true when the nearest board has charged today's upkeep",
                _ => Board().Zdo!.GetInt(BoardZdo.LastUpkeepDay) == EnvMan.instance.GetDay() ? "true" : "false");
            TestHarness.RegisterCheck("index", "<last|hid> exists - (server) whether the server's index has that hireling", args =>
            {
                string sel = args.ElementAtOrDefault(0) ?? "last";
                string hid = sel == "last" ? BoardContracts.LastPostedHid : sel;
                return WorldIndex.Hireling(hid) != null ? "true" : "false";
            }, serverSide: true,
            // "last" means the contract this client posted, which only the client knows.
            prepareArgs: args => args.Select((a, i) => i == 0 && a == "last" ? BoardContracts.LastPostedHid : a).ToArray());
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
                throw new ArgumentException("usage: post <job> <level> [radius] [free]");
            float radius = args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 20f;
            // free: no hire fee (high levels cost more than a board's 8 slots hold under some price tables).
            bool free = args.Skip(3).Any(a => a == "free");
            bool answered = false;
            BoardContracts.Post(Board(), job, level, radius, StanceRules.Default(job), _ => answered = true, free);
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

        private static IEnumerator SkipTime(string[] args)
        {
            double seconds = double.Parse(args.ElementAtOrDefault(0) ?? "300", CultureInfo.InvariantCulture);
            if (seconds <= 0 || seconds > 1500)
                throw new System.InvalidOperationException("skip_time takes 1-1500 seconds (a day is 1800: use skip_days for whole days)");
            double before = ZNet.instance.GetTimeSeconds();
            if (ZNet.instance.IsServer())
            {
                ZNet.instance.SetNetTime(before + seconds);
            }
            else
            {
                var pkg = new ZPackage();
                pkg.Write(seconds);
                _skipTimeRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
                for (float waited = 0f; ZNet.instance.GetTimeSeconds() < before + seconds - 100.0 && waited < 10f; waited += 0.25f)
                    yield return new WaitForSeconds(0.25f);
            }
            VfhLog.I(LogCat.Test, "fixture.skip_time", ("seconds", seconds), ("server", ZNet.instance.IsServer()));
            // Stations work out the skipped time on their next update.
            yield return new WaitForSeconds(2f);
        }

        private static IEnumerator SkipDays(string[] args)
        {
            int days = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            for (int i = 0; i < days; i++)
            {
                double before = ZNet.instance.GetTimeSeconds();
                if (ZNet.instance.IsServer())
                {
                    ZNet.instance.SetNetTime(before + 1800.0);
                }
                else
                {
                    // The server's clock is the one that counts: ask it to move on, then wait for it to reach us.
                    var pkg = new ZPackage();
                    pkg.Write(1800.0);
                    _skipTimeRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
                    for (float waited = 0f; ZNet.instance.GetTimeSeconds() < before + 1700.0 && waited < 10f; waited += 0.25f)
                        yield return new WaitForSeconds(0.25f);
                }
                VfhLog.I(LogCat.Test, "fixture.skip_day", ("day", EnvMan.instance.GetDay()), ("server", ZNet.instance.IsServer()));
                // The board charges on its 5s owner tick (inside its 2s poll).
                yield return new WaitForSeconds(7.5f);
            }
        }

        // Server: a test client (admin only) moves the world clock on, like devcommands' skiptime.
        private static IEnumerator OnServerSkipTime(long sender, ZPackage pkg)
        {
            double seconds = pkg.ReadDouble();
            ZNetPeer? peer = ZNet.instance.GetPeer(sender);
            if (peer == null || !ZNet.instance.IsAdmin(peer.m_socket.GetHostName()))
            {
                VfhLog.W(LogCat.Test, "fixture.skip_time_refused", ("from", sender));
                yield break;
            }
            ZNet.instance.SetNetTime(ZNet.instance.GetTimeSeconds() + seconds);
            VfhLog.I(LogCat.Test, "fixture.skip_time", ("from", sender), ("seconds", seconds), ("day", EnvMan.instance.GetDay()));
        }

        private static IEnumerator OnClientSkipTimeIgnored(long sender, ZPackage pkg)
        {
            yield break;
        }

        private static IEnumerator CfgSet(string[] args)
        {
            var entry = VfhConfig.Find(args.ElementAtOrDefault(0) ?? "") ?? throw new ArgumentException($"no config key {args.ElementAtOrDefault(0)}");
            // Through BoxedValue, not SetSerializedValue: Jotunn blocks the latter for server-synced entries on a client.
            // As an admin, the change is sent on to the server and every client.
            entry.BoxedValue = BepInEx.Configuration.TomlTypeConverter.ConvertToValue(args.ElementAtOrDefault(1) ?? "", entry.SettingType);
            VfhLog.I(LogCat.Test, "fixture.cfg_set", ("key", entry.Definition.Key), ("value", VfhConfig.EffectiveValue(entry)));
            yield return null;
        }
    }
}
