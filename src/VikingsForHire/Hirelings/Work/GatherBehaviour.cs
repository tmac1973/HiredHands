using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Find something to harvest in the work radius, walk to it, swing on a steady rhythm, and pick up what drops.
    /// The swing is the animation; the damage is applied directly at the moment of impact, so it never depends on an
    /// NPC's aim. Works in the profile's order (a woodcutter clears fallen logs, then stumps, before felling more trees).
    /// </summary>
    internal sealed class GatherBehaviour : IHirelingBehaviour
    {
        private const float SwingSeconds = 1.6f;
        private const float ImpactDelay = 0.35f;
        private const float PickupRadius = 8f;
        private const float PickupReach = 1.6f;
        private const float RescanNoTargets = 30f;
        private const float UnreachableSkip = 120f;
        private const float ApproachGiveUp = 12f; // seconds without getting closer
        private const float ApproachStuckWorkFrom = 3f; // stalled this long within reach: work from where we are
        private const float MaxWorkReach = 3.0f; // about an axe or pickaxe swing
        private const float MaxExtraReach = 30f; // largest IGatherProfile.ExtraReach

        private readonly IGatherProfile _profile;
        private Component? _target;
        private Vector3 _anchor;
        private float _nextSwing;
        private float _impactAt = -1f;
        private float _rescanAt;
        private float _approachStarted;
        private float _approachBest = float.MaxValue;
        private ItemDrop? _pickup;
        private float _pickupStarted;
        private readonly HashSet<ItemDrop> _unreachableDrops = new();
        private const float PickupGiveUp = 8f;
        private const float PickupReachUp = 2.5f; // items on a rock or in a dip still count as in reach
        private Vector3? _fellDir;
        private (Vector3 Center, float Radius) _area;

        /// <summary>A stone order: work this target first (its logs and stump follow from the anchor rule).</summary>
        public void Force(HirelingAI ai, Component target)
        {
            Reservations.Release(_target, ai.Hireling.Hid);
            _pickup = null;
            _target = target;
            _anchor = target.transform.position;
            _profile.Plan(target, out _fellDir, out _);
            _approachStarted = Time.time;
            _approachBest = float.MaxValue;
            _rescanAt = 0f;
            Reservations.TryReserve(target, ai.Hireling.Hid);
        }

        /// <summary>Whether this profile can harvest the target with this hireling's tool.</summary>
        public bool CanHarvest(Component target, Hireling h) =>
            _profile.IsValid(target, h, out _) && _profile.Plan(target, out _, out _);

        public HashSet<string> PickupItems => _profile.PickupItems;
        private Collider? _lastStruck;
        private int _strikesOnIt;

        // A chunk or tree takes a handful of hits; this many on the same collider means our hits aren't landing.
        private const int MaxStrikesOnOnePart = 25;

        public GatherBehaviour(IGatherProfile profile) => _profile = profile;

        public string Name => "Gather";
        public int Priority => 200;

        public bool Wants(HirelingAI ai)
        {
            Hireling h = ai.Hireling;
            if (ai.WorkArea == null || h.CargoFull)
                return false;
            _area = ai.WorkArea.Value;
            if (_pickup != null || (_target != null && _profile.IsValid(_target, h, out _)))
                return true;
            if (Time.time < _rescanAt)
                return false;
            _rescanAt = Time.time + Config.VfhConfig.AiScanIntervalSeconds.Value;
            if (FindPickup(h) || FindTarget(h, _anchor, out string why))
            {
                h.NoTargets = false;
                return true;
            }
            if (!h.NoTargets)
            {
                h.NoTargets = true;
                h.SetActivity("$vfh_status_no_targets");
                VfhLog.I(LogCat.Work, "work.no_targets", ("hid", h.Hid), ("job", h.Job), ("radius", _area.Radius), ("center", _area.Center), ("skipped", why));
            }
            _rescanAt = Time.time + RescanNoTargets;
            return false;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_pickup != null)
            {
                Pickup(ai, h, dt);
                return;
            }
            string reason = "none";
            if (_target == null || !_profile.IsValid(_target, h, out reason))
            {
                if (_target != null)
                    VfhLog.T(LogCat.Work, "work.target_done", ("hid", h.Hid), ("reason", reason));
                Reservations.Release(_target, h.Hid);
                _target = null;
                if (!FindPickup(h))
                    FindTarget(h, _anchor, out _);
                return;
            }

            h.SetActivity(_profile.Status);
            if (_profile.Aim(_target, ai.transform.position, out Vector3 at) == null)
            {
                VfhLog.T(LogCat.Work, "work.target_done", ("hid", h.Hid), ("reason", "nothing left to hit"));
                Reservations.Release(_target, h.Hid);
                _target = null;
                return;
            }
            float stand = _profile.StandOff(_target);
            // Where to stand: a tree with a planned fall direction is worked from the opposite side, so the hit pushes
            // it that way. Anything else from just outside the aim point on our side: the aim point itself is on (or
            // in) the rock or trunk, where the pathfinder can't take us.
            Vector3 toMe = ai.transform.position - at;
            toMe.y = 0f;
            Vector3 spot = _fellDir is Vector3 fell ? at - fell * stand
                : toMe.sqrMagnitude > 0.01f ? at + toMe.normalized * stand : at;
            float dist = Utils.DistanceXZ(ai.transform.position, spot);
            // A tree being felled one way must be hit from its spot; anything else may be worked from any side.
            float toAim = Utils.DistanceXZ(ai.transform.position, at);
            bool inPlace = _fellDir != null ? dist <= 1.1f : dist <= 0.8f || toAim <= stand + 0.4f;
            if (!inPlace)
            {
                if (dist < _approachBest - 0.5f)
                {
                    _approachBest = dist;
                    _approachStarted = Time.time; // still getting closer
                }
                // Can't get any closer, but already within a pickaxe's reach of the surface (the rock's own shape or a
                // neighbouring chunk is in the way): work from here rather than give up on the deposit.
                if (Time.time - _approachStarted > ApproachStuckWorkFrom && _fellDir == null && toAim <= MaxWorkReach)
                {
                    inPlace = true;
                    VfhLog.T(LogCat.Work, "work.from_here", ("hid", h.Hid), ("target", _target.name), ("toAim", toAim));
                }
                else if (Time.time - _approachStarted > ApproachGiveUp)
                {
                    VfhLog.D(LogCat.Work, "work.unreachable", ("hid", h.Hid), ("target", _target.name), ("dist", dist), ("toAim", toAim));
                    Reservations.Skip(_target, UnreachableSkip);
                    Reservations.Release(_target, h.Hid);
                    _target = null;
                    return;
                }
                if (!inPlace)
                {
                    ai.WalkTo(dt, spot, 0.5f, run: false);
                    return;
                }
            }

            ai.Halt();
            ai.Face(at + Vector3.up);
            if (_impactAt > 0f && Time.time >= _impactAt)
            {
                _impactAt = -1f;
                Strike(h, _target);
            }
            if (Time.time >= _nextSwing)
            {
                _nextSwing = Time.time + SwingSeconds;
                _impactAt = Time.time + ImpactDelay;
                h.Humanoid.StartAttack(null, false);
            }
        }

        private void Strike(Hireling h, Component target)
        {
            if (target is not IDestructible destructible)
            {
                destructible = target.GetComponent<IDestructible>();
                if (destructible == null)
                    return;
            }
            var hit = new HitData
            {
                m_damage = _profile.SwingDamage(h.ToolDamage, h.LevelData.GatherMult),
                m_toolTier = (short)h.ToolTier,
                m_dir = _fellDir ?? Flat(target.transform.position - h.transform.position).normalized,
            };
            // Bounds, not Collider.ClosestPoint: that only works on convex colliders and warns on rock meshes.
            Collider? col = _profile.Aim(target, h.transform.position, out Vector3 aim);
            if (col == null)
            {
                VfhLog.D(LogCat.Work, "work.strike_nothing", ("hid", h.Hid), ("target", target.name));
                return;
            }
            _strikesOnIt = col == _lastStruck ? _strikesOnIt + 1 : 1;
            _lastStruck = col;
            if (_strikesOnIt > MaxStrikesOnOnePart)
            {
                VfhLog.I(LogCat.Work, "work.hits_not_landing", ("hid", h.Hid), ("target", target.name), ("part", col.name),
                    ("strikes", _strikesOnIt), ("pos", target.transform.position), ("skipFor", UnreachableSkip));
                Reservations.Skip(target, UnreachableSkip);
                Reservations.Release(target, h.Hid);
                _target = null;
                _lastStruck = null;
                return;
            }
            hit.m_hitCollider = col;
            hit.m_point = aim; // the profile's aim point: the surface for rocks, the trunk for trees
            hit.SetAttacker(h.Humanoid);
            _anchor = hit.m_point; // where the drops land (a rock's chunk, not its centre)
            HarvestPatches.Direct = true;
            try
            {
                destructible.Damage(hit);
            }
            finally
            {
                HarvestPatches.Direct = false;
            }
            VfhLog.T(LogCat.Work, "work.strike", ("hid", h.Hid), ("target", target.name), ("part", col.name), ("n", _strikesOnIt),
                ("chop", hit.m_damage.m_chop), ("pickaxe", hit.m_damage.m_pickaxe), ("tier", hit.m_toolTier));
        }

        private bool FindTarget(Hireling h, Vector3 near, out string why)
        {
            var reasons = new Dictionary<string, int>();
            int candidates = 0;
            string? example = null;
            Vector3 me = h.transform.position;
            bool haveAnchor = near != Vector3.zero;
            Component? best = null;
            float bestScore = float.MaxValue;
            foreach (Component c in _profile.Candidates(_area.Center, _area.Radius + MaxExtraReach))
            {
                if (Utils.DistanceXZ(c.transform.position, _area.Center) > _area.Radius + _profile.ExtraReach(c))
                    continue;
                candidates++;
                string? skip = Reservations.IsSkipped(c) ? "skipped after a failed approach"
                    : Reservations.IsReservedByOther(c, h.Hid) ? "claimed by another hireling"
                    : !_profile.IsValid(c, h, out string reason) ? reason
                    : !_profile.Plan(c, out _, out string unsafeReason) ? unsafeReason : null;
                if (skip != null)
                {
                    // One line per distinct reason, keeping the first object's name as an example.
                    string key = skip.StartsWith("near buildings") ? "near buildings" : skip;
                    if (!reasons.ContainsKey(key))
                        VfhLog.D(LogCat.Work, "work.candidate_skipped", ("hid", h.Hid), ("target", c.name), ("reason", skip));
                    reasons[key] = reasons.TryGetValue(key, out int k) ? k + 1 : 1;
                    if (key != skip)
                        example ??= skip;
                    continue;
                }
                // By the profile's work order (a woodcutter clears logs and stumps before felling more), then
                // what a felled tree just left behind, then nearest.
                float d = _profile.Rank(c) * 10000f + Vector3.Distance(me, c.transform.position);
                if (haveAnchor && Vector3.Distance(near, c.transform.position) < 8f)
                    d -= 1000f;
                if (d < bestScore)
                {
                    bestScore = d;
                    best = c;
                }
            }
            why = $"candidates={candidates} " + string.Join(", ", reasons.Select(r => $"{r.Key} x{r.Value}")) + (example != null ? $" (e.g. {example})" : "");
            if (best == null || !Reservations.TryReserve(best, h.Hid))
                return false;
            _target = best;
            _profile.Plan(best, out _fellDir, out _);
            _approachStarted = Time.time;
            _approachBest = float.MaxValue;
            VfhLog.D(LogCat.Work, "work.target", ("hid", h.Hid), ("target", best.name), ("dist", Vector3.Distance(me, best.transform.position)),
                ("fellDir", _fellDir?.ToString() ?? "any"));
            return true;
        }

        private bool FindPickup(Hireling h, ItemDrop? exclude = null)
        {
            HashSet<string> wanted = _profile.PickupItems;
            Vector3 me = h.transform.position;
            Vector3 anchor = _anchor != Vector3.zero ? _anchor : me;
            ItemDrop? best = null;
            float bestSq = float.MaxValue;
            foreach (ItemDrop d in ItemDrop.s_instances)
            {
                if (d == null || d == exclude || _unreachableDrops.Contains(d) || d.m_itemData?.m_dropPrefab == null || !wanted.Contains(d.m_itemData.m_dropPrefab.name) || d.m_itemData.m_customData.ContainsKey(DropPile.Tag))
                    continue;
                if ((d.transform.position - anchor).sqrMagnitude > PickupRadius * PickupRadius || Vector3.Distance(d.transform.position, _area.Center) > _area.Radius + MaxExtraReach + 5f)
                    continue;
                float sq = (d.transform.position - me).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = d;
                }
            }
            if (best != _pickup)
                _pickupStarted = Time.time;
            _pickup = best;
            return best != null;
        }

        private void Pickup(HirelingAI ai, Hireling h, float dt)
        {
            ItemDrop? drop = _pickup;
            if (drop == null || drop.m_nview == null || !drop.m_nview.IsValid())
            {
                // Gone (someone else took it): go straight for the next one rather than idling until the next scan.
                if (!FindPickup(h, drop))
                    FindTarget(h, _anchor, out _);
                return;
            }
            bool inReach = Utils.DistanceXZ(ai.transform.position, drop.transform.position) <= PickupReach &&
                           Mathf.Abs(drop.transform.position.y - ai.transform.position.y) <= PickupReachUp;
            if (!inReach)
            {
                if (Time.time - _pickupStarted > PickupGiveUp)
                {
                    // Inside the rock, under the ground or across water: leave it rather than stand there forever.
                    VfhLog.D(LogCat.Work, "work.pickup_unreachable", ("hid", h.Hid), ("item", drop.m_itemData.m_dropPrefab.name),
                        ("pos", drop.transform.position), ("me", ai.transform.position));
                    _unreachableDrops.RemoveWhere(d => d == null);
                    _unreachableDrops.Add(drop);
                    _pickup = null;
                    if (!FindPickup(h))
                        FindTarget(h, _anchor, out _);
                    return;
                }
                ai.WalkTo(dt, drop.transform.position, PickupReach * 0.6f, run: false);
                return;
            }
            if (!drop.m_nview.IsOwner())
                drop.m_nview.ClaimOwnership();
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            if (h.CargoInventory != null && h.CargoInventory.AddItem(item))
            {
                VfhLog.T(LogCat.Work, "work.pickup", ("hid", h.Hid), ("item", GearApplier.Name(item)), ("n", item.m_stack));
                h.OnPickedUp();
                ZNetScene.instance.Destroy(drop.gameObject);
            }
            // The drop is only destroyed at the end of the frame, so it's still listed: skip it explicitly, or the
            // next search picks it again, finds it gone next tick, and the hireling idles until the next scan.
            if (!FindPickup(h, drop))
                FindTarget(h, _anchor, out _);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
