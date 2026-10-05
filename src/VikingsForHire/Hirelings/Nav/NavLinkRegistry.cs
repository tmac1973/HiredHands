using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Nav;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>One loaded board's area and its links.</summary>
    internal sealed class BoardNav
    {
        public string BoardId = "";
        public HiringBoard Board = null!;
        public readonly NavGraph Graph = new();
        public Vector3 Center;
        public float Radius;
        public bool Dirty = true;
        public float DirtySince;
        public float LastMark;
        public BoardNavScanner? Job;
        public bool RescanAfterJob;
        public int Rejected;
        public int Scans;

        public bool Contains(Vector3 p) => Utils.DistanceXZ(p, Center) <= Radius;
    }

    /// <summary>
    /// The link graphs of the boards loaded in this game, kept up to date: a board is scanned when it loads and again a
    /// couple of seconds after any piece in its area is built or removed (debounced, so a base loading in is one scan).
    /// </summary>
    internal static class NavLinkRegistry
    {
        private const float QuietSeconds = 2f;
        private const float MaxWaitSeconds = 10f;
        private const float AreaMargin = 8f;

        private static readonly Dictionary<string, BoardNav> Boards = new();

        public static IEnumerable<BoardNav> All => Boards.Values;

        public static bool Enabled => VfhConfig.BaseNavLinks != null && VfhConfig.BaseNavLinks.Value;

        public static void Tick()
        {
            if (!Enabled || ZNetScene.instance == null)
            {
                if (Boards.Count > 0)
                {
                    NavOverlay.RemoveAll();
                    Boards.Clear();
                }
                return;
            }
            Sync();
            bool stepped = false;
            foreach (BoardNav nav in Boards.Values)
            {
                if (nav.Job != null)
                {
                    // One scan steps per frame, so two boards loading together don't double the cost.
                    if (stepped)
                        continue;
                    stepped = true;
                    nav.Job.Step();
                    if (!nav.Job.Done)
                        continue;
                    nav.Job = null;
                    nav.Scans++;
                    if (nav.RescanAfterJob)
                    {
                        nav.RescanAfterJob = false;
                        MarkBoard(nav);
                    }
                    continue;
                }
                if (nav.Dirty && (Time.time - nav.LastMark >= QuietSeconds || Time.time - nav.DirtySince >= MaxWaitSeconds) && !stepped)
                {
                    nav.Dirty = false;
                    nav.Job = new BoardNavScanner(nav);
                }
            }
            LegOracle.Step();
            NavOverlay.Tick();
        }

        private static void Sync()
        {
            var seen = new HashSet<string>();
            var rules = new LevelRules(DataStore.Current);
            foreach (HiringBoard b in HiringBoard.Loaded)
            {
                if (b == null || b.Zdo == null || b.Id.Length == 0)
                    continue;
                seen.Add(b.Id);
                float radius = rules.MaxWorkRadius(b.Level) + AreaMargin;
                if (!Boards.TryGetValue(b.Id, out BoardNav nav))
                {
                    nav = new BoardNav { BoardId = b.Id, Board = b, Center = b.transform.position, Radius = radius };
                    Boards[b.Id] = nav;
                    MarkBoard(nav);
                    continue;
                }
                nav.Board = b;
                if (Mathf.Abs(nav.Radius - radius) > 0.5f || Vector3.Distance(nav.Center, b.transform.position) > 0.5f)
                {
                    nav.Radius = radius;
                    nav.Center = b.transform.position;
                    MarkBoard(nav);
                }
            }
            foreach (string gone in Boards.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                NavOverlay.Remove(gone);
                Boards.Remove(gone);
            }
        }

        /// <summary>A piece was built or removed here: rescan the boards whose area holds it.</summary>
        public static void MarkDirty(Vector3 position)
        {
            if (!Enabled)
                return;
            foreach (BoardNav nav in Boards.Values)
                if (nav.Contains(position))
                    MarkBoard(nav);
        }

        public static void MarkBoard(BoardNav nav, bool now = false)
        {
            if (nav.Job != null)
            {
                nav.RescanAfterJob = true;
                return;
            }
            if (!nav.Dirty)
                nav.DirtySince = Time.time;
            nav.Dirty = true;
            nav.LastMark = now ? -999f : Time.time;
        }

        /// <summary>The board area holding the point (the nearest board's, if areas overlap), or null.</summary>
        public static BoardNav? AreaAt(Vector3 p)
        {
            if (!Enabled)
                return null;
            BoardNav? best = null;
            float bestDist = float.MaxValue;
            foreach (BoardNav nav in Boards.Values)
            {
                float d = Utils.DistanceXZ(p, nav.Center);
                if (d <= nav.Radius && d < bestDist)
                {
                    best = nav;
                    bestDist = d;
                }
            }
            return best;
        }

        public static BoardNav? Nearest(Vector3 p) => Boards.Values.OrderBy(n => Utils.DistanceXZ(p, n.Center)).FirstOrDefault();

        public static BoardNav? Get(string boardId) => Boards.TryGetValue(boardId, out BoardNav nav) ? nav : null;
    }
}
