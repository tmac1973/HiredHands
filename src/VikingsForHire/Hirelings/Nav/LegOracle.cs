using System.Collections.Generic;
using UnityEngine;
using VikingsForHire.Core.Nav;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>
    /// Answers the route planner's "can you walk from a to b on the game's map?" (a full route that really ends at b,
    /// not one the map moved to another floor). Answers are kept per board until its links change; a few are worked out
    /// each frame, the rest queued, so planning never costs a slow frame.
    /// </summary>
    internal static class LegOracle
    {
        private const float YesSeconds = 30f;
        private const float NoSeconds = 15f;
        private const int PerFrame = 4; // about 1 ms each: 12 made a 10-13 ms frame when a route was planned
        private const float EndSlackXZ = 1.6f;
        private const float EndSlackY = 0.8f;

        private sealed class Cache
        {
            public int Version = -1;
            public readonly Dictionary<(Vector3Int, Vector3Int), (bool Ok, float Cost, float Until)> Answers = new();
        }

        private static readonly Dictionary<string, Cache> ByBoard = new();
        private static readonly Queue<(string Board, Vector3 A, Vector3 B)> Pending = new();
        private static readonly HashSet<(string, Vector3Int, Vector3Int)> Queued = new();
        private static readonly List<Vector3> Path = new();
        private static int _frame = -1;
        private static int _budget;

        /// <summary>The walking agent hirelings use (set from the first hireling's AI).</summary>
        public static Pathfinding.AgentType Agent = Pathfinding.AgentType.Humanoid;

        private static Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x * 2f), Mathf.RoundToInt(p.y * 2f), Mathf.RoundToInt(p.z * 2f));

        private static Cache For(BoardNav nav)
        {
            if (!ByBoard.TryGetValue(nav.BoardId, out Cache c))
                ByBoard[nav.BoardId] = c = new Cache();
            if (c.Version != nav.Graph.Version)
            {
                c.Answers.Clear();
                c.Version = nav.Graph.Version;
            }
            return c;
        }

        /// <summary>A cached answer; else worked out now if this frame's budget allows; else not known yet (and queued).</summary>
        public static LegAnswer Answer(BoardNav nav, NavPoint a, NavPoint b)
        {
            Cache c = For(nav);
            Vector3 ua = a.ToUnity(), ub = b.ToUnity();
            var key = (Key(ua), Key(ub));
            if (c.Answers.TryGetValue(key, out var known) && Time.time < known.Until)
                return known.Ok ? LegAnswer.Yes(known.Cost) : LegAnswer.No;
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _budget = PerFrame;
            }
            if (_budget > 0)
            {
                _budget--;
                return Store(c, key, ua, ub);
            }
            Queue(nav, a, b);
            return LegAnswer.Unknown;
        }

        /// <summary>Asked now regardless of the budget, and not cached (for `vfh_navlinks why`).</summary>
        public static LegAnswer AnswerNow(BoardNav nav, NavPoint a, NavPoint b) =>
            Walkable(a.ToUnity(), b.ToUnity(), out float cost) ? LegAnswer.Yes(cost) : LegAnswer.No;

        public static void Queue(BoardNav nav, NavPoint a, NavPoint b)
        {
            Vector3 ua = a.ToUnity(), ub = b.ToUnity();
            if (Queued.Add((nav.BoardId, Key(ua), Key(ub))))
                Pending.Enqueue((nav.BoardId, ua, ub));
        }

        /// <summary>A leg it couldn't walk after all: say no for a minute.</summary>
        public static void MarkFailed(BoardNav nav, Vector3 a, Vector3 b) =>
            For(nav).Answers[(Key(a), Key(b))] = (false, 0f, Time.time + 60f);

        /// <summary>Every frame: work out queued legs with what's left of the budget.</summary>
        public static void Step()
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _budget = PerFrame;
            }
            while (_budget > 0 && Pending.Count > 0)
            {
                (string board, Vector3 a, Vector3 b) = Pending.Dequeue();
                Queued.Remove((board, Key(a), Key(b)));
                BoardNav? nav = NavLinkRegistry.Get(board);
                if (nav == null)
                    continue;
                Cache c = For(nav);
                var key = (Key(a), Key(b));
                if (c.Answers.TryGetValue(key, out var known) && Time.time < known.Until)
                    continue;
                _budget--;
                Store(c, key, a, b);
            }
        }

        private static LegAnswer Store(Cache c, (Vector3Int, Vector3Int) key, Vector3 a, Vector3 b)
        {
            bool ok = Walkable(a, b, out float cost);
            c.Answers[key] = (ok, cost, Time.time + (ok ? YesSeconds : NoSeconds));
            return ok ? LegAnswer.Yes(cost) : LegAnswer.No;
        }

        private static bool Walkable(Vector3 a, Vector3 b, out float cost)
        {
            cost = 0f;
            if (Pathfinding.instance == null ||
                !Pathfinding.instance.GetPath(a, b, Path, Agent, requireFullPath: true, cleanup: false) || Path.Count == 0)
                return ShortStep(a, b, out cost);
            // The map moves both ends to the nearest walkable spot: a route to the floor below isn't a route upstairs.
            // Along the ground a little slack (a chest's middle is inside it), in height none to speak of.
            if (!Near(Path[0], a) || !Near(Path[Path.Count - 1], b))
                return ShortStep(a, b, out cost);
            for (int i = 1; i < Path.Count; i++)
                cost += Vector3.Distance(Path[i - 1], Path[i]);
            return true;
        }

        // A step or two on the same level with nothing solid in between (a small landing between two flights, where the
        // game's map, keeping 0.4 m from every edge, may have no walkable area at all): walkable without the map.
        private const float ShortStepXZ = 2.5f;
        private static readonly int SolidMask = LayerMask.GetMask("piece", "Default", "static_solid", "terrain");

        private static bool ShortStep(Vector3 a, Vector3 b, out float cost)
        {
            cost = Vector3.Distance(a, b);
            if (Utils.DistanceXZ(a, b) > ShortStepXZ || Mathf.Abs(a.y - b.y) > 0.5f)
                return false;
            // Stop the look short of b: a goal is often a chest's own position, inside the chest.
            Vector3 dir = b - a;
            dir.y = 0f;
            float clear = dir.magnitude - ShortStepGoalSlack;
            if (clear <= 0f)
                return true;
            Vector3 end = a + dir.normalized * clear;
            end.y = Mathf.Lerp(a.y, b.y, clear / dir.magnitude);
            return !Physics.Linecast(a + Vector3.up * 0.6f, end + Vector3.up * 0.6f, SolidMask, QueryTriggerInteraction.Ignore) &&
                   !Physics.Linecast(a + Vector3.up * 1.4f, end + Vector3.up * 1.4f, SolidMask, QueryTriggerInteraction.Ignore);
        }

        private const float ShortStepGoalSlack = 0.8f;

        private static bool Near(Vector3 end, Vector3 wanted) =>
            Utils.DistanceXZ(end, wanted) <= EndSlackXZ && Mathf.Abs(end.y - wanted.y) <= EndSlackY;

        public static void Clear(string boardId) => ByBoard.Remove(boardId);
    }
}
