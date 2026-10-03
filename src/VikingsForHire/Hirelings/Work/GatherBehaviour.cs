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
    /// NPC's aim. Follows on to the logs and stumps a felled tree leaves (preferring what's within 8 m).
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
            if (FindPickup(h) || FindTarget(h, _anchor))
            {
                h.NoTargets = false;
                return true;
            }
            if (!h.NoTargets)
            {
                h.NoTargets = true;
                h.SetActivity("$vfh_status_no_targets");
                VfhLog.D(LogCat.Work, "work.no_targets", ("hid", h.Hid), ("job", h.Job), ("radius", h.Radius));
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
                    FindTarget(h, _anchor);
                return;
            }

            h.SetActivity(_profile.Status);
            Vector3 at = _target.transform.position;
            float stand = _profile.StandOff(_target);
            if (Utils.DistanceXZ(ai.transform.position, at) > stand + 0.6f)
            {
                if (Time.time - _approachStarted > ApproachGiveUp)
                {
                    VfhLog.D(LogCat.Work, "work.unreachable", ("hid", h.Hid), ("target", _target.name));
                    Reservations.Skip(_target, UnreachableSkip);
                    Reservations.Release(_target, h.Hid);
                    _target = null;
                    return;
                }
                ai.WalkTo(dt, at, stand, run: false);
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
                m_dir = (target.transform.position - h.transform.position).normalized,
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

        private bool FindTarget(Hireling h, Vector3 near)
        {
            Vector3 me = h.transform.position;
            bool haveAnchor = near != Vector3.zero;
            Component? best = null;
            float bestScore = float.MaxValue;
            foreach (Component c in _profile.Candidates(h.Home, h.Radius))
            {
                if (Reservations.IsSkipped(c) || Reservations.IsReservedByOther(c, h.Hid) || !_profile.IsValid(c, h, out _))
                    continue;
                float d = Vector3.Distance(me, c.transform.position);
                // Logs and stumps a tree just left come first.
                if (haveAnchor && Vector3.Distance(near, c.transform.position) < 8f)
                    d -= 1000f;
                if (d < bestScore)
                {
                    bestScore = d;
                    best = c;
                }
            }
            if (best == null || !Reservations.TryReserve(best, h.Hid))
                return false;
            _target = best;
            _approachStarted = Time.time;
            VfhLog.D(LogCat.Work, "work.target", ("hid", h.Hid), ("target", best.name), ("dist", Vector3.Distance(me, best.transform.position)));
            return true;
        }

        private bool FindPickup(Hireling h)
        {
            HashSet<string> wanted = _profile.PickupItems;
            Vector3 me = h.transform.position;
            Vector3 anchor = _anchor != Vector3.zero ? _anchor : me;
            ItemDrop? best = null;
            float bestSq = float.MaxValue;
            foreach (ItemDrop d in ItemDrop.s_instances)
            {
                if (d == null || d.m_itemData?.m_dropPrefab == null || !wanted.Contains(d.m_itemData.m_dropPrefab.name) || d.m_itemData.m_customData.ContainsKey(DropPile.Tag))
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
                _pickup = null;
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
            _pickup = null;
            FindPickup(h);
        }
    }
}
