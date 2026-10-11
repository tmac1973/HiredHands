using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;

namespace VikingsForHire.Diagnostics
{
    /// <summary>
    /// Players' Hired Hands log lines, sent to the server for troubleshooting: with ClientLogsToServer on (a server
    /// setting), every player's game sends its lines at ClientLogLevel or worse every few seconds; vfh_send_log sends a
    /// player's recent lines once, whatever the setting. The server writes them to BepInEx/HiredHands/clients/&lt;player&gt;.log.
    /// Only this mod's own lines go, nothing else from the game.
    /// </summary>
    internal static class ClientLogRelay
    {
        private const float FlushSeconds = 5f;
        private const int RecentLines = 3000;
        private const int MaxPending = 5000;
        private const int MaxPerBatch = 300;
        private const int MaxLineLength = 8192;
        private const int ServerLinesPerMinute = 6000;
        private const int FileMegabytes = 5;

        private static readonly object Lock = new();
        private static readonly Queue<string> Recent = new();
        private static readonly List<string> Pending = new();
        private static int _dropped;
        private static float _nextFlush;
        private static CustomRPC _rpc = null!;

        // Server side: one file per player, and how many lines each sender sent this minute.
        private static readonly Dictionary<string, LogFileSink> Files = new();
        private static readonly Dictionary<long, (float Since, int Lines)> Budget = new();

        public static void Register() => _rpc = NetworkManager.Instance.AddRPC("VFH_ClientLog", OnServer, OnClient);

        /// <summary>Every line VfhLog writes (any thread): kept for vfh_send_log, queued for the server when it's on.</summary>
        public static void Offer(LogLevel level, string text)
        {
            lock (Lock)
            {
                Recent.Enqueue(text);
                while (Recent.Count > RecentLines)
                    Recent.Dequeue();
                if (!_relaying || level > _level)
                    return;
                if (Pending.Count >= MaxPending)
                {
                    _dropped++;
                    return;
                }
                Pending.Add(text);
            }
        }

        // Read on the main thread in Tick, used by Offer on any thread.
        private static volatile bool _relaying;
        private static volatile LogLevel _level = LogLevel.Info;

        /// <summary>Every frame (Plugin.Update): a player's game sends what's queued every few seconds.</summary>
        public static void Tick()
        {
            if (Time.time < _nextFlush)
                return;
            _nextFlush = Time.time + FlushSeconds;
            bool client = ZNet.instance != null && !ZNet.instance.IsServer() && ZRoutedRpc.instance != null;
            _relaying = client && VfhConfig.ClientLogsToServer.Value;
            _level = VfhConfig.ClientLogLevel.Value;
            List<string> batch;
            lock (Lock)
            {
                if (!_relaying)
                {
                    Pending.Clear();
                    _dropped = 0;
                    return;
                }
                if (_dropped > 0)
                {
                    Pending.Add($"[VFH] (relay) {_dropped} lines not sent: too many at once");
                    _dropped = 0;
                }
                batch = Pending.Take(MaxPerBatch * 2).ToList();
                Pending.RemoveRange(0, batch.Count);
            }
            for (int i = 0; i < batch.Count; i += MaxPerBatch)
                Send(batch.Skip(i).Take(MaxPerBatch).ToList(), manual: false);
        }

        /// <summary>vfh_send_log: send this game's last lines to the server now (whatever ClientLogsToServer says).</summary>
        public static int SendRecent(int lines)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null)
                return -1;
            List<string> recent;
            lock (Lock)
                recent = Recent.Skip(Math.Max(0, Recent.Count - Math.Max(1, lines))).ToList();
            recent.Insert(0, $"[VFH] ---- vfh_send_log: the last {recent.Count} lines from {Player.m_localPlayer?.GetPlayerName() ?? "?"} ----");
            for (int i = 0; i < recent.Count; i += MaxPerBatch)
                Send(recent.Skip(i).Take(MaxPerBatch).ToList(), manual: true);
            return recent.Count;
        }

        private static void Send(List<string> lines, bool manual)
        {
            var pkg = new ZPackage();
            pkg.Write(manual);
            pkg.Write(lines.Count);
            foreach (string line in lines)
                pkg.Write(line.Length > MaxLineLength ? line.Substring(0, MaxLineLength) : line);
            _rpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        private static IEnumerator OnServer(long sender, ZPackage pkg)
        {
            VfhLog.Guard(LogCat.Core, "clientlog.receive_failed", () =>
            {
                bool manual = pkg.ReadBool();
                if (!manual && !VfhConfig.ClientLogsToServer.Value)
                    return;
                int n = Math.Min(pkg.ReadInt(), MaxPerBatch + 1);
                if (!Spend(sender, n))
                    return;
                string who = FileName(ZNet.instance.GetPeer(sender)?.m_playerName, sender);
                if (!Files.TryGetValue(who, out LogFileSink? file))
                {
                    string dir = Path.Combine(Paths.BepInExRootPath, "HiredHands", "clients");
                    Directory.CreateDirectory(dir);
                    Files[who] = file = new LogFileSink(Path.Combine(dir, who + ".log"), FileMegabytes);
                }
                for (int i = 0; i < n; i++)
                {
                    string line = pkg.ReadString();
                    if (line.Length > 0 && line.Length <= MaxLineLength)
                        file.Write(line);
                }
            });
            yield break;
        }

        private static IEnumerator OnClient(long sender, ZPackage pkg)
        {
            yield break;
        }

        // A sender's lines this minute: past the limit the rest of the minute's lines are dropped (once noted in our log).
        private static bool Spend(long sender, int lines)
        {
            float now = Time.time;
            (float since, int used) = Budget.TryGetValue(sender, out var b) && now - b.Since < 60f ? b : (now, 0);
            if (used + lines > ServerLinesPerMinute)
            {
                if (used <= ServerLinesPerMinute)
                    VfhLog.W(LogCat.Core, "clientlog.too_many", ("sender", sender), ("perMinute", ServerLinesPerMinute));
                Budget[sender] = (since, ServerLinesPerMinute + 1);
                return false;
            }
            Budget[sender] = (since, used + lines);
            return true;
        }

        // A player's name as a safe file name (letters, digits, - and _), else their peer id.
        private static string FileName(string? name, long sender)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "peer" + sender;
        }

        public static void Shutdown()
        {
            foreach (LogFileSink f in Files.Values)
                f.Dispose();
            Files.Clear();
        }
    }
}
