using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Hirelings.Combat;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// A MonsterAI subclass (so later phases can use its attack/follow helpers) whose brain is replaced: UpdateAI keeps
    /// BaseAI's housekeeping (owner check, timers, health regeneration) and then runs the highest-priority behaviour that
    /// wants control. The monster logic (targeting, aggression, despawning) never runs.
    /// </summary>
    internal sealed class HirelingAI : MonsterAI
    {
        private readonly List<IHirelingBehaviour> _behaviours = new();
        private IHirelingBehaviour? _current;
        private bool _heldForCargo;
        private CombatBehaviour _combat = null!;
        private float _regenTimer;
        private float _baseRunSpeed = -1f;
        private float _runBoost;
        private float _runBoostUntil;

        public ThreatScanner Threats { get; private set; } = null!;

        /// <summary>Attacks seen coming at it (BlockAndDodge).</summary>
        public AttackReader Reader { get; private set; } = null!;
        public IReadOnlyList<IncomingAttack> Incoming => Reader.Incoming;
        public DefenseStats Defense { get; } = new();

        /// <summary>When it last took damage (Time.time).</summary>
        public float LastHitTime { get; private set; } = -999f;
        public Stance Stance => (Stance)(Hireling.Zdo?.GetInt(HirelingZdo.Stance) ?? 0);
        public Character? CombatTarget => _combat?.Target;

        /// <summary>Badly hurt and falling back (with hysteresis), unless an aggressive guard.</summary>
        public bool Retreating { get; private set; }

        /// <summary>
        /// The owner's retreat order (middle click with the stone): drop every fight and follow, ignoring enemies, until
        /// RetreatQuietSeconds pass without a hit (each hit restarts that), at most RetreatMaxSeconds, or a new order.
        /// </summary>
        public bool RetreatOrdered => _retreatStarted >= 0f && Time.time < _retreatUntil;
        private float _retreatStarted = -1f;
        private float _retreatUntil;
        public const float RetreatQuietSeconds = 20f;
        public const float RetreatMaxSeconds = 60f;

        public void OrderRetreat()
        {
            _retreatStarted = Time.time;
            _retreatUntil = Time.time + RetreatQuietSeconds;
        }

        public void CancelRetreat()
        {
            if (_retreatStarted >= 0f && RetreatOrdered)
                VfhLog.D(LogCat.Orders, "order.retreat_end", ("hid", Hireling.Hid), ("why", "new order"));
            _retreatStarted = -1f;
        }

        private void RetreatHit()
        {
            if (RetreatOrdered)
                _retreatUntil = Mathf.Min(Time.time + RetreatQuietSeconds, _retreatStarted + RetreatMaxSeconds);
        }

        /// <summary>Where a fight is measured from for the leash: home for base workers (phase 12 switches it to the owner).</summary>
        /// <summary>Where fights are kept close to: home, or while following the owner (or the stay spot).</summary>
        public Vector3 LeashCenter
        {
            get
            {
                if (Hireling.Mode != HirelingMode.Following)
                    return Hireling.HasPost ? Hireling.PostPos : Hireling.Home;
                if (Hireling.FollowMode != FollowMode.Follow)
                    return Hireling.StayPos;
                Player? owner = Player.GetPlayer(Hireling.OwnerId);
                return owner != null ? owner.transform.position : transform.position;
            }
        }

        /// <summary>How far from LeashCenter a fight may go: the work radius at home, 30 m around the owner.</summary>
        public float LeashRadius => Hireling.Mode == HirelingMode.Following ? FollowerLeash
            : Hireling.HasPost ? VfhConfig.PostLeashRadius.Value : Hireling.Radius;

        public const float FollowerLeash = 30f;

        /// <summary>The current stone order, if any (only on the owner's machine).</summary>
        public Followers.FieldOrder? Order { get; set; }

        /// <summary>The job's gathering behaviour (woodcutters, miners), which Follow drives in the field.</summary>
        public Work.GatherBehaviour? Gather { get; private set; }

        /// <summary>A Steward's field looting in Gather Here (Stewards only).</summary>
        public Work.FieldLoot? Loot { get; private set; }

        /// <summary>The chore loop of a Steward, Farmer or Cook (null for other jobs).</summary>
        public Work.Chores.ChoreLoop? Chores { get; private set; }

        /// <summary>
        /// Where this hireling may gather right now: the board's area while working; around a harvest order's target;
        /// around its parked spot in Gather Here. Null = not gathering.
        /// </summary>
        public (Vector3 Center, float Radius)? WorkArea
        {
            get
            {
                Hireling h = Hireling;
                if (h.Mode == HirelingMode.Working)
                    return h.WorksAtHome ? (h.Home, h.Radius) : null; // "work at home" off: idle at base
                if (h.Mode != HirelingMode.Following)
                    return null;
                if (Order is { Kind: Followers.FieldOrder.OrderKind.Harvest } o && !o.Expired)
                    return (o.Position, Followers.FieldOrder.HarvestRadius);
                if (h.FollowMode == FollowMode.GatherHere)
                    return (h.StayPos, VfhConfig.GatherNearbyRadius.Value);
                return null;
            }
        }

        public Hireling Hireling { get; private set; } = null!;

        public string CurrentBehaviour => _current?.Name ?? "none";

        // No Awake override: the reference assembly is publicized, so MonsterAI.Awake looks public at compile time but is
        // protected at runtime. Hireling.Awake calls Init instead.
        public void Init(Hireling hireling)
        {
            if (Hireling != null)
                return;
            Hireling = hireling;
            Nav.LegOracle.Agent = m_pathAgentType;
            Threats = new ThreatScanner(this);
            Reader = new AttackReader(this);
            Add(new IdleBehaviour());
            Add(new TestWalkBehaviour());
            Add(new LeaveBehaviour());
            Add(new GuardPatrolBehaviour());
            Add(new PostBehaviour());
            Add(new ParkBehaviour());
            switch (hireling.Job)
            {
                case JobType.Woodcutter:
                    Add(Gather = new Work.GatherBehaviour(new Work.WoodcutterProfile()));
                    Add(new Work.Trees.PlantTreesBehaviour());
                    Add(new Work.DeliverBehaviour(new Work.GathererDeliveryPolicy()));
                    break;
                case JobType.Smelter:
                    Add(Chores = new Work.Chores.ChoreLoop(JobType.Smelter, Work.Steward.StewardChores.All()));
                    Loot = new Work.FieldLoot();
                    Add(new Work.DeliverBehaviour(new Work.ChoreDeliveryPolicy()));
                    break;
                case JobType.Miner:
                    Add(Gather = new Work.GatherBehaviour(new Work.MinerProfile(hireling)));
                    Add(new Work.DeliverBehaviour(new Work.GathererDeliveryPolicy()));
                    break;
                case JobType.Farmer:
                    Add(Chores = new Work.Chores.ChoreLoop(JobType.Farmer, new Work.Chores.IChore[] { new Work.Farm.HarvestChore(), new Work.Farm.PlantChore() }));
                    Add(new Work.DeliverBehaviour(new Work.ChoreDeliveryPolicy()));
                    break;
                case JobType.Cook:
                    Add(Chores = new Work.Chores.ChoreLoop(JobType.Cook, new Work.Chores.IChore[] { new Work.Kitchen.StovesChore(), new Work.Kitchen.CraftChore() }));
                    // The farm's seed reserve (and what's planted for seed orders) is never the Cook's to use.
                    Chores.PrepareContext = ctx =>
                    {
                        if (Board.BoardOrders.BoardOf(ctx.Hireling.BoardId) is Board.HiringBoard b)
                        {
                            System.Collections.Generic.Dictionary<string, int> keep = Work.Kitchen.KitchenState.Protected(b);
                            ctx.ExtraReserve = item => keep.TryGetValue(item, out int n) ? n : 0;
                        }
                    };
                    Add(new Work.DeliverBehaviour(new Work.ChoreDeliveryPolicy()));
                    break;
                default:
                    Add(new Work.DeliverBehaviour(new Work.OnRequestDeliveryPolicy()));
                    break;
            }
            Add(new Followers.FollowBehaviour());
            Add(new FleeBehaviour());
            _combat = new CombatBehaviour();
            Add(_combat);
            hireling.Humanoid.m_onDamaged += (damage, attacker) =>
            {
                LastHitTime = Time.time;
                Threats.OnDamaged(attacker);
                _combat.OnHit();
                RetreatHit();
                Telemetry.BalanceFights.Taken(Hireling, damage);
                VfhLog.D(LogCat.Combat, "hireling.damaged", ("hid", Hireling.Hid), ("by", attacker != null ? attacker.m_name : "none"), ("damage", damage),
                    ("health", Hireling.Humanoid.GetHealth()));
            };
        }

        /// <summary>Later phases register job, combat and follow behaviours here.</summary>
        public void Add(IHirelingBehaviour behaviour)
        {
            _behaviours.Add(behaviour);
            _behaviours.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public override bool UpdateAI(float dt)
        {
            long started = PerfCounters.Start();
            try
            {
                return UpdateAIInner(dt);
            }
            finally
            {
                PerfCounters.Add("ai", started);
            }
        }

        private bool UpdateAIInner(float dt)
        {
            if (Hireling == null || !m_nview.IsValid() || !m_nview.IsOwner())
                return false;
            // In transit through a portal, aboard a ship, or on its way home: held at a point, nothing else runs.
            Vector3? pin = TravelPin ?? (Hireling.IsStowed ? Followers.ShipStowage.CarryPoint(Hireling) : null)
                ?? (Hireling.Mode == HirelingMode.Returning ? Followers.HomeReturn.HoldOrArrive(Hireling) : null);
            if (pin is Vector3 at)
            {
                Hireling.SetSneaking(false);
                Hold(at);
                return true;
            }
            _doors.CloseBehind(false);
            if (m_randomMoveUpdateTimer > 0f)
                m_randomMoveUpdateTimer -= dt;
            m_timeSinceHurt += dt;
            ApplyRunBoost();
            UpdateRegeneration(dt);
            Regenerate(dt);
            Threats.Tick(VfhConfig.ThreatScanIntervalSeconds.Value);
            Reader.Tick();
            bool retreat = StanceRules.ShouldRetreat(Stance, Hireling.Job.IsGuard(), Hireling.Humanoid.GetHealthPercentage(), Retreating);
            if (retreat != Retreating)
            {
                Retreating = retreat;
                VfhLog.D(LogCat.Combat, retreat ? "retreat.start" : "retreat.end", ("hid", Hireling.Hid), ("health", Hireling.Humanoid.GetHealthPercentage()));
            }

            // Stand still while someone is using the cargo (so the container doesn't close as they walk off) or has its
            // Shift+E panel open.
            bool cargo = Hireling.CargoInUse;
            if (cargo || Hireling.PanelHeld)
            {
                if (!_heldForCargo)
                {
                    _heldForCargo = true;
                    VfhLog.D(LogCat.AI, "ai.hold", ("hid", Hireling.Hid), ("reason", cargo ? "cargo open" : "panel open"));
                }
                StopMoving();
                Player nearest = Player.GetClosestPlayer(transform.position, 6f);
                if (nearest != null)
                    LookAt(nearest.GetHeadPoint());
                return true;
            }
            if (_heldForCargo)
            {
                _heldForCargo = false;
                VfhLog.D(LogCat.AI, "ai.release", ("hid", Hireling.Hid), ("reason", "cargo or panel closed"));
            }

            VfhLog.Guard(LogCat.AI, "ai.tick_failed", () =>
            {
                long t = PerfCounters.Start();
                IHirelingBehaviour next = _behaviours.First(b => b.Wants(this));
                PerfCounters.Add("choose", t);
                if (next != _current)
                {
                    VfhLog.D(LogCat.AI, "ai.switch", ("hid", Hireling.Hid), ("from", CurrentBehaviour), ("to", next.Name));
                    _current = next;
                }
                t = PerfCounters.Start();
                WantSneak = false;
                next.Tick(this, dt);
                Hireling.SetSneaking(WantSneak); // only the follow behaviour asks for it, so anything else stands it up
                PerfCounters.Add(next.Name, t);
            }, ("hid", Hireling.Hid));
            return true;
        }

        private float _noPathLogAt;
        private DoorHelper? _doorHelper;
        private DoorHelper _doors => _doorHelper ??= new DoorHelper(this);

        /// <summary>
        /// Pathfinds to a point. When the pathfinder has no route (vanilla then just stops: e.g. the hireling is wedged
        /// between pieces, off the nav mesh), it walks straight there steering round obstacles instead of standing still.
        /// </summary>
        public bool WalkTo(float dt, Vector3 point, float stopDistance, bool run)
        {
            // Inside a board's area with no full route on the game's map: through the board's doors and stairs.
            if (Nav.NavLinkRegistry.Enabled && Links.Walk(dt, point, stopDistance, run) is bool viaLinks)
                return viaLinks;
            if (Vector3.Distance(point, transform.position) > stopDistance)
                TrackProgress(point);
            // Doors: open what's ahead, and if there's no route at all, go via a door that leads towards the goal.
            _doors.Tick(point);
            // Going through a door it just opened: straight to the far side first.
            if (_doors.Through is Vector3 beyond)
            {
                Vector3 dir = beyond - transform.position;
                dir.y = 0f;
                MoveTowards(dir.normalized, run);
                return false;
            }
            // Close on the map but on another floor (a chest upstairs, above the stairs we're on): vanilla's walking
            // counts only the distance along the ground and would call this arrived, leaving the hireling under the
            // floor for good. Keep going along the route until the height matches too.
            if (Utils.DistanceXZ(point, transform.position) <= stopDistance && Mathf.Abs(point.y - transform.position.y) > 1.3f)
                return Climb(dt, point, run);
            // Ask the pathfinder about the goal itself (cached by BaseAI): an earlier result may be for another target.
            bool near = Utils.DistanceXZ(transform.position, point) <= stopDistance + 1f;
            Vector3? via = _doors.Detour(point, near || _doors.HasDetour || PathReaches(point, Mathf.Max(stopDistance, 1f) + 1.5f));
            if (via is Vector3 door && Utils.DistanceXZ(transform.position, door) > 0.6f)
            {
                MoveTo(dt, door, 0.4f, run);
                return false;
            }
            bool arrived = MoveTo(dt, point, stopDistance, run);
            if (!arrived || FoundPath() || Utils.DistanceXZ(point, transform.position) <= Mathf.Max(stopDistance, run ? 1f : 0.5f))
                return arrived;
            if (Time.time - _noPathLogAt > 10f)
            {
                _noPathLogAt = Time.time;
                VfhLog.D(LogCat.AI, "ai.no_path", ("hid", Hireling.Hid), ("from", transform.position), ("to", point));
            }
            return MoveAndAvoid(dt, point, stopDistance, run);
        }

        // The pathfinder also returns partial routes (as close as it can get); only a route ending near the goal counts.
        private bool PathReaches(Vector3 point, float within) =>
            FindPath(point) && m_path.Count > 0 && Utils.DistanceXZ(m_path[m_path.Count - 1], point) <= within;

        /// <summary>Whether the pathfinder has a full route from here to the point.</summary>
        public bool CanReach(Vector3 point) => HavePath(point) || (Nav.NavLinkRegistry.Enabled && Links.Reachable(point));

        private Nav.LinkNavigator? _links;

        /// <summary>This hireling's routes through its board's doors and stairs.</summary>
        internal Nav.LinkNavigator Links => _links ??= new Nav.LinkNavigator(this);

        /// <summary>The game's map has a full route ending near the goal on its floor (not under or over it).</summary>
        /// <remarks>Its own query: BaseAI.FindPath hands back its last result for any target within a second.</remarks>
        internal bool FullRouteTo(Vector3 goal, float within)
        {
            if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(transform.position, goal, _directPath, m_pathAgentType, requireFullPath: true) || _directPath.Count == 0)
                return false;
            Vector3 end = _directPath[_directPath.Count - 1];
            return Utils.DistanceXZ(end, goal) <= within && Mathf.Abs(end.y - goal.y) <= 1f;
        }

        private readonly List<Vector3> _directPath = new();

        // For LinkNavigator: BaseAI's movement is protected at runtime (the publicized reference only looks public).
        internal bool MoveToPublic(float dt, Vector3 point, float stop, bool run) => MoveTo(dt, point, stop, run);
        internal bool ClimbTo(float dt, Vector3 point, bool run) => Climb(dt, point, run);
        internal void Track(Vector3 point) => TrackProgress(point);
        internal void DoorTick(Vector3 goal) => _doors.Tick(goal);
        internal void MarkDoorOpened(Door door) => _doors.MarkOpened(door);

        public void Wander(float dt, Vector3 center) => RandomMovement(dt, center, snapToGround: true);

        public void Halt() => StopMoving();

        /// <summary>Walk straight at a point, steering round what's in the way (no pathfinding): unstick detours.</summary>
        public void MoveAround(float dt, Vector3 point) => MoveAndAvoid(dt, point, 0.5f, true);

        /// <summary>Whether the last chase went straight at the target (no full route), for logs.</summary>
        public bool ChasingDirect { get; private set; }

        /// <summary>
        /// Go after a moving target (a follower after its owner). Use the route when it reaches the target; otherwise
        /// head straight for it, steering round obstacles. The game builds its walkable map a tile at a time around where
        /// it's asked, so ahead of a sprinting player there's often none yet, and the pathfinder then returns a partial
        /// route ending at the edge of what's built, behind the follower: following that walked it backwards.
        /// </summary>
        public bool Chase(float dt, Vector3 point, float stopDistance, bool run)
        {
            // Close along the ground counts as there, unless it's another floor of a building at home.
            bool otherFloor = Mathf.Abs(point.y - transform.position.y) > 1.5f && Nav.NavLinkRegistry.Enabled && Links.Active(point);
            if (Utils.DistanceXZ(point, transform.position) <= stopDistance && !otherFloor)
            {
                StopMoving();
                return true;
            }
            // Inside a board's area with no full route to the owner: through the board's doors and stairs.
            if (Nav.NavLinkRegistry.Enabled && Links.Chase(dt, point, stopDistance, run) is bool viaLinks)
                return viaLinks;
            TrackProgress(point);
            bool route = PathReaches(point, Mathf.Max(stopDistance, 1f) + 3f);
            if (route != !ChasingDirect)
            {
                ChasingDirect = !route;
                VfhLog.D(LogCat.Follow, "follow.chase", ("hid", Hireling.Hid), ("how", route ? "route" : "direct"), ("dist", Utils.DistanceXZ(point, transform.position)));
            }
            if (route)
                return WalkTo(dt, point, stopDistance, run);
            _doors.Tick(point);
            if (_doors.Through is Vector3 beyond)
            {
                Vector3 dir = beyond - transform.position;
                dir.y = 0f;
                MoveTowards(dir.normalized, run);
                return false;
            }
            // A wall between us and no route (the walkable map lags behind a door just opened): use a doorway rather
            // than pushing against the wall beside it (which ended in the unstuck jump over it).
            if (WallBetween(point) && _doors.DoorwayTowards(point) is Vector3 doorway)
            {
                // By route to the doorway when there is one (round a corner of the building), else straight at it.
                if (PathReaches(doorway, 1.5f))
                    MoveTo(dt, doorway, 0.4f, run);
                else
                    MoveAndAvoid(dt, doorway, 0.4f, run);
                return false;
            }
            return MoveAndAvoid(dt, point, stopDistance, run);
        }

        // Follow the route's waypoints ourselves (vanilla MoveTo stops by ground distance); straight on if there's none.
        private bool Climb(float dt, Vector3 point, bool run)
        {
            if (FindPath(point) && m_path.Count > 0)
            {
                while (m_path.Count > 1 && Vector3.Distance(m_path[0], transform.position) < 0.7f)
                    m_path.RemoveAt(0);
                Vector3 dir = m_path[0] - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.04f)
                {
                    MoveTowards(dir.normalized, run);
                    return false;
                }
            }
            MoveAndAvoid(dt, point, 0.3f, run);
            return false;
        }

        // Progress towards where we're walking, for the stuck log and for behaviours that give up on a target.
        private Vector3 _navTarget = new(float.NaN, 0f, 0f);
        private float _navBest = float.MaxValue;
        private float _navProgressAt;
        private float _navLoggedAt = -999f;
        private int _navJumps;
        private const float NavStuckSeconds = 8f;

        private void TrackProgress(Vector3 point)
        {
            if (!(Vector3.Distance(point, _navTarget) < 1.5f))
            {
                _navTarget = point;
                _navBest = float.MaxValue;
                _navProgressAt = Time.time;
                _navJumps = 0;
            }
            float d = Vector3.Distance(transform.position, point);
            if (d < _navBest - 0.5f)
            {
                _navBest = d;
                _navProgressAt = Time.time;
                return;
            }
            if (Time.time - _navProgressAt < NavStuckSeconds)
                return;
            // Stuck: log what the pathfinder had (always on, at most every 30 s per hireling) and try a jump or two.
            if (Time.time - _navLoggedAt > 30f)
            {
                _navLoggedAt = Time.time;
                Vector3 me = transform.position;
                Vector3? end = m_path.Count > 0 ? m_path[m_path.Count - 1] : null;
                VfhLog.I(LogCat.Nav, "nav.stuck", ("hid", Hireling.Hid), ("doing", CurrentBehaviour), ("pos", me), ("to", point),
                    ("dist", d), ("dy", point.y - me.y), ("route", FoundPath()), ("waypoints", m_path.Count),
                    ("next", m_path.Count > 0 ? m_path[0] : (Vector3?)null), ("routeEndsShortBy", end is Vector3 e ? Vector3.Distance(e, point) : -1f),
                    ("onGround", Hireling.Humanoid.IsOnGround()), ("inWater", Hireling.Humanoid.InWater()), ("secs", Time.time - _navProgressAt),
                    ("links", _links?.HasRoute ?? false), ("step", _links?.StepName ?? ""));
            }
            if (_navJumps < 2 && Hireling.Humanoid.IsOnGround())
            {
                Hireling.Humanoid.Jump();
                _navJumps++;
            }
        }

        /// <summary>Seconds without getting closer to <paramref name="target"/> (0 if we're walking somewhere else).</summary>
        public float StuckSeconds(Vector3 target) =>
            Vector3.Distance(target, _navTarget) < 1.5f ? Time.time - _navProgressAt : 0f;

        private static readonly int WallMask = LayerMask.GetMask("piece", "Default", "static_solid");

        // A built wall (or rock) on the straight line between us at chest height.
        private bool WallBetween(Vector3 point) =>
            Physics.Linecast(transform.position + Vector3.up * 1f, point + Vector3.up * 1f, WallMask);

        /// <summary>Set by a behaviour during its tick to crouch (a follower sneaking with its owner).</summary>
        public bool WantSneak { get; set; }

        /// <summary>Set while a follower waits on the far side of a portal for its owner to arrive (TeleportTravel).</summary>
        public Vector3? TravelPin { get; set; }

        private void Hold(Vector3 at)
        {
            StopMoving();
            transform.position = at;
            if (m_body != null)
            {
                m_body.position = at;
                m_body.linearVelocity = Vector3.zero;
            }
            Hireling.Humanoid.m_maxAirAltitude = at.y;
        }

        /// <summary>Forget the current route (after a teleport it leads from where the hireling used to be).</summary>
        public void ResetPath()
        {
            m_path.Clear();
            m_lastFindPathTime = -10f;
            m_lastFindPathTarget = new Vector3(-999999f, -999999f, -999999f);
            ChasingDirect = false;
        }

        public void Face(Vector3 point) => LookAt(point);

        /// <summary>Swing/shoot the current weapon at the target if its attack interval allows (vanilla MonsterAI.DoAttack).</summary>
        public bool Attack(Character target) => DoAttack(target, false);

        /// <summary>A follower falling behind its owner sprints faster for a moment (renewed every tick while it's behind).</summary>
        public void BoostRun(float bonus, float seconds = 0.5f)
        {
            _runBoost = bonus;
            _runBoostUntil = Time.time + seconds;
        }

        private void ApplyRunBoost()
        {
            Humanoid hum = Hireling.Humanoid;
            if (_baseRunSpeed < 0f)
                _baseRunSpeed = hum.m_runSpeed;
            hum.m_runSpeed = Time.time < _runBoostUntil ? _baseRunSpeed * (1f + _runBoost) : _baseRunSpeed;
        }

        /// <summary>1% of max health every 2 s once nothing has hurt them for 10 s.</summary>
        private void Regenerate(float dt)
        {
            _regenTimer += dt;
            if (_regenTimer < 2f)
                return;
            _regenTimer = 0f;
            Humanoid h = Hireling.Humanoid;
            if (m_timeSinceHurt > 10f && h.GetHealth() < h.GetMaxHealth())
                h.Heal(h.GetMaxHealth() * 0.01f, showText: false);
        }
    }
}
