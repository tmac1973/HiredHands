using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Commands
{
    /// <summary>A console command from a delegate. Bodies run inside VfhLog.Guard, so a bug can't break the console.</summary>
    internal sealed class VfhCommand : ConsoleCommand
    {
        private readonly Action<string[]> _run;
        private readonly bool _cheat;
        private readonly List<string>? _options;

        public VfhCommand(string name, string help, bool cheat, Action<string[]> run, List<string>? options = null)
        {
            Name = name;
            Help = help;
            _cheat = cheat;
            _run = run;
            _options = options;
        }

        public override string Name { get; }
        public override string Help { get; }
        public override bool IsCheat => _cheat;

        public override void Run(string[] args) =>
            VfhLog.Guard(LogCat.Core, "command.failed", () => _run(args), ("cmd", Name));

        public override List<string> CommandOptionList() => _options ?? new List<string>();

        /// <summary>Prints to the in-game console (when there is one) and to the log.</summary>
        public static void Print(string text)
        {
            foreach (string line in text.Split('\n'))
                Console.instance?.AddString(line);
        }
    }

    internal static class DebugCommands
    {
        /// <summary>Later phases add sections (boards, hirelings, roster) to vfh_dump_state.</summary>
        public static readonly List<Action> DumpStateSections = new();

        public static void Register()
        {
            var cats = Enum.GetNames(typeof(LogCat)).Append("All").ToList();

            Add(new VfhCommand("vfh_debug", "<category|All> <on|off|trace> - change log verbosity (saved to the cfg)", false, Debug, cats));
            Add(new VfhCommand("vfh_log_mark", "<text> - write a marker line to the logs to bracket a bug repro", false, args =>
            {
                string text = args.Length == 0 ? "mark" : string.Join(" ", args);
                VfhLog.I(LogCat.Core, "mark", ("text", text));
                VfhCommand.Print($"VikingsForHire: marked '{text}'");
            }));
            Add(new VfhCommand("vfh_dump_data", "- print the data tables in effect (server's when connected)", false, _ => DumpData()));
            Add(new VfhCommand("vfh_perf", "- hireling AI cost on this machine (rolling 10 s average)", false, _ =>
            {
                string report = PerfCounters.Report();
                VfhCommand.Print(report);
                VfhLog.I(LogCat.Perf, "perf.report", ("aiMsPerFrame", PerfCounters.LastAiMsPerFrame), ("text", report.Replace("\n", " |")));
            }));
            Add(new VfhCommand("vfh_dump_state", "- log the session header and every loaded board and hireling", false, _ => DumpState()));
            Add(new VfhCommand("vfh_debug_throw", "- (test) throw inside a guarded command to check exception logging", true,
                _ => throw new InvalidOperationException("vfh_debug_throw test exception")));
        }

        private static void Add(ConsoleCommand cmd) => CommandManager.Instance.AddConsoleCommand(cmd);

        private static void Debug(string[] args)
        {
            if (args.Length < 2 || !new[] { "on", "off", "trace" }.Contains(args[1].ToLowerInvariant()))
            {
                VfhCommand.Print($"Usage: vfh_debug <category|All> <on|off|trace>. Debug: {LogFilter.FormatCategories(VfhLog.Filter.DebugCats)}; Trace: {LogFilter.FormatCategories(VfhLog.Filter.TraceCats)}");
                return;
            }

            HashSet<LogCat> picked = LogFilter.ParseCategories(args[0], out List<string> unknown);
            if (unknown.Count > 0 || picked.Count == 0)
            {
                VfhCommand.Print($"Unknown category '{args[0]}'. Use one of: {string.Join(", ", Enum.GetNames(typeof(LogCat)))}, All");
                return;
            }

            var debug = new HashSet<LogCat>(VfhLog.Filter.DebugCats.Except(VfhLog.Filter.TraceCats));
            var trace = new HashSet<LogCat>(VfhLog.Filter.TraceCats);
            switch (args[1].ToLowerInvariant())
            {
                case "on":
                    debug.UnionWith(picked);
                    trace.ExceptWith(picked);
                    break;
                case "trace":
                    trace.UnionWith(picked);
                    break;
                default:
                    debug.ExceptWith(picked);
                    trace.ExceptWith(picked);
                    break;
            }

            // Saving the cfg entries re-applies the filter through their SettingChanged handlers.
            VfhConfig.DebugCategories.Value = LogFilter.FormatCategories(debug);
            VfhConfig.TraceCategories.Value = LogFilter.FormatCategories(trace);
            VfhLog.I(LogCat.Core, "debug.categories", ("debug", VfhConfig.DebugCategories.Value), ("trace", VfhConfig.TraceCategories.Value));
            VfhCommand.Print($"VikingsForHire debug: [{VfhConfig.DebugCategories.Value}] trace: [{VfhConfig.TraceCategories.Value}]");
        }

        private static void DumpData()
        {
            VfhData d = DataStore.Current;
            var lines = new List<string> { $"VikingsForHire data: source={DataStore.Source} hash={DataStore.Hash} reloads={DataStore.Reloads}" };
            foreach (BoardLevelData b in d.BoardLevels)
                lines.Add($"  board L{b.Level}: cap {b.HirelingCap}, radius {b.MaxWorkRadius}m, cost {Cost(b.Cost)}");
            foreach (HirelingLevelData h in d.HirelingLevels)
                lines.Add($"  hireling L{h.Level}: hp {h.Health}, armor {h.Armor}, dmg x{h.GuardDamageMult}, gather x{h.GatherMult}, slots {h.CargoSlots}, " +
                          $"hire {h.HireFood}fp+{h.HireCoins}c, upkeep {h.UpkeepFood}fp+{h.UpkeepCoins}c/day");
            foreach (var job in d.Jobs)
                lines.Add($"  job {job.Key}: cost x{job.Value.CostMult}, combat x{job.Value.WorkerCombatFactor}, gear {string.Join("/", job.Value.Gear.Select(g => g.Main))}");
            foreach (StoneLevelData s in d.CommandStone)
                lines.Add($"  stone Q{s.Quality}: board L{s.RequiredBoardLevel}, followers {s.FollowerCap}, cost {Cost(s.Cost)}");
            lines.Add($"  config: {string.Join(", ", VfhConfig.AllEffective().Where(c => !c.Key.StartsWith("6 ") && !c.Key.StartsWith("7 ")).Select(c => c.Key.Split('/')[1] + "=" + c.Value))}");

            foreach (string line in lines)
                VfhCommand.Print(line);
            VfhLog.I(LogCat.Data, "dump.data", ("source", DataStore.Source), ("hash", DataStore.Hash), ("reloads", DataStore.Reloads));
            VfhLog.Raw(string.Join("\n", lines));
        }

        private static void DumpState()
        {
            SessionInfo.LogHeader("dump");
            foreach (Action section in DumpStateSections)
                VfhLog.Guard(LogCat.Core, "dump.section_failed", section);
            if (DumpStateSections.Count == 0)
                VfhLog.I(LogCat.Core, "dump.state", ("note", "no boards or hirelings yet in this build"));
            VfhCommand.Print("VikingsForHire: state written to the log");
        }

        private static string Cost(Dictionary<string, int> cost) => string.Join(" ", cost.Select(c => $"{c.Key}x{c.Value}"));
    }
}
