using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Fight by stance. Melee closes in and swings, guards with shields block incoming swings; archers keep 8–20 m and
    /// shoot when the shot is clear (otherwise they move in, or wait at a post), switching to their club when something
    /// gets within 6 m. Posted guards watch all round. Gives up when the target dies, runs past the leash, or nothing
    /// has happened for 10 s.
    /// </summary>
    internal sealed class CombatBehaviour : IHirelingBehaviour
    {
        private const float MeleeReach = 2.2f;
        private const float RangedMin = 8f;
        private const float RangedMax = 20f;
        // Wizards cast from a little further than archers, and back off from anything inside CastBackOff.
        private const float CastMin = 10f;
        private const float CastMax = 22f;
        private const float CastBackOff = 8f;
        // Backing off is given this long; an enemy that keeps up (a fuling, a greydwarf) is then fought where it stands,
        // or a wizard backed off forever and never cast.
        private const float MaxBackOffSeconds = 3f;
        private float _backingOffSince = -1f;
        private const float SidearmRange = 6f;
        private const float QuietSeconds = 10f;
        private const float BlockSeconds = 0.8f;
        private const float MaxSwingHold = 1f;
        private const float ShotRadius = 0.1f;
        private const float BlockedGiveUpSeconds = 5f;
        // Guards join in on an enemy a player hit this close to them (Defensive and Aggressive).
        private const float PlayerAssistRange = 30f;
        private const float BlockedIgnoreSeconds = 20f;
        private const float NoEffectSeconds = 25f;
        private const float NoEffectIgnoreSeconds = 60f;
        private static readonly int ShotBlockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

        private Character? _target;
        private float _lastAction;
        private float _blockUntil;
        private float _nextAttack;
        private float _swingReadyAt = -1f;
        private static string _lastBlocker = "";
        private Character? _ignored;
        private float _engagedAt = -1f;
        private float _ignoredUntil;
        private float _blockedSince = -1f;
        private float _targetHealth;
        private float _hurtAt;
        private bool _attackedTarget;

        public string Name => "Combat";
        public int Priority => 900;
        public Character? Target => _target;

        /// <summary>How far the target was at this wizard's last cast (-1 before any), for tests.</summary>
        public float LastCastDistance { get; private set; } = -1f;

        /// <summary>The longest cast so far (tests: a swarmed wizard's last cast may be point-blank).</summary>
        public float LongestCastDistance { get; private set; } = -1f;

        public bool Wants(HirelingAI ai)
        {
            if (ai.Retreating)
                return Drop(ai, "retreat");
            if (ai.RetreatOrdered)
                return Drop(ai, "retreat order");
            if (ThreatScanner.Alive(_target) && !Leashed(ai, _target!) && Time.time - _lastAction < QuietSeconds)
                return true;
            if (_target != null)
                Drop(ai, ThreatScanner.Alive(_target) ? "quiet or leashed" : "target down");

            Character? pick = Choose(ai);
            if (pick == null || (pick == _ignored && Time.time < _ignoredUntil) || pick.GetHealth() <= 0f)
                return false;
            // Under the water (a fish or a serpent below the surface by the shore): arrows stop at the surface and swords
            // don't reach, so a guard would shoot or swing at it forever.
            if (Underwater(pick))
            {
                Ignore(ai, pick, "underwater", NoEffectIgnoreSeconds);
                return false;
            }
            _target = pick;
            Telemetry.BalanceFights.Start(ai.Hireling, pick);
            _lastAction = Time.time;
            _engagedAt = Time.time;
            _targetHealth = pick.GetHealth();
            _hurtAt = Time.time;
            _attackedTarget = false;
            VfhLog.D(LogCat.Combat, "combat.engage", ("hid", ai.Hireling.Hid), ("target", pick.m_name), ("stance", ai.Stance),
                ("dist", Vector3.Distance(pick.transform.position, ai.transform.position)), ("gear", GearApplier.Describe(ai.Hireling.Humanoid)));
            return true;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Character target = _target!;
            // Attacking without effect: nothing it does takes any health off the target (it can't be hit where it is,
            // or can't be hurt at all). Every attack used to count as fighting, so it never let go.
            float health = target.GetHealth();
            if (health < _targetHealth - 0.01f)
                _hurtAt = Time.time;
            _targetHealth = health;
            if (_attackedTarget && Time.time - _hurtAt > NoEffectSeconds)
            {
                Ignore(ai, target, "no effect", NoEffectIgnoreSeconds);
                Drop(ai, "no effect");
                return;
            }
            Humanoid me = ai.Hireling.Humanoid;
            float dist = Vector3.Distance(target.transform.position, ai.transform.position) - target.GetRadius();
            if (ai.Hireling.Job == JobType.GuardRanged)
                Ranged(ai, me, target, dist, dt);
            else if (ai.Hireling.Job == JobType.Wizard)
                Cast(ai, me, target, dist, dt);
            else
                Melee(ai, me, target, dist, dt);
            if (!ai.Block.On && Time.time > _blockUntil && me.m_blocking)
                me.m_blocking = false;
        }

        public void OnHit() => _lastAction = Time.time;

        /// <summary>
        /// Vanilla paces monster attacks inside MonsterAI.UpdateAI, which hirelings don't run, so the pace is set here.
        /// </summary>
        private void Attacked(float cooldown)
        {
            if (_engagedAt > 0f)
            {
                VfhLog.D(LogCat.Combat, "combat.first_swing", ("target", _target != null ? _target.m_name : ""), ("secondsAfterEngage", Time.time - _engagedAt));
                _engagedAt = -1f;
            }
            _lastAction = Time.time;
            _nextAttack = Time.time + cooldown;
            _attackedTarget = true;
        }

        internal static bool Underwater(Character c) =>
            c.InWater() && ZoneSystem.instance != null && c.GetCenterPoint().y < ZoneSystem.instance.m_waterLevel - 0.5f && !c.IsFlying();

        // Leave a target alone for a while, saying what it was (at Info: these are the ones players ask about).
        private void Ignore(HirelingAI ai, Character c, string why, float seconds)
        {
            if (_ignored != c || Time.time >= _ignoredUntil)
                VfhLog.I(LogCat.Combat, "combat.ignored", ("hid", ai.Hireling.Hid), ("target", c.m_name), ("prefab", Utils.GetPrefabName(c.gameObject)),
                    ("why", why), ("pos", c.transform.position), ("inWater", c.InWater()), ("health", c.GetHealth()), ("seconds", seconds));
            _ignored = c;
            _ignoredUntil = Time.time + seconds;
            ai.Threats.Ignore(c, seconds);
        }

        private void Melee(HirelingAI ai, Humanoid me, Character target, float dist, float dt)
        {
            // Something flying out of reach overhead: wait under it rather than walk into it forever (it's dropped
            // after the quiet time if it never comes down).
            if (target.IsFlying() && target.transform.position.y - ai.transform.position.y > MeleeReach + 1.5f)
            {
                ai.Halt();
                ai.Face(target.GetCenterPoint());
                return;
            }
            // Out in the water (swimming away, or a serpent): don't wade out after it. Wait on the shore for it to come
            // back; give it up if it doesn't.
            if (dist > MeleeReach && WaterRules.Out(target) && !me.InWater())
            {
                BlockedShot(ai, target, "combat.target_in_water");
                if (Time.time - _blockedSince > BlockedGiveUpSeconds)
                {
                    Ignore(ai, target, "in the water", BlockedIgnoreSeconds);
                    Drop(ai, "in the water");
                    return;
                }
                ai.Halt();
                ai.Face(target.GetCenterPoint());
                return;
            }
            if (dist > MeleeReach)
            {
                // Stop at the target's edge, not its centre: walking to within reach of a troll's centre means walking
                // into the troll, which never ends, so the hireling never got close enough to swing.
                ai.WalkTo(dt, target.transform.position, target.GetRadius() + MeleeReach * 0.7f, run: true);
                return;
            }
            if (!ClearSwing(me, target))
            {
                // In reach but behind a wall (a mob pressed against the base from outside): swinging only hits the wall,
                // and every swing kept the fight going. Walk round to it if there's a way; give up on it if there isn't.
                BlockedShot(ai, target, "combat.swing_blocked");
                if (Time.time - _blockedSince > BlockedGiveUpSeconds)
                {
                    Drop(ai, "blocked");
                    return;
                }
                if (ai.WalkTo(dt, target.transform.position, 0.5f, run: true))
                    ai.Face(target.GetCenterPoint());
                return;
            }
            _blockedSince = -1f;
            ai.Halt();
            ai.Face(target.GetCenterPoint());
            // Swing whenever the swing is ready; raise the shield only in between. Blocking first meant a guard facing
            // a fast or busy enemy (nearly always mid-attack) held its shield up and never hit back.
            // With CombatSkill on a ready swing waits (up to a second) for a block that's about to go up: swinging drops
            // the shield. The block controller raises and lowers it.
            bool blockOwned = ai.Block.On;
            // Shield bash (an enhancement): a stagger between swings, when it's ready.
            if (!me.InAttack() && ai.Guard.TryBash(target, dist))
                _lastAction = Time.time;
            if (Time.time < _nextAttack)
                _swingReadyAt = -1f;
            else if (_swingReadyAt < 0f)
                _swingReadyAt = Time.time;
            bool hold = (blockOwned && ai.Block.HoldSwing() && Time.time - _swingReadyAt < MaxSwingHold) || ai.Dodge.HoldAttack;
            if (Time.time >= _nextAttack && !hold && ai.IsLookingAt(target.GetCenterPoint(), 35f))
            {
                me.m_blocking = false;
                if (ai.Attack(target))
                {
                    Attacked(Config.VfhConfig.MeleeAttackCooldown.Value * ai.Hireling.Enh.Effects.AttackIntervalMult);
                    return;
                }
            }
            bool guard = ai.Hireling.Job == JobType.GuardMelee;
            if (!blockOwned && guard && me.GetLeftItem() != null && target.InAttack() && dist < 4f && !me.InAttack())
            {
                me.m_blocking = true;
                _blockUntil = Time.time + BlockSeconds;
            }
        }

        private void Ranged(HirelingAI ai, Humanoid me, Character target, float dist, float dt)
        {
            bool close = dist < SidearmRange;
            ai.Hireling.UseSidearm(close);
            if (close)
            {
                Melee(ai, me, target, dist, dt);
                return;
            }
            // A posted archer shoots from its post: no backing off or closing in, it just waits for targets in range
            // (and a clear shot).
            bool posted = ai.Hireling.HasPost && ai.Hireling.Mode == HirelingMode.Working;
            if (!posted && dist < RangedMin)
            {
                if (_backingOffSince < 0f)
                    _backingOffSince = Time.time;
                // Backing off in a crypt's corridor walks into the wall behind: give it a few seconds, then shoot from here.
                if (Time.time - _backingOffSince < MaxBackOffSeconds)
                {
                    Vector3 away = (ai.transform.position - target.transform.position).normalized;
                    ai.WalkTo(dt, ai.transform.position + away * 5f, 1f, run: true);
                    return;
                }
            }
            else
                _backingOffSince = -1f;
            // Closing in is measured along the ground: a bat or drake overhead is already as close as walking gets
            // (measured in 3D, an archer under a high flyer kept walking and never shot).
            float flat = Utils.DistanceXZ(target.transform.position, ai.transform.position) - target.GetRadius();
            if (!posted && flat > RangedMax)
            {
                // Not out into the water to get in range: shoot from the shore or let it go.
                if (WaterRules.Out(target) && !me.InWater())
                {
                    BlockedShot(ai, target, "combat.target_in_water");
                    if (Time.time - _blockedSince > BlockedGiveUpSeconds)
                    {
                        Ignore(ai, target, "in the water", BlockedIgnoreSeconds);
                        Drop(ai, "in the water");
                    }
                    else
                        ai.Halt();
                    return;
                }
                ai.WalkTo(dt, target.transform.position, RangedMax * 0.8f, run: true);
                return;
            }
            Vector3? aim = ArrowAim(me, target, out Vector3 from);
            if (aim == null)
            {
                // Every line to the target runs into the ground or a wall: don't waste arrows. A free archer moves in
                // until it has a shot (or the target is close enough for the club); a posted one waits.
                BlockedShot(ai, target);
                if (posted)
                {
                    ai.Halt();
                    ai.Face(target.GetCenterPoint());
                }
                else if (NoWayToAShot(ai, target))
                    return;
                else
                {
                    ai.WalkTo(dt, target.transform.position, SidearmRange - 1f, run: true);
                }
                return;
            }
            _blockedSince = -1f;
            ai.Halt();
            // Aim from where the arrow leaves the bow, not from the eyes: vanilla shoots along the look direction from
            // the bow hand, so looking from the eyes put every arrow half a metre low, into the ground before a
            // low target.
            ai.LookTowards((aim.Value - from).normalized);
            if (dist <= RangedMax + 10f && Time.time >= _nextAttack && !ai.Dodge.HoldAttack && ai.IsLookingAt(target.GetCenterPoint(), 15f))
            {
                // Player bows only reach full power when drawn; NPCs never hold the button, so draw them fully. The draw
                // is read only as the shot starts: clear it straight after, because while it's set the game counts the
                // archer as drawing a bow, and an archer drawing a bow can't run (she jogged behind her owner for good).
                me.m_attackDrawTime = 10f;
                bool shot = ai.Attack(target);
                me.m_attackDrawTime = 0f;
                if (shot)
                {
                    float draw = me.GetCurrentWeapon()?.m_shared.m_attack.m_drawDurationMin ?? 0f;
                    Attacked(Mathf.Max(Config.VfhConfig.RangedAttackCooldown.Value, draw + 0.8f) * ai.Hireling.Enh.Effects.AttackIntervalMult);
                }
            }
        }

        /// <summary>
        /// A wizard: the staff's main spell from CastMin-CastMax, one every WizardAttackCooldown. Fragile up close, so it
        /// steps back from anything inside CastBackOff instead of fighting hand to hand (no sidearm, no shield). Casting
        /// costs it nothing: vanilla only keeps an eitr bar for players.
        /// </summary>
        private void Cast(HirelingAI ai, Humanoid me, Character target, float dist, float dt)
        {
            bool posted = ai.Hireling.HasPost && ai.Hireling.Mode == HirelingMode.Working;
            if (!posted && dist < CastBackOff)
            {
                if (_backingOffSince < 0f)
                    _backingOffSince = Time.time;
                if (Time.time - _backingOffSince < MaxBackOffSeconds)
                {
                    Vector3 away = (ai.transform.position - target.transform.position).normalized;
                    ai.WalkTo(dt, ai.transform.position + away * (CastMin - dist + 2f), 1f, run: true);
                    return;
                }
                // Couldn't shake it: stand and cast.
            }
            else
                _backingOffSince = -1f;
            float flat = Utils.DistanceXZ(target.transform.position, ai.transform.position) - target.GetRadius();
            if (!posted && flat > CastMax)
            {
                if (WaterRules.Out(target) && !me.InWater())
                {
                    BlockedShot(ai, target, "combat.target_in_water");
                    if (Time.time - _blockedSince > BlockedGiveUpSeconds)
                    {
                        Ignore(ai, target, "in the water", BlockedIgnoreSeconds);
                        Drop(ai, "in the water");
                    }
                    else
                        ai.Halt();
                    return;
                }
                ai.WalkTo(dt, target.transform.position, CastMax * 0.8f, run: true);
                return;
            }
            // Spells are projectiles too: the same clear-line check and drop as an arrow, plus leading a moving target.
            Vector3? aim = ArrowAim(me, target, out Vector3 from, lead: true);
            if (aim == null)
            {
                BlockedShot(ai, target);
                if (posted)
                {
                    ai.Halt();
                    ai.Face(target.GetCenterPoint());
                }
                else if (NoWayToAShot(ai, target))
                    return;
                else
                {
                    ai.WalkTo(dt, target.transform.position, CastBackOff + 1f, run: true);
                }
                return;
            }
            _blockedSince = -1f;
            ai.Halt();
            ai.LookTowards((aim.Value - from).normalized);
            if (dist <= CastMax + 10f && Time.time >= _nextAttack && !ai.Dodge.HoldAttack && ai.IsLookingAt(aim.Value, 15f) &&
                ai.Attack(target))
            {
                LastCastDistance = dist;
                LongestCastDistance = Mathf.Max(LongestCastDistance, dist);
                Attacked(Mathf.Max(0.5f, Config.VfhConfig.WizardAttackCooldown.Value) * ai.Hireling.Enh.Effects.AttackIntervalMult);
            }
        }

        /// <summary>
        /// Whether a melee swing can reach the target: a line from the hireling's middle or eyes to the target's middle or
        /// head that no wall, floor or ground is in the way of.
        /// </summary>
        private static bool ClearSwing(Humanoid me, Character target)
        {
            Vector3 head = target.m_head != null ? target.GetHeadPoint() : target.m_eye != null ? target.m_eye.position : target.GetCenterPoint();
            Vector3 eye = me.m_eye != null ? me.m_eye.position : me.GetCenterPoint();
            foreach (Vector3 from in new[] { me.GetCenterPoint(), eye })
                foreach (Vector3 to in new[] { target.GetCenterPoint(), head })
                    if (!Physics.Linecast(from, to, ShotBlockMask))
                        return true;
            return false;
        }

        // Thorvald, in a burial chamber, walked at a skeleton behind a wall and stayed there (123 blocked shots, never
        // following his owner out): a free archer or wizard that hasn't had a clear shot for a while, and isn't getting
        // any closer, leaves that target alone for a bit.
        private const float NoShotGiveUpSeconds = 10f;

        private bool NoWayToAShot(HirelingAI ai, Character target)
        {
            if (Time.time - _blockedSince < NoShotGiveUpSeconds && ai.StuckSeconds(target.transform.position) < BlockedGiveUpSeconds)
                return false;
            Ignore(ai, target, "no clear shot", BlockedIgnoreSeconds);
            Drop(ai, "no clear shot");
            return true;
        }

        private void BlockedShot(HirelingAI ai, Character target, string evt = "combat.shot_blocked")
        {
            if (_blockedSince >= 0f)
                return;
            _blockedSince = Time.time;
            VfhLog.D(LogCat.Combat, evt, ("hid", ai.Hireling.Hid), ("target", target.m_name), ("blocker", _lastBlocker),
                ("dist", Vector3.Distance(target.transform.position, ai.transform.position)));
        }

        /// <summary>
        /// Where to aim so the arrow reaches the target: its middle (or else its head) raised for the arrow's drop,
        /// provided the line from the bow to that point isn't blocked by terrain or buildings. Null when no shot is clear.
        /// </summary>
        private static Vector3? ArrowAim(Humanoid me, Character target, out Vector3 from, bool lead = false)
        {
            ItemDrop.ItemData? bow = me.GetCurrentWeapon();
            Attack? attack = bow?.m_shared.m_attack;
            from = me.m_eye.position;
            if (attack == null)
                return target.GetCenterPoint();
            Transform origin = me.transform;
            if (attack.m_attackOriginJoint.Length > 0 && Utils.FindChild(me.GetVisual().transform, attack.m_attackOriginJoint) is Transform joint)
                origin = joint;
            // Where the arrow leaves once it has turned to the target (it turns before it shoots): from its current facing, an
            // archer with its back or side to a crypt wall put the bow inside the wall and found every shot blocked.
            Transform t = me.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(target.GetCenterPoint() - origin.position, Vector3.up);
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : t.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            from = origin.position + Vector3.up * attack.m_attackHeight + fwd * attack.m_attackRange + right * attack.m_attackOffset;

            float speed = attack.m_projectileVel;
            GameObject? projectile = attack.m_attackProjectile;
            ItemDrop.ItemData? ammo = me.GetAmmoItem();
            if (ammo != null && ammo.m_shared.m_attack.m_attackProjectile != null)
            {
                projectile = ammo.m_shared.m_attack.m_attackProjectile;
                speed += ammo.m_shared.m_attack.m_projectileVel;
            }
            float gravity = projectile != null && projectile.GetComponent<Projectile>() is Projectile p ? p.m_gravity : 0f;

            // Not every creature has a head bone (greylings don't): use its eyes, or just its middle.
            Vector3 head = target.m_head != null ? target.GetHeadPoint() : target.m_eye != null ? target.m_eye.position : target.GetCenterPoint();
            foreach (Vector3 point in new[] { target.GetCenterPoint(), head })
            {
                Vector3 line = point - from;
                if (Physics.SphereCast(from, ShotRadius, line.normalized, out RaycastHit blocker, Mathf.Max(0f, line.magnitude - 0.3f), ShotBlockMask))
                {
                    _lastBlocker = $"{blocker.collider.name}@{blocker.distance:0.0}m layer={LayerMask.LayerToName(blocker.collider.gameObject.layer)} from={from}";
                    continue;
                }
                float flight = speed > 1f ? line.magnitude / speed : 0f;
                // Slow spells (a fireball) miss a creature on the move: aim where it will be when the spell gets there.
                Vector3 ahead = lead ? Vector3.ProjectOnPlane(target.GetVelocity(), Vector3.up) * flight : Vector3.zero;
                Vector3 want = point + ahead + Vector3.up * (0.5f * gravity * flight * flight) - from;
                // Most staffs launch tilted (launch angle -5 = 5 degrees up): vanilla turns the look direction by it, so turn
                // the aim the other way first or the spell sails over the target.
                if (attack.m_launchAngle != 0f && want.sqrMagnitude > 0.01f)
                    want = Quaternion.AngleAxis(-attack.m_launchAngle, Vector3.Cross(Vector3.up, want.normalized)) * want;
                return from + want;
            }
            return null;
        }

        private Character? Choose(HirelingAI ai)
        {
            // A stone order to attack something wins over the stance (guards only get such orders).
            if (ai.Order is { Kind: Followers.FieldOrder.OrderKind.Attack } order && !order.Expired && order.Enemy != null &&
                Vector3.Distance(order.Enemy.transform.position, ai.LeashCenter) <= ai.LeashRadius + StanceRules.LeashBeyondRadius(Stance.Aggressive))
                return order.Enemy;
            ThreatScanner scan = ai.Threats;
            Vector3 home = ai.LeashCenter;
            float radius = ai.LeashRadius;
            Character? threat = scan.Nearest;
            bool inRadius = threat != null && Vector3.Distance(threat.transform.position, home) <= radius;
            bool inExtended = threat != null && Vector3.Distance(threat.transform.position, home) <= radius + StanceRules.AggressiveExtraRange;
            Character? allyAttacker = AllyHits.RecentAttackerNear(ai.transform.position, 15f)
                ?? (ai.Hireling.Job.IsGuard() ? AllyHits.RecentPlayerTargetNear(ai.transform.position, PlayerAssistRange) : null);
            CombatAction action = StanceRules.Decide(ai.Stance, threat != null ? scan.NearestDistance : null, inRadius, inExtended,
                scan.RecentlyAttacked, allyAttacker != null);
            if (action != CombatAction.Engage)
                return null;
            if (scan.RecentlyAttacked)
                return scan.LastAttacker;
            return threat ?? allyAttacker;
        }

        private bool Leashed(HirelingAI ai, Character target)
        {
            float limit = ai.LeashRadius + StanceRules.LeashBeyondRadius(ai.Stance);
            return Vector3.Distance(target.transform.position, ai.LeashCenter) > limit;
        }

        private bool Drop(HirelingAI ai, string reason)
        {
            if (_target != null)
            {
                VfhLog.D(LogCat.Combat, "combat.disengage", ("hid", ai.Hireling.Hid), ("target", _target.m_name), ("reason", reason));
                Telemetry.BalanceFights.End(ai.Hireling, reason);
                if ((reason == "quiet or leashed" || reason == "blocked") && ThreatScanner.Alive(_target) && !(_ignored == _target && Time.time < _ignoredUntil))
                {
                    _ignored = _target;
                    _ignoredUntil = Time.time + (reason == "blocked" ? BlockedIgnoreSeconds : 10f);
                }
            }
            _target = null;
            _blockedSince = -1f;
            _backingOffSince = -1f;
            ai.Hireling.Humanoid.m_blocking = false;
            ai.Block.Reset();
            ai.Hireling.UseSidearm(false);
            return false;
        }
    }
}
