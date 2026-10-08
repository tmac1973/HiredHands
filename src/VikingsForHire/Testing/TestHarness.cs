using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Commands;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Testing;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Testing
{
    /// <summary>
    /// In-game test runs driven by ServerDevcommands aliases. ServerDevcommands fires chained commands immediately, so
    /// every harness command (begin, assert, fixture, end) goes into one queue that runs strictly in order: an
    /// assert_eventually holds back everything after it until it passes or times out.
    /// </summary>
    internal static class TestHarness
    {
        private const float ServerReplyTimeout = 5f;

        private sealed class Check
        {
            public string Usage = "";
            public Func<string[], string> Eval = _ => "";
            public bool ServerSide;

            /// <summary>Server-side checks: resolves client-only arguments (like "last") before they're sent.</summary>
            public Func<string[], string[]>? PrepareArgs;
        }

        private sealed class Run
        {
            public string Row = "";
            public int Checks;
            public readonly List<string> Fails = new();
        }

        private readonly struct Result
        {
            public readonly string Row;
            public readonly bool Pass;
            public readonly int Checks;
            public readonly int Failed;

            public Result(string row, bool pass, int checks, int failed)
            {
                Row = row;
                Pass = pass;
                Checks = checks;
                Failed = failed;
            }
        }

        private static readonly Dictionary<string, Check> Checks = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<Func<IEnumerator>> QueueItems = new();
        private static readonly HashSet<Func<IEnumerator>> EndSteps = new(ReferenceComparer.Instance);
        private static int _endCount;

        private sealed class ReferenceComparer : IEqualityComparer<Func<IEnumerator>>
        {
            public static readonly ReferenceComparer Instance = new();
            public bool Equals(Func<IEnumerator> a, Func<IEnumerator> b) => ReferenceEquals(a, b);
            public int GetHashCode(Func<IEnumerator> f) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(f);
        }
        private static string? _skipToEndReason;
        private static readonly List<Result> Results = new();
        private static readonly Dictionary<int, string> ServerReplies = new();
        private static Run? _run;
        private static bool _running;
        private static Coroutine? _queue;
        private static int _nextRequest = 1;
        private static CustomRPC _checkRpc = null!;

        public static void Register()
        {
            _checkRpc = NetworkManager.Instance.AddRPC("VFH_TestCheck", OnServerCheck, OnClientReply);

            Add("vfh_test_begin", "<ROW-ID> - start a test run", args =>
            {
                string row = args.Length > 0 ? args[0] : "adhoc";
                Enqueue(() => Begin(row));
            });
            Add("vfh_test_end", "- finish the run and log its result line", _ =>
            {
                // A fresh closure each time: delegates for the same method compare equal, and the set must tell them apart.
                int n = ++_endCount;
                Func<IEnumerator> end = () => EndNumbered(n);
                EndSteps.Add(end);
                Enqueue(end);
            });
            Add("vfh_assert", "<check> [args…] <op> <value> - e.g. vfh_assert data BoardLevels.1.Cost.Wood == 40", args =>
            {
                if (Parse(args, out string check, out string[] checkArgs, out string op, out string expected))
                    Enqueue(() => Assert(check, checkArgs, op, expected, 0f));
            });
            Add("vfh_assert_eventually", "<seconds> <check> [args…] <op> <value> - re-check every second until it passes or times out", args =>
            {
                if (args.Length < 1 || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float timeout))
                {
                    VfhCommand.Print("Usage: vfh_assert_eventually <seconds> <check> [args…] <op> <value>");
                    return;
                }
                if (Parse(args.Skip(1).ToArray(), out string check, out string[] checkArgs, out string op, out string expected))
                    Enqueue(() => Assert(check, checkArgs, op, expected, timeout));
            });
            Add("vfh_wait_until", "<seconds> <check> [args…] <op> <value> - wait until the check passes or the time is up (not a check: never fails the row)", args =>
            {
                if (args.Length < 1 || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float timeout))
                {
                    VfhCommand.Print("Usage: vfh_wait_until <seconds> <check> [args…] <op> <value>");
                    return;
                }
                if (Parse(args.Skip(1).ToArray(), out string check, out string[] checkArgs, out string op, out string expected))
                    Enqueue(() => WaitUntil(check, checkArgs, op, expected, timeout));
            });
            Add("vfh_test_abort", "- stop the running test and drop every queued step", _ => Abort("vfh_test_abort"));
            Add("vfh_test_chain", "<macro> [macro]… - queue test macros (vfh_t_* aliases from alias_vfh.yaml, prefix optional) strictly in order, cleaning up after each", Chain);
            Add("vfh_test_summary", "- print every test result since login (after any queued tests finish)", _ =>
            {
                if (_running)
                    Enqueue(SummaryStep);
                else
                    Summary();
            });
            Add("vfh_test_reset", "- clear stored test results", _ =>
            {
                Results.Clear();
                VfhCommand.Print("HiredHands: test results cleared");
            });
            Add("vfh_checks", "- list the registered vfh_assert checks", _ =>
            {
                foreach (var c in Checks.OrderBy(c => c.Key))
                    VfhCommand.Print($"  {c.Key} {c.Value.Usage}{(c.Value.ServerSide ? " (server)" : "")}");
            });

            RegisterBuiltInChecks();
        }

        /// <summary>
        /// A setup step couldn't do its job (e.g. no room for a test board): record it as a failed check so the run
        /// can't pass on the wrong setup, and say why on screen.
        /// </summary>
        public static void FailSetup(string what, string reason)
        {
            Record("setup " + what, Array.Empty<string>(), "==", "ok", reason, false, 0f);
            // Carrying on would run the rest of this test against the wrong world (e.g. your own board), so the queue
            // skips to the end of this test once the current step returns. Later tests in a chained run still go.
            _skipToEndReason = "setup " + what + " failed";
        }

        // Drops the rest of the current test (through its vfh_test_end) and records it as aborted.
        private static void SkipToEnd()
        {
            string reason = _skipToEndReason!;
            _skipToEndReason = null;
            int dropped = 0;
            while (QueueItems.Count > 0)
            {
                Func<IEnumerator> step = QueueItems.Dequeue();
                dropped++;
                if (EndSteps.Remove(step))
                    break;
            }
            FinishAborted(reason, dropped);
        }

        private static void FinishAborted(string reason, int dropped)
        {
            if (_run == null)
                return;
            Run run = _run;
            _run = null;
            VfhLog.W(LogCat.Test, "test.result", ("row", run.Row), ("pass", false), ("checks", run.Checks), ("failed", run.Fails.Count),
                ("aborted", reason), ("droppedSteps", dropped), ("fails", string.Join("; ", run.Fails)));
            Results.Add(new Result(run.Row, false, run.Checks, run.Fails.Count));
            Message($"<color=#f66>ABORTED</color> {run.Row}: {reason}. Clean up with vfh_fixture kill_hirelings / clear_area if needed");
        }

        /// <summary>Stops the queue, drops every pending step and records the current run as failed.</summary>
        public static void Abort(string reason)
        {
            int dropped = QueueItems.Count;
            QueueItems.Clear();
            EndSteps.Clear();
            _skipToEndReason = null;
            if (_queue != null)
                Plugin.Instance.StopCoroutine(_queue);
            _queue = null;
            _running = false;
            TestProtection.Set(false);
            if (_run != null)
                FinishAborted(reason, dropped);
            else if (dropped > 0)
            {
                VfhLog.W(LogCat.Test, "test.aborted", ("reason", reason), ("droppedSteps", dropped));
            }
        }

        public static void RegisterCheck(string name, string usage, Func<string[], string> eval, bool serverSide = false,
            Func<string[], string[]>? prepareArgs = null) =>
            Checks[name] = new Check { Usage = usage, Eval = eval, ServerSide = serverSide, PrepareArgs = prepareArgs };

        /// <summary>Adds a step to the run queue; the queue starts itself if idle.</summary>
        public static void Enqueue(Func<IEnumerator> step)
        {
            QueueItems.Enqueue(step);
            if (!_running)
            {
                _running = true;
                _queue = Plugin.Instance.StartCoroutine(RunQueue());
            }
        }

        private static void Add(string name, string help, Action<string[]> run) =>
            CommandManager.Instance.AddConsoleCommand(new VfhCommand(name, help, true, run));

        private static IEnumerator RunQueue()
        {
            _running = true;
            while (QueueItems.Count > 0)
            {
                if (_skipToEndReason != null)
                {
                    SkipToEnd();
                    continue;
                }
                IEnumerator? step = null;
                Func<IEnumerator> next = QueueItems.Dequeue();
                EndSteps.Remove(next);
                VfhLog.Guard(LogCat.Test, "test.step_failed", () => step = next());
                if (step == null)
                    continue;
                while (true)
                {
                    bool more = false;
                    VfhLog.Guard(LogCat.Test, "test.step_failed", () => more = step.MoveNext());
                    if (!more)
                        break;
                    yield return step.Current;
                }
            }
            _running = false;
            _queue = null;
            TestProtection.Set(false);
        }

        private static IEnumerator Begin(string row)
        {
            if (_run != null)
                VfhLog.W(LogCat.Test, "test.abandoned", ("row", _run.Row), ("checks", _run.Checks));
            _run = new Run { Row = row };
            TestProtection.Set(true); // enemies ignore you and nothing hurts you while it runs
            VfhLog.I(LogCat.Core, "mark", ("text", "test " + row));
            VfhLog.I(LogCat.Test, "test.begin", ("row", row), ("fastTimers", VfhConfig.FastTimers), ("role", VfhLog.Role()));
            Message($"Test {row} started");
            yield break;
        }

        private static IEnumerator EndNumbered(int n) => End();

        private static IEnumerator End()
        {
            if (_run == null)
            {
                VfhLog.W(LogCat.Test, "test.end_without_begin");
                yield break;
            }
            Run run = _run;
            _run = null;
            bool pass = run.Fails.Count == 0 && run.Checks > 0;
            var fields = new List<(string, object?)>
            {
                ("row", run.Row), ("pass", pass), ("checks", run.Checks), ("failed", run.Fails.Count),
            };
            if (run.Fails.Count > 0)
                fields.Add(("fails", string.Join("; ", run.Fails)));
            if (run.Checks == 0)
                fields.Add(("note", "no checks ran"));
            if (pass)
                VfhLog.I(LogCat.Test, "test.result", fields.ToArray());
            else
                VfhLog.W(LogCat.Test, "test.result", fields.ToArray());
            Results.Add(new Result(run.Row, pass, run.Checks, run.Fails.Count));
            Message(pass ? $"<color=#6f6>PASS</color> {run.Row} ({run.Checks} checks)" : $"<color=#f66>FAIL</color> {run.Row} ({run.Fails.Count}/{run.Checks} failed)");
        }

        private static IEnumerator WaitUntil(string check, string[] args, string op, string expected, float timeout)
        {
            float start = Time.realtimeSinceStartup;
            bool met = false;
            if (Checks.TryGetValue(check, out Check? def))
            {
                while (Time.realtimeSinceStartup - start < timeout)
                {
                    string actual = Evaluate(def, def.PrepareArgs != null ? def.PrepareArgs(args) : args);
                    try
                    {
                        met = !actual.StartsWith("error:") && CheckExpr.Compare(actual, op, expected);
                    }
                    catch (ArgumentException)
                    {
                        met = false;
                    }
                    if (met)
                        break;
                    yield return new WaitForSeconds(1f);
                }
            }
            VfhLog.I(LogCat.Test, "test.wait", ("check", $"{check} {string.Join(" ", args)} {op} {expected}".Replace("  ", " ")), ("met", met),
                ("waited", Time.realtimeSinceStartup - start));
        }

        private static IEnumerator Assert(string check, string[] args, string op, string expected, float timeout)
        {
            if (!Checks.TryGetValue(check, out Check? def))
            {
                Record(check, args, op, expected, $"unknown check (vfh_checks lists them)", false, 0f);
                yield break;
            }

            float start = Time.realtimeSinceStartup;
            while (true)
            {
                string actual = "";
                if (def.ServerSide && ZNet.instance != null && !ZNet.instance.IsServer())
                {
                    int id = _nextRequest++;
                    string[] sendArgs = def.PrepareArgs != null ? def.PrepareArgs(args) : args;
                    var pkg = new ZPackage();
                    pkg.Write(id);
                    pkg.Write(check);
                    pkg.Write(sendArgs.Length);
                    foreach (string a in sendArgs)
                        pkg.Write(a);
                    _checkRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
                    float sent = Time.realtimeSinceStartup;
                    while (!ServerReplies.ContainsKey(id) && Time.realtimeSinceStartup - sent < ServerReplyTimeout)
                        yield return null;
                    actual = ServerReplies.TryGetValue(id, out string reply) ? reply : "error: no reply from server";
                    ServerReplies.Remove(id);
                }
                else
                {
                    // Single player (or the host): the same arguments a client would send, e.g. your player id.
                    actual = Evaluate(def, def.PrepareArgs != null ? def.PrepareArgs(args) : args);
                }

                bool pass;
                try
                {
                    pass = !actual.StartsWith("error:") && CheckExpr.Compare(actual, op, expected);
                }
                catch (ArgumentException ex)
                {
                    actual = "error: " + ex.Message;
                    pass = false;
                }

                float waited = Time.realtimeSinceStartup - start;
                if (pass || waited >= timeout)
                {
                    Record(check, args, op, expected, actual, pass, waited);
                    yield break;
                }
                yield return new WaitForSeconds(1f);
            }
        }

        private static string Evaluate(Check def, string[] args)
        {
            try
            {
                return def.Eval(args);
            }
            catch (Exception ex)
            {
                return "error: " + ex.Message;
            }
        }

        private static void Record(string check, string[] args, string op, string expected, string actual, bool pass, float waited)
        {
            string label = string.Join(" ", new[] { check }.Concat(args)) + $" {op} {expected}";
            var fields = new (string, object?)[]
            {
                ("row", _run?.Row ?? "adhoc"), ("check", label), ("expected", expected), ("actual", actual), ("pass", pass), ("waited", waited),
            };
            if (pass)
                VfhLog.I(LogCat.Test, "test.assert", fields);
            else
                VfhLog.W(LogCat.Test, "test.assert", fields);

            if (_run != null)
            {
                _run.Checks++;
                if (!pass)
                    _run.Fails.Add($"{label} (actual {actual})");
            }
            Message(pass ? $"<color=#6f6>ok</color> {label}" : $"<color=#f66>FAIL</color> {label} — got {actual}");
        }

        private static bool Parse(string[] args, out string check, out string[] checkArgs, out string op, out string expected)
        {
            check = op = expected = "";
            checkArgs = Array.Empty<string>();
            if (args.Length < 3 || !CheckExpr.IsOperator(args[args.Length - 2]))
            {
                VfhCommand.Print($"Usage: vfh_assert <check> [args…] <op> <value>, op one of {string.Join(" ", CheckExpr.Operators)}");
                return false;
            }
            check = args[0];
            checkArgs = args.Skip(1).Take(args.Length - 3).ToArray();
            op = args[args.Length - 2];
            expected = args[args.Length - 1];
            return true;
        }

        /// <summary>
        /// ServerDevcommands expands an alias used inside another alias only after the outer one has run, so chaining
        /// test macros through an alias scrambles their order (all the cleanups first). This reads the macros from
        /// alias_vfh.yaml and runs their commands now, one after another, so every step lands in the queue in order.
        /// After each macro: kill hirelings, clear the area, and wait for the server's board index to catch up.
        /// </summary>
        private static void Chain(string[] args)
        {
            string path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "alias_vfh.yaml");
            if (!System.IO.File.Exists(path))
            {
                VfhCommand.Print("HiredHands: no " + path);
                return;
            }
            var macros = new Dictionary<string, string>();
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                int colon = line.IndexOf(": ", StringComparison.Ordinal);
                if (!line.StartsWith("#") && colon > 0)
                    macros[line.Substring(0, colon).Trim()] = line.Substring(colon + 2).Trim();
            }
            foreach (string arg in args)
            {
                string name = arg.StartsWith("vfh_t_") ? arg : "vfh_t_" + arg;
                if (!macros.TryGetValue(name, out string body))
                {
                    VfhCommand.Print("HiredHands: no macro " + name);
                    continue;
                }
                foreach (string cmd in body.Split(';').Select(c => c.Trim()).Where(c => c.Length > 0))
                    Console.instance.TryRunCommand(cmd, silentFail: false, skipAllowedCheck: true);
                foreach (string cmd in new[] { "vfh_fixture kill_hirelings 80", "vfh_fixture clear_area 80", "vfh_fixture wait 3" })
                    Console.instance.TryRunCommand(cmd, silentFail: false, skipAllowedCheck: true);
            }
            VfhLog.I(LogCat.Test, "test.chain", ("macros", args.Length));
        }

        private static IEnumerator SummaryStep()
        {
            Summary();
            yield break;
        }

        private static void Summary()
        {
            if (Results.Count == 0)
            {
                VfhCommand.Print("HiredHands: no test results yet");
                return;
            }
            foreach (Result r in Results)
                VfhCommand.Print($"  {(r.Pass ? "PASS" : "FAIL")} {r.Row} ({r.Checks} checks, {r.Failed} failed)");
            int passed = Results.Count(r => r.Pass);
            VfhCommand.Print($"HiredHands: {passed}/{Results.Count} passed");
            VfhLog.I(LogCat.Test, "test.summary", ("passed", passed), ("total", Results.Count),
                ("failedRows", string.Join(",", Results.Where(r => !r.Pass).Select(r => r.Row))));
        }

        private static void Message(string text)
        {
            if (MessageHud.instance != null)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
            VfhCommand.Print(text);
        }

        private static IEnumerator OnServerCheck(long sender, ZPackage package)
        {
            int id = package.ReadInt();
            string check = package.ReadString();
            string[] args = new string[package.ReadInt()];
            for (int i = 0; i < args.Length; i++)
                args[i] = package.ReadString();

            string actual = Checks.TryGetValue(check, out Check? def) ? Evaluate(def, args) : "error: unknown check on server";
            VfhLog.I(LogCat.Test, "test.server_check", ("from", sender), ("check", check), ("args", string.Join(" ", args)), ("actual", actual));

            var reply = new ZPackage();
            reply.Write(id);
            reply.Write(actual);
            _checkRpc.SendPackage(sender, reply);
            yield break;
        }

        private static IEnumerator OnClientReply(long sender, ZPackage package)
        {
            int id = package.ReadInt();
            ServerReplies[id] = package.ReadString();
            yield break;
        }

        private static void RegisterBuiltInChecks()
        {
            RegisterCheck("cfg", "<key> - effective config value (test overrides applied)", args =>
            {
                if (args.Length != 1)
                    return "error: usage cfg <key>";
                var entry = VfhConfig.Find(args[0]);
                return entry == null ? $"error: no config key {args[0]}" : VfhConfig.EffectiveValue(entry);
            });
            RegisterCheck("data", "<dotted.path> - data table value; list positions are 1-based (BoardLevels.2.HirelingCap)",
                args => args.Length == 1 ? DataPath(args[0]) : "error: usage data <path>");
            RegisterCheck("data_source", "- local | server | defaults", _ => DataStore.Source);
            RegisterCheck("data_reloads", "- how many times the data tables were (re)loaded this session", _ => DataStore.Reloads.ToString());
            RegisterCheck("log_errors", "- error lines logged this session", _ => VfhLog.ErrorCount.ToString());
            RegisterCheck("fast_timers", "- true when test timers are active", _ => VfhConfig.FastTimers ? "true" : "false");
        }

        /// <summary>Walks a dotted path through the data tables: properties and dictionary keys by name (any case), lists by 1-based position.</summary>
        private static string DataPath(string path)
        {
            object? current = DataStore.Current;
            foreach (string segment in path.Split('.'))
            {
                switch (current)
                {
                    case null:
                        return $"error: '{segment}' is past the end of {path}";
                    case IDictionary dict:
                    {
                        object? key = dict.Keys.Cast<object>().FirstOrDefault(k => string.Equals(k.ToString(), segment, StringComparison.OrdinalIgnoreCase));
                        if (key == null)
                            return $"error: no key '{segment}' in {path}";
                        current = dict[key];
                        break;
                    }
                    case IList list:
                        if (!int.TryParse(segment, out int pos) || pos < 1 || pos > list.Count)
                            return $"error: '{segment}' isn't a position 1-{list.Count} in {path}";
                        current = list[pos - 1];
                        break;
                    default:
                    {
                        PropertyInfo? prop = current.GetType().GetProperty(segment,
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (prop == null)
                            return $"error: no field '{segment}' in {path}";
                        current = prop.GetValue(current);
                        break;
                    }
                }
            }
            return current switch
            {
                null => "null",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                ICollection c => c.Count.ToString(),
                _ => current.ToString() ?? "",
            };
        }
    }
}
