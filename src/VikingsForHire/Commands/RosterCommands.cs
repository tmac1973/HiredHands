using System;
using System.Linq;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Commands
{
    internal static class RosterCommands
    {
        public static void Register()
        {
            var jobs = Enum.GetNames(typeof(JobType)).ToList();
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_spawn_contract", "<job> <level> - hire for the nearest board for free; arrives at once",
                true, args =>
                {
                    HiringBoard board = HiringBoard.Nearest(Player.m_localPlayer.transform.position, 50f) ?? throw new InvalidOperationException("no hiring board within 50m");
                    if (args.Length < 2 || !Enum.TryParse(args[0], true, out JobType job) || !int.TryParse(args[1], out int level))
                    {
                        VfhCommand.Print("Usage: vfh_spawn_contract <job> <level>");
                        return;
                    }
                    BoardContracts.Post(board, job, level, 20f, StanceRules.Default(job), free: true);
                }, jobs));
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_dismiss_contract", "<hid-prefix> - dismiss a hireling you hired, from anywhere",
                true, args =>
                {
                    string prefix = args.FirstOrDefault() ?? "";
                    var match = BoardContracts.BoardOfHid.FirstOrDefault(kv => prefix.Length > 0 && kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (match.Key == null)
                    {
                        VfhCommand.Print("Unknown hireling: use the id prefix of one you hired this session (see the log, hid=…)");
                        return;
                    }
                    BoardContracts.Dismiss(match.Value, match.Key);
                }));
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_dump_index", "- (server/single-player) log every board and hireling the server knows of",
                false, _ =>
                {
                    if (ZNet.instance == null || !ZNet.instance.IsServer())
                    {
                        VfhCommand.Print("vfh_dump_index only works on the server or in single-player");
                        return;
                    }
                    var hirelings = WorldIndex.AllHirelings().ToList();
                    VfhLog.I(LogCat.Roster, "dump.index", ("boards", WorldIndex.BoardCount), ("hirelings", hirelings.Count));
                    foreach (ZDO z in hirelings)
                        VfhLog.I(LogCat.Roster, "dump.index_hireling", ("hid", z.GetString(HirelingZdo.Hid)), ("board", z.GetString(HirelingZdo.BoardId)),
                            ("mode", (HirelingMode)z.GetInt(HirelingZdo.Mode)), ("owner", z.GetOwner()), ("pos", z.GetPosition()));
                    VfhCommand.Print($"HiredHands: {WorldIndex.BoardCount} boards, {hirelings.Count} hirelings (details in the log)");
                }));
            DebugCommands.DumpStateSections.Add(DumpRosters);
        }

        private static void DumpRosters()
        {
            foreach (HiringBoard b in HiringBoard.Loaded.Where(b => b != null && b.Zdo != null))
            {
                Roster roster = BoardRosterOps.Read(b.Zdo!);
                VfhLog.I(LogCat.Roster, "dump.roster", ("board", b.Id), ("entries", roster.Count), ("active", roster.Active), ("pending", roster.Pending),
                    ("leaving", roster.Leaving), ("lastUpkeepDay", b.Zdo!.GetInt(BoardZdo.LastUpkeepDay)), ("today", EnvMan.instance.GetDay()));
                double now = ZNet.instance.GetTimeSeconds();
                foreach (ContractEntry e in roster.Entries)
                    VfhLog.I(LogCat.Roster, "dump.contract", ("board", b.Id), ("contract", e.ContractId), ("hid", e.Hid), ("name", e.Name), ("job", e.Job),
                        ("level", e.Level), ("state", e.State), ("radius", e.Radius), ("stance", e.Stance), ("unpaid", e.UnpaidDays),
                        ("arriveIn", e.State == ContractState.Pending ? e.ArriveAt - now : 0), ("respawn", e.RespawnPending), ("paid", e.Paid));
            }
        }
    }
}
