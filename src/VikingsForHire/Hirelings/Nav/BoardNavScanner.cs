using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Nav;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>
    /// Scans one board's area into links, a few dozen pieces per frame: every door, and every piece whose surface
    /// climbs from one floor to another (by its shape, so modded stairs count; the data file's lists override).
    /// </summary>
    internal sealed class BoardNavScanner
    {
        private const int PiecesPerFrame = 40;
        private const double FrameBudgetMs = 2.0;
        private const float DoorDepth = 1.2f;
        private const float EndOut = 0.7f;

        private static readonly string[] Hints = { "stair", "ladder", "step" };

        private readonly BoardNav _nav;
        private readonly List<Piece> _pieces = new();
        private readonly List<NavLink> _links = new();
        private readonly List<Collider> _colliders = new();
        private readonly List<Vector3> _points = new();
        private readonly HashSet<string> _include;
        private readonly HashSet<string> _exclude;
        private int _next;
        private int _frames;
        private double _ms;
        private int _doors, _stairs, _ladders, _rejected;

        public bool Done { get; private set; }

        public BoardNavScanner(BoardNav nav)
        {
            _nav = nav;
            Piece.GetAllPiecesInRadius(nav.Center, nav.Radius, _pieces);
            _include = new HashSet<string>(DataStore.Current.NavLinks.Include, StringComparer.OrdinalIgnoreCase);
            _exclude = new HashSet<string>(DataStore.Current.NavLinks.Exclude, StringComparer.OrdinalIgnoreCase);
        }

        public void Step()
        {
            var sw = Stopwatch.StartNew();
            int n = 0;
            while (_next < _pieces.Count && n < PiecesPerFrame && sw.Elapsed.TotalMilliseconds < FrameBudgetMs)
            {
                Piece p = _pieces[_next++];
                n++;
                if (p != null && p.m_nview != null && p.m_nview.IsValid())
                    VfhLog.Guard(LogCat.Nav, "navlinks.piece_failed", () => Consider(p));
            }
            _frames++;
            _ms += sw.Elapsed.TotalMilliseconds;
            if (_next >= _pieces.Count)
                Finish();
        }

        private void Finish()
        {
            _nav.Graph.Rebuild(_links);
            _nav.Rejected = _rejected;
            Done = true;
            VfhLog.I(LogCat.Nav, "navlinks.scan", ("board", _nav.BoardId), ("pieces", _pieces.Count), ("doors", _doors), ("stairs", _stairs),
                ("ladders", _ladders), ("rejected", _rejected), ("ms", Math.Round(_ms, 2)), ("frames", _frames), ("version", _nav.Graph.Version));
        }

        private void Consider(Piece piece)
        {
            if (piece.GetComponent<HiringBoard>() != null)
                return;
            string prefab = Utils.GetPrefabName(piece.gameObject);
            Door? door = piece.GetComponentInChildren<Door>();
            if (door != null)
            {
                AddDoor(door, piece, prefab);
                return;
            }
            bool hint = _include.Contains(prefab) || Hints.Any(h => prefab.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                    piece.m_name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_exclude.Contains(prefab))
            {
                Reject(piece, prefab, "excluded", hint);
                return;
            }
            if (!StairSampler.Colliders(piece, _colliders, out Bounds b))
            {
                if (hint)
                    Reject(piece, prefab, "no_colliders", true);
                return;
            }
            if (!hint && (b.size.y < 0.8f || b.size.y > 6f || Mathf.Max(b.size.x, b.size.z) > 8f))
                return; // not a stair candidate: not worth a log line
            AddStair(piece, prefab, b, hint);
        }

        private void AddDoor(Door door, Piece piece, string prefab)
        {
            Vector3 c = door.transform.position;
            Vector3 f = door.transform.forward;
            f.y = 0f;
            f.Normalize();
            Vector3 a = OnFloor(c + f * DoorDepth, c.y);
            Vector3 bSide = OnFloor(c - f * DoorDepth, c.y);
            _links.Add(new NavLink(NavLinkKind.Door, a.ToNav(), bSide.ToNav(), null, piece.m_nview.GetZDO().m_uid.ToString(), prefab));
            _doors++;
        }

        // The floor under a door's side (or the door's own height when there's none, e.g. over water).
        private static Vector3 OnFloor(Vector3 p, float doorY)
        {
            float? floor = StairSampler.FloorAt(p, doorY, 1.0f);
            p.y = floor ?? doorY;
            return p;
        }

        private void AddStair(Piece piece, string prefab, Bounds b, bool hint)
        {
            StairResult best = StairResult.Reject("no_surface");
            List<Vector3>? bestPoints = null;
            List<(float Along, float? Height)>? bestSamples = null;
            foreach (Vector3 axis in new[] { piece.transform.forward, piece.transform.right })
            {
                List<(float Along, float? Height)> samples = StairSampler.Sample(_colliders, b, axis, _points);
                StairResult r = StairProfile.Classify(samples, hint);
                if (r.Accepted && (!best.Accepted || r.Rise > best.Rise))
                {
                    best = r;
                    bestPoints = new List<Vector3>(_points);
                    bestSamples = samples;
                }
                else if (!best.Accepted && r.Reason != "no_surface")
                    best = r; // keep the more telling reason
            }

            if (best.Accepted && bestPoints != null)
            {
                string? why = Ends(bestPoints, best, out Vector3 bottom, out Vector3 top);
                if (why == null)
                {
                    int step = best.TopIndex > best.BottomIndex ? 1 : -1;
                    var way = new List<NavPoint> { bottom.ToNav() };
                    for (int i = best.BottomIndex; i != best.TopIndex + step; i += step)
                        if (bestSamples![i].Height != null) // a missed sample has no real height to walk to
                            way.Add(bestPoints[i].ToNav());
                    way.Add(top.ToNav());
                    _links.Add(new NavLink(NavLinkKind.Stair, bottom.ToNav(), top.ToNav(), way, piece.m_nview.GetZDO().m_uid.ToString(), prefab, best.IsLadder));
                    if (best.IsLadder)
                        _ladders++;
                    else
                        _stairs++;
                    return;
                }
                Reject(piece, prefab, why, hint);
                return;
            }
            if (hint && (best.Reason == "no_surface" || best.Reason == "too_steep") && LadderByBounds(piece, prefab, b))
                return;
            Reject(piece, prefab, best.Reason, hint);
        }

        // Where to stand at each end: just beyond the bottom and top samples, on a floor, with room overhead.
        private static string? Ends(List<Vector3> pts, StairResult r, out Vector3 bottom, out Vector3 top)
        {
            Vector3 lo = pts[r.BottomIndex], hi = pts[r.TopIndex];
            Vector3 up = hi - lo;
            up.y = 0f;
            up = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.forward;
            bottom = lo - up * EndOut;
            top = hi + up * EndOut;
            float? floorLo = StairSampler.FloorAt(bottom, lo.y, 0.5f);
            if (floorLo == null)
                return "no_bottom_floor";
            float? floorHi = StairSampler.FloorAt(top, hi.y, 0.4f);
            if (floorHi == null)
                return "no_top_floor";
            bottom.y = floorLo.Value;
            top.y = floorHi.Value;
            if (!StairSampler.Headroom(bottom) || !StairSampler.Headroom(top))
                return "no_headroom";
            return null;
        }

        // A ladder with no walkable surface (a thin collider): its foot on a floor on one side, its top on a floor on another.
        private bool LadderByBounds(Piece piece, string prefab, Bounds b)
        {
            Vector3 f = piece.transform.forward, r = piece.transform.right;
            f.y = 0f;
            r.y = 0f;
            Vector3[] dirs = { f.normalized, -f.normalized, r.normalized, -r.normalized };
            float Reach(Vector3 d) => Mathf.Abs(d.x) * b.extents.x + Mathf.Abs(d.z) * b.extents.z;
            foreach (Vector3 dLo in dirs)
            {
                Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z) + dLo * (Reach(dLo) + 0.6f);
                if (StairSampler.FloorAt(bottom, b.min.y, 0.5f) is not float lo || !StairSampler.Headroom(bottom with { y = lo }))
                    continue;
                foreach (Vector3 dHi in dirs)
                {
                    Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z) + dHi * (Reach(dHi) + EndOut);
                    if (StairSampler.FloorAt(top, b.max.y, 0.5f) is not float hi || !StairSampler.Headroom(top with { y = hi }))
                        continue;
                    bottom.y = lo;
                    top.y = hi;
                    var way = new List<NavPoint> { bottom.ToNav(), new Vector3(b.center.x, lo, b.center.z).ToNav(), new Vector3(b.center.x, hi, b.center.z).ToNav(), top.ToNav() };
                    _links.Add(new NavLink(NavLinkKind.Stair, bottom.ToNav(), top.ToNav(), way, piece.m_nview.GetZDO().m_uid.ToString(), prefab, isLadder: true));
                    _ladders++;
                    return true;
                }
            }
            return false;
        }

        private void Reject(Piece piece, string prefab, string reason, bool hint)
        {
            _rejected++;
            if (hint)
                VfhLog.D(LogCat.Nav, "navlinks.piece_rejected", ("board", _nav.BoardId), ("prefab", prefab), ("pos", piece.transform.position), ("reason", reason));
        }
    }
}
