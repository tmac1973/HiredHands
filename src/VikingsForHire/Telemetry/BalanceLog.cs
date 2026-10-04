using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Telemetry
{
    /// <summary>
    /// The opt-in balance log (BalanceLog, off by default, set on the server and synced to every player). Hirelings are
    /// simulated on players' machines, so that's where fights and work are seen: each game keeps one JSON line per
    /// event (a fight's summary, a death, a delivery, an upkeep day) in memory and sends them to the server once a
    /// minute. The server appends them to BepInEx/HiredHands/balance/YYYY-MM-DD.jsonl from a worker thread and keeps the
    /// folder under BalanceLogMaxMB by deleting the oldest days. Players appear only as scrambled ids.
    /// </summary>
    internal static class BalanceLog
    {
        private const float FlushSeconds = 60f;
        private const int MaxQueued = 2000;
        private const int MaxPerBatch = 500;

        private static readonly List<string> Pending = new();
        private static int _dropped;
        private static float _nextFlush;
        private static CustomRPC _rpc = null!;
        private static readonly object WriteLock = new();

        public static bool On => VfhConfig.BalanceLog.Value;

        public static void Register() =>
            _rpc = NetworkManager.Instance.AddRPC("VFH_BalanceLog", OnServer, OnClient);

        /// <summary>Queue one record (main thread). Fields become JSON; t, day and v are added.</summary>
        public static void Record(string type, params (string Key, object? Value)[] fields)
        {
            if (!On)
                return;
            if (Pending.Count >= MaxQueued)
            {
                _dropped++;
                return;
            }
            var sb = new StringBuilder(256);
            sb.Append("{\"type\":").Append(Json(type));
            sb.Append(",\"t\":").Append(Num(ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0d));
            sb.Append(",\"day\":").Append(EnvMan.instance != null ? EnvMan.instance.GetDay() : 0);
            sb.Append(",\"v\":").Append(Json(Plugin.Version));
            foreach ((string key, object? value) in fields)
                sb.Append(',').Append(Json(key)).Append(':').Append(Value(value));
            sb.Append('}');
            Pending.Add(sb.ToString());
        }

        /// <summary>Every frame (Plugin.Update): fights' housekeeping, and the once-a-minute send.</summary>
        public static void Tick()
        {
            if (Time.time < _nextFlush)
                return;
            _nextFlush = Time.time + FlushSeconds;
            BalanceFights.Prune();
            if (!On)
            {
                Pending.Clear();
                _dropped = 0;
                return;
            }
            if (_dropped > 0)
            {
                int dropped = _dropped;
                _dropped = 0;
                Record("dropped", ("n", dropped));
            }
            while (Pending.Count > 0)
            {
                List<string> batch = Pending.Take(MaxPerBatch).ToList();
                Pending.RemoveRange(0, batch.Count);
                Send(batch);
            }
        }

        private static void Send(List<string> lines)
        {
            if (ZNet.instance == null)
                return;
            if (ZNet.instance.IsServer())
            {
                Write(lines);
                return;
            }
            var pkg = new ZPackage();
            pkg.Write(lines.Count);
            foreach (string line in lines)
                pkg.Write(line);
            _rpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
        }

        private static IEnumerator OnServer(long sender, ZPackage pkg)
        {
            VfhLog.Guard(LogCat.Core, "balance.receive_failed", () =>
            {
                if (!On)
                    return;
                int n = Math.Min(pkg.ReadInt(), MaxPerBatch + 1);
                var lines = new List<string>(n);
                for (int i = 0; i < n; i++)
                {
                    string line = pkg.ReadString();
                    // Only what our own game sends: one JSON object per line.
                    if (line.Length > 0 && line.Length < 4096 && line[0] == '{' && line.IndexOf('\n') < 0)
                        lines.Add(line);
                }
                Write(lines);
            });
            yield break;
        }

        private static IEnumerator OnClient(long sender, ZPackage pkg)
        {
            yield break;
        }

        // Off the main thread: append to today's file, then keep the folder under its size cap.
        private static void Write(List<string> lines)
        {
            if (lines.Count == 0)
                return;
            string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "HiredHands", "balance");
            long cap = Math.Max(1, VfhConfig.BalanceLogMaxMB.Value) * 1024L * 1024L;
            string file = Path.Combine(dir, DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    lock (WriteLock)
                    {
                        Directory.CreateDirectory(dir);
                        File.AppendAllLines(file, lines);
                        List<FileInfo> files = new DirectoryInfo(dir).GetFiles("*.jsonl").OrderBy(f => f.Name).ToList();
                        long total = files.Sum(f => f.Length);
                        for (int i = 0; i < files.Count - 1 && total > cap; i++)
                        {
                            total -= files[i].Length;
                            files[i].Delete();
                        }
                    }
                }
                catch (Exception e)
                {
                    VfhLog.W(LogCat.Core, "balance.write_failed", ("error", e.Message));
                }
            });
        }

        /// <summary>A player as a scrambled id: enough to tell players apart in the log, not who they are.</summary>
        public static string Who(long playerId) => playerId == 0L ? "" : ((uint)((playerId ^ 0x5bd1e995L) * 2654435761L >> 7)).ToString("x8");

        private static string Value(object? v) => v switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            float f => Num(f),
            double d => Num(d),
            _ => Json(v.ToString()),
        };

        private static string Num(double d) => double.IsNaN(d) || double.IsInfinity(d) ? "null" : Math.Round(d, 2).ToString("0.##", CultureInfo.InvariantCulture);

        private static string Json(string? s)
        {
            if (s == null)
                return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
