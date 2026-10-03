using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Commands;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Testing
{
    /// <summary>
    /// <c>vfh_fixture &lt;name&gt; [args]</c>: test setup steps. They run through the harness queue, so in a macro they
    /// happen in order with the asserts around them. Each later phase adds a Fixtures&lt;Area&gt;.cs that registers here.
    /// </summary>
    internal static class Fixtures
    {
        private sealed class Fixture
        {
            public string Usage = "";
            public Func<string[], IEnumerator> Run = _ => Empty();
        }

        private static readonly Dictionary<string, Fixture> All = new(StringComparer.OrdinalIgnoreCase);
        private static CustomRPC _fastTimersRpc = null!;

        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_fixture",
                "<name> [args] - test setup step (vfh_fixture list shows them)", true, Run,
                new List<string> { "list", "fast_timers" }));

            _fastTimersRpc = NetworkManager.Instance.AddRPC("VFH_TestFastTimers", OnServerFastTimers, OnClientFastTimers);
            SynchronizationManager.Instance.AddInitialSynchronization(_fastTimersRpc, () => FastTimersPackage(VfhConfig.FastTimers));

            Add("fast_timers", "<on|off> - short arrival/respawn waits and orphan/return timers ÷10, on the server and every client", FastTimers);
        }

        public static void Add(string name, string usage, Func<string[], IEnumerator> run) =>
            All[name] = new Fixture { Usage = usage, Run = run };

        private static void Run(string[] args)
        {
            if (args.Length == 0 || args[0] == "list")
            {
                foreach (var f in All.OrderBy(f => f.Key))
                    VfhCommand.Print($"  {f.Key} {f.Value.Usage}");
                return;
            }
            if (!All.TryGetValue(args[0], out Fixture? fixture))
            {
                VfhCommand.Print($"Unknown fixture '{args[0]}'. vfh_fixture list shows them.");
                return;
            }
            string[] rest = args.Skip(1).ToArray();
            TestHarness.Enqueue(() =>
            {
                VfhLog.I(LogCat.Test, "fixture", ("name", args[0]), ("args", string.Join(" ", rest)));
                return fixture.Run(rest);
            });
        }

        private static IEnumerator FastTimers(string[] args)
        {
            if (args.Length != 1 || (args[0] != "on" && args[0] != "off"))
            {
                VfhCommand.Print("Usage: vfh_fixture fast_timers <on|off>");
                yield break;
            }
            bool on = args[0] == "on";
            if (ZNet.instance == null || ZNet.instance.IsServer())
            {
                ApplyFastTimers(on, "local");
                BroadcastFastTimers();
            }
            else
            {
                // The server checks admin rights, applies, and sends the result to every client including this one.
                _fastTimersRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), FastTimersPackage(on));
                VfhLog.D(LogCat.Test, "fast_timers.request", ("on", on));
                yield return new UnityEngine.WaitForSeconds(0.5f);
            }
        }

        private static void ApplyFastTimers(bool on, string from)
        {
            if (VfhConfig.FastTimers == on)
                return;
            VfhConfig.SetFastTimers(on);
            VfhLog.D(LogCat.Test, "fast_timers.applied", ("on", on), ("from", from));
        }

        private static void BroadcastFastTimers()
        {
            if (ZNet.instance != null && ZNet.instance.GetPeers().Count > 0)
                _fastTimersRpc.SendPackage(ZNet.instance.GetPeers(), FastTimersPackage(VfhConfig.FastTimers));
        }

        private static ZPackage FastTimersPackage(bool on)
        {
            var pkg = new ZPackage();
            pkg.Write(on);
            return pkg;
        }

        private static IEnumerator OnServerFastTimers(long sender, ZPackage package)
        {
            bool on = package.ReadBool();
            ZNetPeer? peer = ZNet.instance.GetPeer(sender);
            string host = peer?.m_socket.GetHostName() ?? "?";
            if (peer == null || !ZNet.instance.ListContainsId(ZNet.instance.m_adminList, host))
            {
                VfhLog.W(LogCat.Test, "fast_timers.denied", ("from", host), ("reason", "not an admin"));
                yield break;
            }
            ApplyFastTimers(on, host);
            BroadcastFastTimers();
        }

        private static IEnumerator OnClientFastTimers(long sender, ZPackage package)
        {
            ApplyFastTimers(package.ReadBool(), "server");
            yield break;
        }

        private static IEnumerator Empty()
        {
            yield break;
        }
    }
}
