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
        public Stance Stance => (Stance)(Hireling.Zdo?.GetInt(HirelingZdo.Stance) ?? 0);
        public Character? CombatTarget => _combat?.Target;

        /// <summary>Badly hurt and falling back (with hysteresis), unless an aggressive guard.</summary>
        public bool Retreating { get; private set; }

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
                    return (h.Home, h.Radius);
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
            Threats = new ThreatScanner(this);
            Add(new IdleBehaviour());
            Add(new LeaveBehaviour());
            Add(new GuardPatrolBehaviour());
            Add(new PostBehaviour());
            switch (hireling.Job)
            {
                case JobType.Woodcutter:
                    Add(Gather = new Work.GatherBehaviour(new Work.WoodcutterProfile()));
                    Add(new Work.DeliverBehaviour(new Work.GathererDeliveryPolicy()));
                    break;
                case JobType.Smelter:
                    Add(new Work.SmelterBehaviour());
                    Add(new Work.DeliverBehaviour(new Work.SmelterDeliveryPolicy()));
                    break;
                case JobType.Miner:
                    Add(Gather = new Work.GatherBehaviour(new Work.MinerProfile()));
                    Add(new Work.DeliverBehaviour(new Work.GathererDeliveryPolicy()));
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
                Threats.OnDamaged(attacker);
                _combat.OnHit();
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
            _doors.CloseBehind(false);
            if (m_randomMoveUpdateTimer > 0f)
                m_randomMoveUpdateTimer -= dt;
            m_timeSinceHurt += dt;
            ApplyRunBoost();
            UpdateRegeneration(dt);
            Regenerate(dt);
            Threats.Tick(VfhConfig.ThreatScanIntervalSeconds.Value);
            bool retreat = StanceRules.ShouldRetreat(Stance, Hireling.Job.IsGuard(), Hireling.Humanoid.GetHealthPercentage(), Retreating);
            if (retreat != Retreating)
            {
                Retreating = retreat;
                VfhLog.D(LogCat.Combat, retreat ? "retreat.start" : "retreat.end", ("hid", Hireling.Hid), ("health", Hireling.Humanoid.GetHealthPercentage()));
            }

            // Stand still while someone is using the cargo, so the container doesn't close as they walk off.
            if (Hireling.CargoInUse)
            {
                if (!_heldForCargo)
                {
                    _heldForCargo = true;
                    VfhLog.D(LogCat.AI, "ai.hold", ("hid", Hireling.Hid), ("reason", "cargo open"));
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
                VfhLog.D(LogCat.AI, "ai.release", ("hid", Hireling.Hid), ("reason", "cargo closed"));
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
                next.Tick(this, dt);
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
            // Doors: open what's ahead, and if there's no route at all, go via a door that leads towards the goal.
            _doors.Tick(point);
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
        public bool CanReach(Vector3 point) => HavePath(point);

        public void Wander(float dt, Vector3 center) => RandomMovement(dt, center, snapToGround: true);

        public void Halt() => StopMoving();

        /// <summary>Walk straight at a point, steering round what's in the way (no pathfinding): unstick detours.</summary>
        public void MoveAround(float dt, Vector3 point) => MoveAndAvoid(dt, point, 0.5f, true);

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
