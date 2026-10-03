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
        private const float UnreachableSkip = 300f;
        private const float ApproachGiveUp = 20f;

        private readonly IGatherProfile _profile;
        private Component? _target;
        private Vector3 _anchor;
        private float _nextSwing;
        private float _impactAt = -1f;
        private float _rescanAt;
        private float _approachStarted;
        private ItemDrop? _pickup;
        private Vector3? _fellDir;

        public GatherBehaviour(IGatherProfile profile) => _profile = profile;

        public string Name => "Gather";
        public int Priority => 200;

        public bool Wants(HirelingAI ai)
        {
            Hireling h = ai.Hireling;
            if (h.Mode != HirelingMode.Working || h.CargoFull)
                return false;
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
                VfhLog.I(LogCat.Work, "work.no_targets", ("hid", h.Hid), ("job", h.Job), ("radius", h.Radius), ("home", h.Home), ("skipped", why));
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
            Vector3 at = _target.transform.position;
            float stand = _profile.StandOff(_target);
            // A tree with a planned fall direction is worked from the opposite side, so the hit pushes it that way.
            Vector3 spot = _fellDir is Vector3 fell ? at - fell * stand : at;
            float stop = _fellDir != null ? 0.5f : stand;
            if (Utils.DistanceXZ(ai.transform.position, spot) > stop + 0.6f)
            {
                if (Time.time - _approachStarted > ApproachGiveUp)
                {
                    VfhLog.D(LogCat.Work, "work.unreachable", ("hid", h.Hid), ("target", _target.name));
                    Reservations.Skip(_target, UnreachableSkip);
                    Reservations.Release(_target, h.Hid);
                    _target = null;
                    return;
                }
                ai.WalkTo(dt, spot, stop, run: false);
                return;
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
            Collider? col = target.GetComponentInChildren<Collider>();
            hit.m_point = col != null ? col.ClosestPoint(h.transform.position + Vector3.up) : target.transform.position + Vector3.up;
            hit.SetAttacker(h.Humanoid);
            _anchor = target.transform.position;
            HarvestPatches.Direct = true;
            try
            {
                destructible.Damage(hit);
            }
            finally
            {
                HarvestPatches.Direct = false;
            }
            VfhLog.T(LogCat.Work, "work.strike", ("hid", h.Hid), ("target", target.name), ("chop", hit.m_damage.m_chop), ("pickaxe", hit.m_damage.m_pickaxe), ("tier", hit.m_toolTier));
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
            foreach (Component c in _profile.Candidates(h.Home, h.Radius))
            {
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
                if (d == null || d == exclude || d.m_itemData?.m_dropPrefab == null || !wanted.Contains(d.m_itemData.m_dropPrefab.name) || d.m_itemData.m_customData.ContainsKey(DropPile.Tag))
                    continue;
                if ((d.transform.position - anchor).sqrMagnitude > PickupRadius * PickupRadius || Vector3.Distance(d.transform.position, h.Home) > h.Radius + 5f)
                    continue;
                float sq = (d.transform.position - me).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = d;
                }
            }
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
            if (Vector3.Distance(ai.transform.position, drop.transform.position) > PickupReach)
            {
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
