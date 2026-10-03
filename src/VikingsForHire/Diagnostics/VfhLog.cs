using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using VikingsForHire.Core.Diagnostics;
using UnityEngine;
using LogLevel = VikingsForHire.Core.Diagnostics.LogLevel;

namespace VikingsForHire.Diagnostics
{
    /// <summary>
    /// The one logging entry point. Every line is <c>[VFH] t=… f=… role=… lvl=… cat=… evt=… key=value…</c>, written to
    /// the BepInEx log and to BepInEx/VikingsForHire.log. Error/Warning/Info always log; Debug/Trace per category.
    /// </summary>
    internal static class VfhLog
    {
        private const int GuardFullReports = 5;
        private const float GuardSummarySeconds = 60f;

        private static ManualLogSource _source = null!;
        private static LogFileSink? _file;
        private static LogFilter _filter = new(Array.Empty<LogCat>(), Array.Empty<LogCat>());
        private static int _mainThreadId;
        private static readonly HashSet<string> SeenIds = new();
        private static readonly Dictionary<string, GuardState> Guards = new();
        private static readonly Dictionary<string, float> ThrottleUntil = new();

        /// <summary>Errors logged this session; the test harness asserts this stays 0.</summary>
        public static int ErrorCount { get; private set; }

        public static int WarningCount { get; private set; }

        public static string? FilePath => _file?.Path;

        private sealed class GuardState
        {
            public int Count;
            public int Suppressed;
            public float LastSummary;
        }

        public static void Init(ManualLogSource source)
        {
            _source = source;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static void SetFilter(LogFilter filter) => _filter = filter;

        public static LogFilter Filter => _filter;

        public static void SetFileLogging(bool enabled, int maxMegabytes)
        {
            if (!enabled)
            {
                _file?.Dispose();
                _file = null;
                return;
            }
            if (_file != null)
            {
                _file.SetMaxMegabytes(maxMegabytes);
                return;
            }
            try
            {
                _file = new LogFileSink(Path.Combine(Paths.BepInExRootPath, "VikingsForHire.log"), maxMegabytes);
            }
            catch (Exception ex)
            {
                _source.LogWarning($"[VFH] could not open VikingsForHire.log, logging to LogOutput.log only: {ex.Message}");
            }
        }

        public static void Shutdown()
        {
            _file?.Dispose();
            _file = null;
        }

        public static bool Enabled(LogLevel level, LogCat cat) => _filter.Enabled(level, cat);

        public static void E(LogCat cat, string evt, params (string, object?)[] fields) => Write(LogLevel.Error, cat, evt, fields);
        public static void W(LogCat cat, string evt, params (string, object?)[] fields) => Write(LogLevel.Warning, cat, evt, fields);
        public static void I(LogCat cat, string evt, params (string, object?)[] fields) => Write(LogLevel.Info, cat, evt, fields);
        public static void D(LogCat cat, string evt, params (string, object?)[] fields) => Write(LogLevel.Debug, cat, evt, fields);
        public static void T(LogCat cat, string evt, params (string, object?)[] fields) => Write(LogLevel.Trace, cat, evt, fields);

        public static void Exception(LogCat cat, string evt, Exception ex, params (string, object?)[] fields)
        {
            var all = fields.Concat(new (string, object?)[] { ("ex", ex.GetType().Name), ("msg", ex.Message) }).ToArray();
            Write(LogLevel.Error, cat, evt, all, Indent(ex.ToString()));
        }

        /// <summary>
        /// Runs <paramref name="action"/> and logs any exception. The first five failures of each (cat, evt) get full
        /// stack traces; after that a single summary line per minute with the count, so a broken tick can't flood the log.
        /// </summary>
        public static void Guard(LogCat cat, string evt, Action action, params (string, object?)[] fields)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                string key = cat + "/" + evt;
                if (!Guards.TryGetValue(key, out GuardState state))
                    Guards[key] = state = new GuardState { LastSummary = Now() };
                state.Count++;
                if (state.Count <= GuardFullReports)
                {
                    Exception(cat, evt, ex, fields.Concat(new (string, object?)[] { ("occurrence", state.Count) }).ToArray());
                    return;
                }
                state.Suppressed++;
                if (Now() - state.LastSummary >= GuardSummarySeconds)
                {
                    Write(LogLevel.Error, cat, evt,
                        fields.Concat(new (string, object?)[] { ("repeat", state.Suppressed), ("total", state.Count), ("ex", ex.GetType().Name), ("msg", ex.Message) }).ToArray());
                    state.Suppressed = 0;
                    state.LastSummary = Now();
                }
            }
        }

        /// <summary>Logs at most once per <paramref name="seconds"/> for the given key (e.g. per-tick "no targets").</summary>
        public static void Throttled(string key, float seconds, LogLevel level, LogCat cat, string evt, params (string, object?)[] fields)
        {
            if (!_filter.Enabled(level, cat))
                return;
            float now = Now();
            if (ThrottleUntil.TryGetValue(key, out float until) && now < until)
                return;
            ThrottleUntil[key] = now + seconds;
            Write(level, cat, evt, fields);
        }

        /// <summary>Writes a raw line (session header blocks) to both sinks without the standard prefix.</summary>
        public static void Raw(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                _source.LogInfo(line);
                _file?.Write(line);
            }
        }

        private static void Write(LogLevel level, LogCat cat, string evt, (string, object?)[] fields, string? trailer = null)
        {
            if (!_filter.Enabled(level, cat))
                return;
            if (level == LogLevel.Error) ErrorCount++;
            if (level == LogLevel.Warning) WarningCount++;

            bool main = Thread.CurrentThread.ManagedThreadId == _mainThreadId;
            double time = main ? GameTime() : -1;
            long frame = main ? Time.frameCount : -1;
            string role = main ? Role() : "THREAD";

            foreach ((string key, object? value) in fields)
            {
                if (value is string id && LogFormat.IdKeys.Contains(key) && SeenIds.Add(id))
                    Emit(level, LogFormat.Line(time, frame, role, level, cat, "id.map",
                        new (string, object?)[] { ("key", key), ("short", LogFormat.ShortId(id)), ("full", id) }));
            }

            string line = LogFormat.Line(time, frame, role, level, cat, evt, fields);
            Emit(level, trailer == null ? line : line + "\n" + trailer);
        }

        private static void Emit(LogLevel level, string text)
        {
            switch (level)
            {
                case LogLevel.Error: _source.LogError(text); break;
                case LogLevel.Warning: _source.LogWarning(text); break;
                case LogLevel.Info: _source.LogInfo(text); break;
                default: _source.LogDebug(text); break;
            }
            _file?.Write(text);
        }

        private static string Indent(string text) => "    " + text.Replace("\r", "").Replace("\n", "\n    ");

        private static float Now() => Time.realtimeSinceStartup;

        private static double GameTime() => ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.realtimeSinceStartup;

        /// <summary>SP (single-player), HOST (listen server), CLIENT, SERVER (dedicated) or MENU (no world loaded).</summary>
        public static string Role()
        {
            ZNet znet = ZNet.instance;
            if (znet == null)
                return "MENU";
            if (znet.IsDedicated())
                return "SERVER";
            if (znet.IsServer())
                return ZNet.m_openServer ? "HOST" : "SP";
            return "CLIENT";
        }
    }
}
