using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Nav;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>
    /// One hireling's way through its board's links. Inside a board's area, when the game's map has no full route to
    /// where it's going on the right floor, it plans one through doors and stairs and walks it step by step: legs on the
    /// game's map, doors opened and stepped through (closed behind by the door helper), stairs walked waypoint by
    /// waypoint with a hop to the far end if it can't make it. A failed step blocks that link (or leg) and replans.
    /// </summary>
    internal sealed class LinkNavigator
    {
        private const float GoalMoved = 1.5f;
        private const float ChaseMoved = 3f;
        private const float PlanEvery = 0.5f;
        private const float ChasePlanEvery = 1f;
        private const float DirectCheckEvery = 2f;
        private const float WalkDone = 0.8f;
        private const float WalkStuckSeconds = 10f;
        private const float DoorSeconds = 6f;
        private const float NeedLegsHoldSeconds = 3f;
        private const float StaleSeconds = 0.5f;
        private const float StaleDistance = 3f;
        private const float DoorReopenSeconds = 1f;
        private const int MaxReplans = 3;

        /// <summary>Hops to a stair's far end since load (for the tests).</summary>
        public static int Hops;

        private readonly HirelingAI _ai;
        private List<RouteStep>? _steps;
        private int _i;
        private Vector3 _goal;
        private int _version;
        private BoardNav? _nav;
        private Vector3 _stepFrom;
        private bool _stepStarted;
        private float _stepStartedAt;
        private int _wp;
        private float _lastPlan = -999f;
        private float _lastGoAt = -999f;
        private Vector3 _planStart;
        private float _doorTouchedAt;
        private float _needLegsSince = -1f;
        private int _replans;
        private Vector3 _fallbackGoal = new(float.NaN, 0, 0);
        private float _fallbackUntil;
        private Vector3 _directGoal = new(float.NaN, 0, 0);
        private float _directAt = -999f;
        private bool _directOk;
        private float _noRouteLogAt = -999f;
        private readonly Dictionary<Vector3Int, (bool Ok, float Until)> _reach = new();

        public LinkNavigator(HirelingAI ai) => _ai = ai;

        public bool HasRoute => _steps != null;

        /// <summary>In the middle of a door or a stair (don't call it stuck, don't turn round).</summary>
        public bool OnLinkStep => _steps != null && _i < _steps.Count && _steps[_i].Kind != RouteStepKind.Walk && _stepStarted;

        public string StepName => _steps != null && _i < _steps.Count ? _steps[_i].ToString() : "";

        /// <summary>The points still ahead on the route (for the overlay).</summary>
        public IEnumerable<Vector3> Remaining()
        {
            if (_steps == null)
                yield break;
            for (int i = _i; i < _steps.Count; i++)
                yield return _steps[i].To.ToUnity();
        }

        /// <summary>The board area both the hireling and the goal are in, if it has links.</summary>
        private BoardNav? Area(Vector3 goal)
        {
            if (!NavLinkRegistry.Enabled)
                return null;
            BoardNav? here = NavLinkRegistry.AreaAt(_ai.transform.position);
            if (here == null || here.Graph.Links.Count == 0 || NavLinkRegistry.AreaAt(goal) != here)
                return null;
            return here;
        }

        public bool Active(Vector3 goal) => Area(goal) != null && !FallingBack(goal);

        private bool FallingBack(Vector3 goal) => Time.time < _fallbackUntil && Vector3.Distance(goal, _fallbackGoal) < GoalMoved;

        /// <summary>
        /// Walk towards the goal through the links if that's needed. Null: not needed or not possible, so the caller walks
        /// the usual way; otherwise whether it has arrived.
        /// </summary>
        public bool? Walk(float dt, Vector3 goal, float stopDistance, bool run) => Go(dt, goal, stopDistance, run, chase: false);

        /// <summary>
        /// A follower after its owner (a moving goal) inside a board's area: through the links when the game's map has no
        /// full route to them; back to the usual chase as soon as it has one.
        /// </summary>
        public bool? Chase(float dt, Vector3 owner, float stopDistance, bool run) => Go(dt, owner, stopDistance, run, chase: true);

        /// <summary>Getting somewhere along a route (for the follower stuck check): mid door or stair, or closing in on the next point.</summary>
        public bool Progressing => _steps != null && (OnLinkStep || (_i < _steps.Count && _ai.StuckSeconds(_steps[_i].To.ToUnity()) < 4f));

        private bool? Go(float dt, Vector3 goal, float stopDistance, bool run, bool chase)
        {
            float moved = chase ? ChaseMoved : GoalMoved;
            float slack = chase ? 3f : 1.5f;
            // Not walked for a moment (combat, a stop near its owner) or pushed away: whatever step it was on is stale.
            if (Time.time - _lastGoAt > StaleSeconds || (_steps != null && _stepStarted && _i < _steps.Count && _steps[_i].Link is NavLink cur &&
                                                      Vector3.Distance(_ai.transform.position, _stepFrom) > StaleDistance + cur.Length))
            {
                Drop();
                _needLegsSince = -1f;
            }
            _lastGoAt = Time.time;
            BoardNav? nav = Area(goal);
            if (nav == null || FallingBack(goal))
            {
                if (!OnLinkStep)
                {
                    Drop();
                    _needLegsSince = -1f;
                    return null;
                }
            }

            if (_steps != null && !OnLinkStep)
            {
                if (Vector3.Distance(goal, _goal) > moved || (_nav != null && _nav.Graph.Version != _version))
                    Drop();
                // A follower takes the usual chase again as soon as the map can get it to its owner.
                else if (chase && DirectOk(goal, stopDistance, slack))
                {
                    Drop();
                    _needLegsSince = -1f;
                    return null;
                }
            }

            if (_steps == null)
            {
                if (nav == null)
                    return null;
                if (Vector3.Distance(goal, _goal) > moved)
                {
                    _replans = 0;
                    _goal = goal;
                    _needLegsSince = -1f;
                }
                if (DirectOk(goal, stopDistance, slack))
                {
                    _needLegsSince = -1f;
                    return null;
                }
                if (Time.time - _lastPlan < (chase ? ChasePlanEvery : PlanEvery))
                    return Waiting();
                Plan(nav, goal);
                if (_steps == null)
                    return Waiting();
            }
            return Step(dt, stopDistance, run);
        }

        // While waiting for the map's answers stand still (moving would change the start and ask again); otherwise the usual way.
        private bool? Waiting()
        {
            if (_needLegsSince < 0f)
                return null;
            _ai.Halt();
            return false;
        }

        private bool DirectOk(Vector3 goal, float stopDistance, float slack)
        {
            if (Vector3.Distance(goal, _directGoal) > GoalMoved || Time.time - _directAt > DirectCheckEvery)
            {
                _directGoal = goal;
                _directAt = Time.time;
                _directOk = _ai.FullRouteTo(goal, Mathf.Max(stopDistance, 1f) + slack);
            }
            return _directOk;
        }

        private void Plan(BoardNav nav, Vector3 goal)
        {
            _lastPlan = Time.time;
            double now = ZNet.instance.GetTimeSeconds();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PlanResult r = LinkPlanner.Plan(nav.Graph, _ai.transform.position.ToNav(), goal.ToNav(), (a, b) => LegOracle.Answer(nav, a, b), now);
            switch (r.Kind)
            {
                case PlanKind.Route:
                    _needLegsSince = -1f;
                    _steps = r.Steps;
                    _i = 0;
                    _planStart = _ai.transform.position;
                    _nav = nav;
                    _version = nav.Graph.Version;
                    _goal = goal;
                    StartStep();
                    VfhLog.D(LogCat.Nav, "navlinks.route", ("hid", _ai.Hireling.Hid), ("goal", goal), ("steps", r.Describe()),
                        ("calls", r.OracleCalls), ("ms", System.Math.Round(sw.Elapsed.TotalMilliseconds, 2)));
                    break;
                case PlanKind.NeedLegs:
                    foreach ((NavPoint a, NavPoint b) in r.Unknown)
                        LegOracle.Queue(nav, a, b);
                    if (_needLegsSince < 0f)
                        _needLegsSince = Time.time;
                    else if (Time.time - _needLegsSince > NeedLegsHoldSeconds)
                        FallBack(goal, 10f, "slow_map");
                    break;
                default:
                    _needLegsSince = -1f;
                    if (Time.time - _noRouteLogAt > 10f)
                    {
                        _noRouteLogAt = Time.time;
                        VfhLog.D(LogCat.Nav, "navlinks.no_route", ("hid", _ai.Hireling.Hid), ("goal", goal));
                    }
                    FallBack(goal, 10f, null);
                    break;
            }
        }

        private void FallBack(Vector3 goal, float seconds, string? why)
        {
            _needLegsSince = -1f;
            _fallbackGoal = goal;
            _fallbackUntil = Time.time + seconds;
            Drop();
            if (why != null)
                VfhLog.I(LogCat.Nav, "navlinks.route_failed", ("hid", _ai.Hireling.Hid), ("goal", goal), ("last", why));
        }

        private void Drop()
        {
            _steps = null;
            _i = 0;
            _stepStarted = false;
        }

        private void StartStep()
        {
            _stepFrom = _ai.transform.position;
            _stepStarted = false;
            _stepStartedAt = Time.time;
            _wp = 0;
        }

        private void Next()
        {
            _i++;
            StartStep();
        }

        // A step failed: block what failed, and plan again (up to a few times for the same goal).
        private void Failed(string what)
        {
            _replans++;
            Vector3 goal = _goal;
            Drop();
            _lastPlan = -999f;
            if (_replans > MaxReplans)
                FallBack(goal, 30f, what);
        }

        private bool Step(float dt, float stopDistance, bool run)
        {
            if (_steps == null || _nav == null)
                return false;
            RouteStep s = _steps[_i];
            bool last = _i == _steps.Count - 1;
            switch (s.Kind)
            {
                case RouteStepKind.Walk:
                    return WalkStep(dt, s, last, stopDistance, run);
                case RouteStepKind.Door:
                    DoorStep(s, run);
                    return false;
                default:
                    StairStep(s, run);
                    return false;
            }
        }

        private bool WalkStep(float dt, RouteStep s, bool last, float stopDistance, bool run)
        {
            Vector3 to = s.To.ToUnity();
            Vector3 me = _ai.transform.position;
            if (last)
            {
                if (Utils.DistanceXZ(me, to) <= stopDistance && Mathf.Abs(me.y - to.y) <= 1.3f)
                {
                    Drop();
                    _ai.Halt();
                    return true;
                }
            }
            // The game's walking stops short of a point (1 m when running), so count that as there.
            else if (Utils.DistanceXZ(me, to) <= Mathf.Max(WalkDone, run ? 1.05f : 0.55f) && Mathf.Abs(me.y - to.y) <= 1f)
            {
                Next();
                return false;
            }
            _ai.Track(to);
            if (_ai.StuckSeconds(to) > WalkStuckSeconds)
            {
                // The leg as the planner asked about it: from the previous step's point (or where the plan started).
                Vector3 from = _i == 0 ? _planStart : _steps![_i - 1].To.ToUnity();
                LegOracle.MarkFailed(_nav!, from, to);
                VfhLog.D(LogCat.Nav, "navlinks.leg_failed", ("hid", _ai.Hireling.Hid), ("from", from), ("to", to));
                Failed("walk");
                return false;
            }
            _ai.DoorTick(to);
            if (Utils.DistanceXZ(me, to) <= 0.4f && Mathf.Abs(me.y - to.y) > 1.3f)
                _ai.ClimbTo(dt, to, run);
            else
                _ai.MoveToPublic(dt, to, last ? Mathf.Max(stopDistance * 0.9f, 0.3f) : 0.4f, run);
            return false;
        }

        private void DoorStep(RouteStep s, bool run)
        {
            NavLink link = s.Link!;
            Vector3 far = s.To.ToUnity();
            Vector3 me = _ai.transform.position;
            Door? door = FindDoor(link.PieceId);
            if (door == null)
            {
                _nav!.Graph.Block(link.Id, ZNet.instance.GetTimeSeconds() + 60.0);
                Failed("door_gone");
                return;
            }
            if (!_stepStarted)
            {
                _stepStarted = true;
                _stepStartedAt = Time.time;
                if (!DoorRules.IsOpen(door))
                {
                    if (!VfhConfig.HirelingsOpenDoors.Value || !DoorRules.Usable(door, DoorRules.BoardOwner(_ai.Hireling.BoardId)))
                    {
                        link.BlockedUntil = ZNet.instance.GetTimeSeconds() + 120.0;
                        VfhLog.I(LogCat.Nav, "navlinks.door_blocked", ("hid", _ai.Hireling.Hid), ("door", door.transform.position),
                            ("why", VfhConfig.HirelingsOpenDoors.Value ? "no_access" : "doors_off"));
                        Failed("door_blocked");
                        return;
                    }
                    DoorRules.Open(door, me);
                    _ai.MarkDoorOpened(door);
                    _doorTouchedAt = Time.time;
                    VfhLog.D(LogCat.Nav, "navlinks.door", ("hid", _ai.Hireling.Hid), ("door", door.transform.position));
                }
            }
            // Closed again in front of it (another hireling behind it, a player): open it again.
            else if (!DoorRules.IsOpen(door) && Time.time - _doorTouchedAt > DoorReopenSeconds && VfhConfig.HirelingsOpenDoors.Value &&
                     DoorRules.Usable(door, DoorRules.BoardOwner(_ai.Hireling.BoardId)))
            {
                DoorRules.Open(door, me);
                _ai.MarkDoorOpened(door);
                _doorTouchedAt = Time.time;
            }
            if (Utils.DistanceXZ(me, far) <= 0.6f)
            {
                Next();
                return;
            }
            if (Time.time - _stepStartedAt > DoorSeconds)
            {
                link.BlockedUntil = ZNet.instance.GetTimeSeconds() + 60.0;
                Failed("door_timeout");
                return;
            }
            Vector3 dir = far - me;
            dir.y = 0f;
            _ai.MoveTowards(dir.normalized, run);
        }

        private void StairStep(RouteStep s, bool run)
        {
            NavLink link = s.Link!;
            IReadOnlyList<NavPoint> way = link.Waypoints.Count > 1 ? link.Waypoints : new[] { link.A, link.B };
            int n = way.Count;
            Vector3 me = _ai.transform.position;
            if (!_stepStarted)
            {
                _stepStarted = true;
                _stepStartedAt = Time.time;
                _wp = 1; // the first waypoint is the end we're standing at
            }
            Vector3 far = s.To.ToUnity();
            if (Utils.DistanceXZ(me, far) <= 0.5f && Mathf.Abs(me.y - far.y) <= 1.0f)
            {
                Next();
                return;
            }
            if (Time.time - _stepStartedAt > 4f + link.Length)
            {
                // Couldn't climb it (too steep, a ladder): move to the far end.
                Vector3 ahead = _i + 1 < _steps!.Count ? _steps[_i + 1].To.ToUnity() : far + (far - me);
                Followers.FollowCatchUp.Place(_ai, far, ahead);
                Hops++;
                VfhLog.I(LogCat.Nav, "navlinks.hop", ("hid", _ai.Hireling.Hid), ("link", link.Id), ("piece", link.Prefab), ("ladder", link.IsLadder),
                    ("secs", System.Math.Round(Time.time - _stepStartedAt, 1)));
                Next();
                return;
            }
            Vector3 target = Point(way, s.FromA, Mathf.Min(_wp, n - 1));
            while (_wp < n - 1 && Utils.DistanceXZ(me, target) <= 0.5f && Mathf.Abs(me.y - target.y) <= 1.0f)
            {
                _wp++;
                target = Point(way, s.FromA, _wp);
            }
            Vector3 dir = target - me;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0025f)
                _ai.MoveTowards(dir.normalized, run);
        }

        private static Vector3 Point(IReadOnlyList<NavPoint> way, bool fromA, int i) => (fromA ? way[i] : way[way.Count - 1 - i]).ToUnity();

        private static Door? FindDoor(string pieceId)
        {
            string[] parts = pieceId.Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], out long user) || !uint.TryParse(parts[1], out uint id))
                return null;
            GameObject? go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(new ZDOID(user, id)) : null;
            return go != null ? go.GetComponentInChildren<Door>() : null;
        }

        /// <summary>Whether a route through the links reaches the point (using what the map has already said; asks for the rest).</summary>
        public bool Reachable(Vector3 point)
        {
            BoardNav? nav = Area(point);
            if (nav == null)
                return false;
            var key = new Vector3Int(Mathf.RoundToInt(point.x * 2f), Mathf.RoundToInt(point.y * 2f), Mathf.RoundToInt(point.z * 2f));
            if (_reach.TryGetValue(key, out var known) && Time.time < known.Until)
                return known.Ok;
            PlanResult r = LinkPlanner.Plan(nav.Graph, _ai.transform.position.ToNav(), point.ToNav(), (a, b) => LegOracle.Answer(nav, a, b),
                ZNet.instance.GetTimeSeconds());
            if (r.Kind == PlanKind.NeedLegs)
            {
                foreach ((NavPoint a, NavPoint b) in r.Unknown)
                    LegOracle.Queue(nav, a, b);
                return false;
            }
            bool ok = r.Kind == PlanKind.Route;
            if (_reach.Count > 64)
                _reach.Clear();
            _reach[key] = (ok, Time.time + 5f);
            return ok;
        }
    }
}
