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

        public static double LastAiMsPerFrame => _lastFrames == 0 || !_last.TryGetValue("ai", out Bucket ai) ? 0 : ai.Ms / _lastFrames;
    }
}
