using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VikingsForHire.Diagnostics
{
    /// <summary>
    /// Cheap timing of hireling AI on this machine: total per frame and per behaviour, averaged over a rolling 10 s
    /// window. Read with vfh_perf. Only hirelings this machine owns are ticked, so run it on the host or client in question.
    /// </summary>
    internal static class PerfCounters
    {
        private const float Window = 10f;

        private sealed class Bucket
        {
            public double Ms;
            public int Calls;
        }

        private static Dictionary<string, Bucket> _current = new();
        private static Dictionary<string, Bucket> _last = new();
        private static float _windowStart;
        private static int _framesWithAi, _lastFrames;
        private static int _lastFrameSeen = -1;
        private static double _frameMs, _maxFrameMs, _lastMaxFrameMs;
        private static float _lastWindowSeconds;

        public static long Start() => Stopwatch.GetTimestamp();

        public static void Add(string key, long started)
        {
            double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            Roll();
            if (!_current.TryGetValue(key, out Bucket b))
                _current[key] = b = new Bucket();
            b.Ms += ms;
            b.Calls++;
            if (key != "ai")
                return;
            if (Time.frameCount != _lastFrameSeen)
            {
                _lastFrameSeen = Time.frameCount;
                _framesWithAi++;
                _frameMs = 0;
            }
            _frameMs += ms;
            if (_frameMs > _maxFrameMs)
                _maxFrameMs = _frameMs;
        }

        private static void Roll()
        {
            if (Time.realtimeSinceStartup - _windowStart < Window)
                return;
            _lastWindowSeconds = Time.realtimeSinceStartup - _windowStart;
            _windowStart = Time.realtimeSinceStartup;
            _last = _current;
            _current = new Dictionary<string, Bucket>();
            _lastFrames = _framesWithAi;
            _framesWithAi = 0;
            _lastMaxFrameMs = _maxFrameMs;
            _maxFrameMs = 0;
        }

        public static string Report()
        {
            Roll();
            if (_lastFrames == 0 || !_last.TryGetValue("ai", out Bucket ai))
                return "HiredHands perf: no hireling AI ran on this machine in the last 10 s window (hirelings are ticked by their owner).";
            var sb = new StringBuilder();
            sb.Append($"HiredHands perf (last {_lastWindowSeconds:0}s, {_lastFrames} frames with AI): ")
              .Append($"AI {ai.Ms / _lastFrames:0.000} ms/frame avg, {_lastMaxFrameMs:0.00} ms worst frame, {ai.Calls / (double)_lastFrames:0.0} hirelings/frame");
            foreach (var kv in _last.Where(k => k.Key != "ai").OrderByDescending(k => k.Value.Ms))
                sb.Append($"\n  {kv.Key}: {kv.Value.Ms / _lastFrames:0.000} ms/frame ({kv.Value.Calls} calls)");
            return sb.ToString();
        }

        private sealed class PatchTimer
        {
            public long Ticks, Calls, LastTicks, LastCalls;
            public float LastWindow;
        }

        private static readonly Dictionary<string, PatchTimer> Patches = new();
        private static float _patchWindowStart;

        /// <summary>Time spent in one of our Harmony patch bodies. Cheap: two timestamps and a dictionary hit.</summary>
        public static void Patch(string name, long started)
        {
            long elapsed = Stopwatch.GetTimestamp() - started;
            if (!Patches.TryGetValue(name, out PatchTimer t))
                Patches[name] = t = new PatchTimer();
            t.Ticks += elapsed;
            t.Calls++;
            if (Time.realtimeSinceStartup - _patchWindowStart >= Window)
                RollPatches();
        }

        private static void RollPatches()
        {
            float window = Time.realtimeSinceStartup - _patchWindowStart;
            _patchWindowStart = Time.realtimeSinceStartup;
            foreach (PatchTimer t in Patches.Values)
            {
                t.LastTicks = t.Ticks;
                t.LastCalls = t.Calls;
                t.LastWindow = window;
                t.Ticks = t.Calls = 0;
            }
        }

        /// <summary>Our Harmony patches over the last 10 s window: calls per second and milliseconds per second.</summary>
        public static string PatchReport()
        {
            if (Time.realtimeSinceStartup - _patchWindowStart >= Window)
                RollPatches();
            var rows = Patches.Where(p => p.Value.LastWindow > 0f && p.Value.LastCalls > 0)
                .OrderByDescending(p => p.Value.LastTicks).ToList();
            if (rows.Count == 0)
                return "HiredHands patches: none ran in the last window";
            double totalMsPerSec = rows.Sum(r => r.Value.LastTicks * 1000.0 / Stopwatch.Frequency / r.Value.LastWindow);
            var sb = new StringBuilder($"HiredHands patches (last window): {totalMsPerSec:0.00} ms per second in total");
            foreach (var r in rows)
            {
                double msPerSec = r.Value.LastTicks * 1000.0 / Stopwatch.Frequency / r.Value.LastWindow;
                sb.Append($"\n  {r.Key}: {r.Value.LastCalls / r.Value.LastWindow:0} calls/s, {msPerSec:0.000} ms/s");
            }
            return sb.ToString();
        }

        public static double PatchMsPerSecond =>
            Patches.Values.Where(t => t.LastWindow > 0f).Sum(t => t.LastTicks * 1000.0 / Stopwatch.Frequency / t.LastWindow);

        private static float _minuteStart = -1f;
        private static int _minuteFrames;
        private static float _worstFrame;

        /// <summary>Called every frame: once a minute logs the frame rate and what our code cost, so a slowdown report
        /// comes with numbers without anyone typing a command.</summary>
        public static void FrameTick()
        {
            if (_minuteStart < 0f)
                _minuteStart = Time.realtimeSinceStartup;
            _minuteFrames++;
            if (Time.unscaledDeltaTime > _worstFrame)
                _worstFrame = Time.unscaledDeltaTime;
            float elapsed = Time.realtimeSinceStartup - _minuteStart;
            if (elapsed < 60f)
                return;
            if (Time.realtimeSinceStartup - _patchWindowStart >= Window)
                RollPatches();
            string top = string.Join(",", Patches.Where(p => p.Value.LastWindow > 0f && p.Value.LastCalls > 0)
                .OrderByDescending(p => p.Value.LastTicks).Take(3)
                .Select(p => $"{p.Key}:{p.Value.LastCalls / p.Value.LastWindow:0}/s:{p.Value.LastTicks * 1000.0 / Stopwatch.Frequency / p.Value.LastWindow:0.00}ms/s"));
            VfhLog.I(Core.Diagnostics.LogCat.Perf, "perf.minute", ("fps", _minuteFrames / elapsed), ("worstFrameMs", _worstFrame * 1000f),
                ("patchMsPerSec", PatchMsPerSecond), ("aiMsPerFrame", LastAiMsPerFrame), ("topPatches", top));
            _minuteStart = Time.realtimeSinceStartup;
            _minuteFrames = 0;
            _worstFrame = 0f;
        }

        public static double LastAiMsPerFrame => _lastFrames == 0 || !_last.TryGetValue("ai", out Bucket ai) ? 0 : ai.Ms / _lastFrames;
    }
}
