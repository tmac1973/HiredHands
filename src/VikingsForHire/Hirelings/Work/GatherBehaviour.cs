using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
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
        private const float MaxExtraReach = 30f; // largest IGatherProfile.ExtraReach

        private readonly IGatherProfile _profile;

        public IGatherProfile Profile => _profile;

        /// <summary>What it's working on now (tests).</summary>
        public Component? Target => _target;
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

        /// <summary>Enhancements' swing power (Strong swing, the masters; target-dependent ones are added by their phase).</summary>
        public static float GatherEnhancement(Hireling h) => h.Enh.Effects.GatherMult;

        /// <summary>Clean cut (logs and stumps) and Rock breaker (rocks with no ore): a harder swing on those.</summary>
        public static float TargetGather(Hireling h, Component target)
        {
            if (!h.Enh.Effects.AnyPicks)
                return 1f;
            if (h.Job == JobType.Woodcutter && target is TreeLog or Destructible)
                return 1f + h.Enh.Value(Core.Enhancements.EnhancementId.CleanCut, "gather");
            if (h.Job == JobType.Miner && h.Ai.Gather?.Profile.Rank(target) == 1)
                return 1f + h.Enh.Value(Core.Enhancements.EnhancementId.RockBreaker, "gather");
            return 1f;
        }

        private static bool Felled(TreeBase tree) =>
            tree == null || tree.m_nview == null || !tree.m_nview.IsValid() || tree.m_nview.GetZDO().GetFloat(ZDOVars.s_health, tree.m_health) <= 0f;

        /// <summary>Timber! A felled tree takes down the nearest small tree it falls onto.</summary>
        private static void Timber(Hireling h, TreeBase felled, Vector3 dir)
        {
            if (!h.Enh.Has(Core.Enhancements.EnhancementId.Timber))
                return;
            float range = h.Enh.Value(Core.Enhancements.EnhancementId.Timber, "range");
            float maxHealth = h.Enh.Value(Core.Enhancements.EnhancementId.Timber, "maxHealth");
            Vector3 from = felled.transform.position;
            // A small tree: a tree with little health left, or a sapling-sized one (the game's small trees are destructibles).
            Component? best = null;
            float bestD = float.MaxValue;
            foreach (Collider c in Physics.OverlapSphere(from, range + 1f))
            {
                Component? t = c.GetComponentInParent<TreeBase>() is TreeBase tb && tb != felled ? tb
                    : c.GetComponentInParent<Destructible>() is Destructible ds && ds.m_destructibleType == DestructibleType.Tree ? ds : null;
                ZNetView? nv = t?.GetComponent<ZNetView>();
                if (t == null || nv == null || !nv.IsValid())
                    continue;
                Vector3 to = t.transform.position - from;
                to.y = 0f;
                float d = to.magnitude;
                if (d > range || d < 0.3f || Vector3.Angle(Flat(dir), to) > 75f)
                    continue;
                float baseHealth = t is TreeBase b0 ? b0.m_health : ((Destructible)t).m_health;
                float health = nv.GetZDO().GetFloat(ZDOVars.s_health, baseHealth);
                if (health > maxHealth || d >= bestD)
                    continue;
                best = t;
                bestD = d;
            }
            if (best == null)
                return;
            var hit = new HitData { m_toolTier = 100, m_dir = Flat(dir), m_point = best.transform.position + Vector3.up };
            hit.m_damage.m_chop = maxHealth * 10f;
            hit.SetAttacker(h.Humanoid);
            HarvestPatches.DirectTarget = best;
            ((IDestructible)best).Damage(hit);
            EnhancementEvents.Fire(h, "Timber", Core.Diagnostics.LogCat.Work, ("tree", best.name), ("dist", bestD));
        }
        private Collider? _lastStruck;
        private int _strikesOnIt;

        // A chunk or tree takes a handful of hits; this many on the same collider means our hits aren't landing.
        private const int MaxStrikesOnOnePart = 25;

        public GatherBehaviour(IGatherProfile profile) => _profile = profile;

        // What its last look round found in the way, for its Problems list (activity tokens, one per line).
        private string _problems = "";

        // The profiles' reasons (made for the log) as Problems lines: grouped, the details left out.
        private static string ReasonToken(string reason) =>
            reason == "gone" ? ""
            : reason.StartsWith("tier ") ? "$vfh_gwhy_tier"
            : reason.StartsWith("near buildings") ? "$vfh_gwhy_buildings"
            : reason.StartsWith("only buried") ? "$vfh_gwhy_buried"
            : reason.StartsWith("buried and undiscovered") ? "$vfh_gwhy_undiscovered"
            : "$vfh_gwhy_other";

        private Collider? _aimPart;

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
                h.SetWhy("");
                return true;
            }
            h.SetWhy(_problems);
            if (!h.NoTargets)
            {
                h.NoTargets = true;
                h.SetActivity(_fullItems.Count > 0
                    ? Chores.ActivityText.Make("$vfh_status_storage_full", string.Join(", ", _fullItems.Select(Chores.WorkSteps.SharedName)))
                    : "$vfh_status_no_targets");
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
            _aimPart = _profile.Aim(_target, ai.transform.position, out Vector3 at);
            if (_aimPart == null)
            {
                VfhLog.T(LogCat.Work, "work.target_done", ("hid", h.Hid), ("reason", "nothing left to hit"));
                Reservations.Release(_target, h.Hid);
                _target = null;
                return;
            }
            // A follower in Gather Here works around its spot: a big rock is picked by its centre,
            // but a chunk of it well outside the circle is out of bounds (a miner set by a rock swam to one 30 m off).
            if (h.Mode == HirelingMode.Following && h.FollowMode == FollowMode.GatherHere && h.Ai.Order == null &&
                Utils.DistanceXZ(at, _area.Center) > _area.Radius + _profile.WorkReach)
            {
                VfhLog.D(LogCat.Work, "work.part_outside", ("hid", h.Hid), ("target", _target.name), ("part", _aimPart.name),
                    ("fromCenter", Utils.DistanceXZ(at, _area.Center)), ("radius", _area.Radius));
                if (!_profile.GiveUpOnPart(_target, _aimPart))
                {
                    Reservations.Skip(_target, UnreachableSkip);
                    Reservations.Release(_target, h.Hid);
                    _target = null;
                }
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
            // Swimming to get there (a rock off the shore, across a cove): not worth it; something on land is.
            if (!inPlace && h.Humanoid.IsSwimming())
            {
                VfhLog.D(LogCat.Work, "work.needs_swim", ("hid", h.Hid), ("target", _target.name), ("dist", dist));
                Reservations.Skip(_target, UnreachableSkip);
                Reservations.Release(_target, h.Hid);
                _target = null;
                return;
            }
            if (!inPlace)
            {
                if (dist < _approachBest - 0.5f)
                {
                    _approachBest = dist;
                    _approachStarted = Time.time; // still getting closer
                }
                // Can't get any closer, but already within a pickaxe's reach of the surface (the rock's own shape or a
                // neighbouring chunk is in the way): work from here rather than give up on the deposit.
                if (Time.time - _approachStarted > ApproachStuckWorkFrom && _fellDir == null && toAim <= _profile.WorkReach)
                {
                    inPlace = true;
                    VfhLog.T(LogCat.Work, "work.from_here", ("hid", h.Hid), ("target", _target.name), ("toAim", toAim));
                }
                else if (Time.time - _approachStarted > ApproachGiveUp && _aimPart != null && _profile.GiveUpOnPart(_target, _aimPart))
                {
                    // Just this chunk: the rest of the deposit is still there to work.
                    VfhLog.D(LogCat.Work, "work.part_unreachable", ("hid", h.Hid), ("target", _target.name), ("part", _aimPart.name), ("toAim", toAim));
                    _approachStarted = Time.time;
                    _approachBest = float.MaxValue;
                    return;
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
            // Digging: face the dirt itself so the swing comes down into it; otherwise chest height on the target.
            ai.Face(_aimPart != null && _profile.NeedsDigging(_aimPart) ? at : at + Vector3.up);
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
                m_damage = _profile.SwingDamage(h.ToolDamage, h.LevelData.GatherMult * GatherEnhancement(h) * TargetGather(h, target)),
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
            if (_strikesOnIt > MaxStrikesOnOnePart && _profile.GiveUpOnPart(target, col))
            {
                VfhLog.D(LogCat.Work, "work.part_not_breaking", ("hid", h.Hid), ("target", target.name), ("part", col.name), ("strikes", _strikesOnIt));
                _lastStruck = null;
                _strikesOnIt = 0;
                return;
            }
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
            // Still under the dirt: this swing digs (the pickaxe's own hit on the ground does that, out in the field);
            // the chunk is hit once it's exposed. The strike count above gives up on a chunk that never comes free.
            // Harvesting is loud: vanilla gives the noise of each hit to the nearest player within 10 m, so a hireling
            // working on its own made none and monsters never came. Each strike makes the same noise on the hireling.
            // Quiet work (an enhancement): no noise for monsters to come to.
            if (Config.VfhConfig.GatheringMakesNoise.Value && !h.Enh.Has(Core.Enhancements.EnhancementId.QuietWork))
                h.Humanoid.AddNoise(target switch
            {
                TreeLog log => log.m_hitNoise,
                Destructible d => d.m_hitNoise,
                _ => 100f,
            });
            if (_profile.NeedsDigging(col))
            {
                VfhLog.T(LogCat.Work, "work.dig", ("hid", h.Hid), ("target", target.name), ("part", col.name), ("n", _strikesOnIt), ("at", aim));
                return;
            }
            hit.m_hitCollider = col;
            hit.m_point = aim; // the profile's aim point: the surface for rocks, the trunk for trees
            hit.SetAttacker(h.Humanoid);
            _anchor = hit.m_point; // where the drops land (a rock's chunk, not its centre)
            HarvestPatches.Direct = true;
            HarvestPatches.DirectBy = h;
            HarvestPatches.DirectTarget = target;
            try
            {
                destructible.Damage(hit);
                if (target is TreeBase felled && Felled(felled))
                    Timber(h, felled, hit.m_dir);
            }
            finally
            {
                HarvestPatches.Direct = false;
                HarvestPatches.DirectBy = null;
                HarvestPatches.DirectTarget = null;
            }
            VfhLog.T(LogCat.Work, "work.strike", ("hid", h.Hid), ("target", target.name), ("part", col.name), ("n", _strikesOnIt),
                ("chop", hit.m_damage.m_chop), ("pickaxe", hit.m_damage.m_pickaxe), ("tier", hit.m_toolTier));
        }

        private bool FindTarget(Hireling h, Vector3 near, out string why)
        {
            _fullItems.Clear();
            var reasons = new Dictionary<string, int>();
            // The same, for its Problems list: (activity token, item or reason) -> how many.
            var problems = new Dictionary<(string Token, string Arg), int>();
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
                string? off = null, full = null, reason = "", unsafeReason = "";
                (string? skip, string token, string arg) = h.Ai.Order is { } order && !order.Allows(c) ? ("not part of the order", "$vfh_gwhy_order", "")
                    : h.Ai.Order == null && (off = SwitchedOff(h, c)) != null ? ($"{off} switched off", "$vfh_gwhy_off", off)
                    : (full = NoRoomFor(h, c)) != null ? ($"no room for {full}", "$vfh_gwhy_full", full)
                    : WaterRules.Under(c.transform.position, 1f) ? ("in the water", "$vfh_gwhy_water", "")
                    : Reservations.IsSkipped(c) ? ("skipped after a failed approach", "$vfh_gwhy_unreachable", "")
                    : Reservations.IsReservedByOther(c, h.Hid) ? ("claimed by another hireling", "$vfh_gwhy_claimed", "")
                    : !_profile.IsValid(c, h, out reason) ? (reason, ReasonToken(reason), reason)
                    : !_profile.Plan(c, out _, out unsafeReason) ? (unsafeReason, ReasonToken(unsafeReason), unsafeReason)
                    : ((string?)null, "", "");
                if (skip != null)
                {
                    // The profiles' grouped reasons drop their details; the item stays for the switched-off and full lines.
                    if (token is "$vfh_gwhy_tier" or "$vfh_gwhy_buildings" or "$vfh_gwhy_buried" or "$vfh_gwhy_undiscovered")
                        arg = "";
                    if (token.Length > 0)
                        problems[(token, arg)] = problems.TryGetValue((token, arg), out int pn) ? pn + 1 : 1;
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
            _problems = candidates == 0
                ? Chores.ActivityText.Make(_profile is WoodcutterProfile ? "$vfh_gwhy_no_trees" : "$vfh_gwhy_no_rocks", Mathf.RoundToInt(_area.Radius).ToString())
                : string.Join("\n", problems.OrderByDescending(p => p.Value).Select(p => p.Key.Arg.Length > 0
                    ? Chores.ActivityText.Make(p.Key.Token, p.Value.ToString(), p.Key.Arg)
                    : Chores.ActivityText.Make(p.Key.Token, p.Value.ToString())));
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

        private readonly HashSet<string> _fullItems = new();
        private List<Container>? _homeChests;
        private float _homeChestsAt = -999f;
        private const float HomeChestsSeconds = 10f;

        // Only at home, on its own account: an order, or work out in the field with its owner, isn't paused.
        private bool AtHome(Hireling h) => h.Ai.Order == null && Utils.DistanceXZ(_area.Center, h.Home) < 1f;

        private List<Container> HomeChests(Hireling h)
        {
            if (_homeChests == null || Time.time - _homeChestsAt > HomeChestsSeconds)
            {
                _homeChests = ChestModes.Put(ChestFinder.Find(h.Home, h.Radius)).ToList();
                _homeChestsAt = Time.time;
            }
            return _homeChests;
        }

        // PauseWhenStorageFull: the item this tree or rock counts as, when the chests for it are full.
        private string? NoRoomFor(Hireling h, Component target)
        {
            if (!Config.VfhConfig.PauseWhenStorageFull.Value || !AtHome(h))
                return null;
            IEnumerable<string> yield = _profile.Yield(target);
            string? primary = DataStore.Current.Jobs.TryGetValue(h.Job, out JobData? job) ? GatherRules.Primary(yield, job.GatherToggles) : null;
            primary ??= yield.FirstOrDefault();
            if (primary == null || !StorageRoom.NoRoom(HomeChests(h), primary))
                return null;
            _fullItems.Add(primary);
            return primary;
        }

        // The "what to gather" setting: the item this tree or rock counts as, if it's switched off.
        private string? SwitchedOff(Hireling h, Component target)
        {
            HashSet<string> skip = h.SkipItems;
            if (skip.Count == 0 || !DataStore.Current.Jobs.TryGetValue(h.Job, out JobData? job))
                return null;
            string? primary = GatherRules.Primary(_profile.Yield(target), job.GatherToggles);
            return primary != null && skip.Contains(primary) ? primary : null;
        }

        private bool FindPickup(Hireling h, ItemDrop? exclude = null)
        {
            HashSet<string> wanted = new(_profile.PickupItems);
            wanted.ExceptWith(h.SkipItems); // switched off: left on the ground
            if (Config.VfhConfig.PauseWhenStorageFull.Value && AtHome(h))
                wanted.RemoveWhere(i => StorageRoom.NoRoom(HomeChests(h), i)); // nowhere to put it: left too
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
            // Only as many as the weight limit allows; the rest stays on the ground for the next trip.
            float unit = h.WeightOf(item, 1);
            int fits = unit <= 0f ? item.m_stack : Mathf.Min(item.m_stack, Mathf.FloorToInt(h.CargoWeightRoom / unit));
            if (fits <= 0)
            {
                _pickup = null;
                return;
            }
            if (fits < item.m_stack)
            {
                item.m_stack = fits;
                if (h.CargoInventory != null && h.CargoInventory.AddItem(item))
                {
                    drop.m_itemData.m_stack -= fits;
                    drop.Save();
                    VfhLog.T(LogCat.Work, "work.pickup_part", ("hid", h.Hid), ("item", GearApplier.Name(item)), ("n", fits), ("left", drop.m_itemData.m_stack));
                    h.OnPickedUp();
                }
                _pickup = null;
                return;
            }
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
