using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// A recruited hireling with its owner. Follow: keep within a few metres, sprinting when the owner sprints or it's
    /// behind, faster still when well behind, and unsticking or teleporting out of sight if it can't keep up (FollowCatchUp).
    /// Stay: hold the stored position, walking back if pushed off it. (Gather Nearby comes with field orders.) Combat
    /// and fleeing still take over by priority; their leash is the owner (or the stay spot) while following.
    /// </summary>
    internal sealed class FollowBehaviour : IHirelingBehaviour
    {
        private const float FollowDistance = 3f;
        private const float RunBeyond = 10f;
        private const float FollowRunBeyond = 5f;
        private const float SneakWithin = 15f;
        private const float StaySlack = 3f;

        private string _shown = "";
        // Hold spot handling: walk all the way to a new spot (height included, so it climbs the last stairs), and only
        // once there use the slack, so a nudge in a fight doesn't send it shuffling back and forth.
        private Vector3 _holdSpot = new(float.NaN, 0f, 0f);
        private bool _arrived;
        private float _bestHoldDist = float.MaxValue;
        private float _holdProgressAt;
        private const float ArriveDistance = 0.6f;
        private const float HoldStuckSeconds = 8f;
        private bool _toldFull;
        private readonly FollowCatchUp _catchUp = new();
        private bool _retreating;

        public string Name => "Follow";
        public int Priority => 400;

        public bool Wants(HirelingAI ai) => ai.Hireling.Mode == HirelingMode.Following;

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (ai.Order != null && ai.Order.Expired)
            {
                VfhLog.D(LogCat.Orders, "order.done", ("hid", h.Hid), ("kind", ai.Order.Kind));
                ai.Order = null;
            }

            bool retreat = ai.RetreatOrdered;
            if (retreat != _retreating)
            {
                _retreating = retreat;
                VfhLog.D(LogCat.Orders, retreat ? "order.retreat_start" : "order.retreat_end", ("hid", h.Hid), ("why", retreat ? "ordered" : "quiet"));
            }

            // Gatherers with a harvest order or parked in Gather Here work while there's something to work; the shared
            // gather behaviour does the chopping and mining, inside the follower's work area.
            if (!retreat && ai.Gather != null && ai.WorkArea != null)
            {
                if (h.CargoFull)
                {
                    TellFull(h);
                }
                else
                {
                    _toldFull = false;
                    if (ai.Gather.Wants(ai))
                    {
                        Show(h, h.Job == JobType.Miner ? "$vfh_status_mining" : "$vfh_status_chopping");
                        ai.Gather.Tick(ai, dt);
                        return;
                    }
                }
                if (ai.Order is { Kind: FieldOrder.OrderKind.Harvest })
                {
                    VfhLog.D(LogCat.Orders, "order.done", ("hid", h.Hid), ("kind", "harvest"), ("why", h.CargoFull ? "cargo full" : "nothing left"));
                    ai.Order = null;
                }
            }

            if (h.FollowMode != FollowMode.Follow && !retreat)
            {
                Show(h, h.CargoFull && h.FollowMode == FollowMode.GatherHere ? "$vfh_status_cargo_full" : "$vfh_status_staying");
                Hold(ai, h.StayPos, dt);
                return;
            }

            Player? owner = Player.GetPlayer(h.OwnerId);
            if (owner == null || owner.IsDead())
            {
                // Not loaded here (or dead): wait where we are. Phase 15 sends long-lost followers home.
                Show(h, "$vfh_status_waiting");
                ai.Halt();
                return;
            }
            Show(h, retreat ? "$vfh_status_retreating" : "$vfh_status_following");
            float dist = OwnerDistance(ai, owner.transform.position);
            // Sneak with the owner when close; a follower well behind keeps running to catch up.
            bool sneak = owner.IsCrouching() && !retreat && dist <= SneakWithin;
            ai.WantSneak = sneak;
            if (_catchUp.Tick(ai, owner, dist, dt))
                return;
            if (dist > FollowDistance)
                ai.Chase(dt, owner.transform.position, FollowDistance * 0.8f, run: !sneak && (retreat || owner.IsRunning() || dist > FollowRunBeyond));
            else
            {
                ai.Halt();
                ai.Face(owner.GetHeadPoint());
            }
        }

        // Along the ground, unless far apart in height: a dungeon's inside is 5000 m above its entrance, and a follower
        // left in there measured along the ground thought it was right next to its owner outside.
        // At home, on another floor counts as far too (upstairs right above it isn't "with" its owner).
        // Only while there's a way up through the links: otherwise it would grind underneath its owner forever.
        private static float OwnerDistance(HirelingAI ai, Vector3 owner)
        {
            Vector3 me = ai.transform.position;
            if (Mathf.Abs(me.y - owner.y) > 10f)
                return Vector3.Distance(me, owner);
            if (Mathf.Abs(me.y - owner.y) > 1.5f && Hirelings.Nav.NavLinkRegistry.Enabled && ai.Links.Active(owner))
                return Vector3.Distance(me, owner) + FollowDistance;
            return Utils.DistanceXZ(me, owner);
        }

        private void Hold(HirelingAI ai, Vector3 spot, float dt)
        {
            if ((spot - _holdSpot).sqrMagnitude > 0.01f)
            {
                _holdSpot = spot;
                _arrived = false;
                _bestHoldDist = float.MaxValue;
                _holdProgressAt = Time.time;
            }
            float dist = Vector3.Distance(ai.transform.position, spot);
            if (!_arrived)
            {
                if (dist <= ArriveDistance)
                {
                    _arrived = true;
                }
                else
                {
                    if (dist < _bestHoldDist - 0.2f)
                    {
                        _bestHoldDist = dist;
                        _holdProgressAt = Time.time;
                    }
                    if (Time.time - _holdProgressAt > HoldStuckSeconds)
                    {
                        _arrived = true; // as close as it can get: hold here
                        VfhLog.D(LogCat.Follow, "follow.hold_short", ("hid", ai.Hireling.Hid), ("missedBy", dist));
                    }
                    else
                    {
                        ai.WalkTo(dt, spot, ArriveDistance * 0.5f, run: dist > RunBeyond);
                        return;
                    }
                }
            }
            if (Utils.DistanceXZ(ai.transform.position, spot) > StaySlack)
            {
                _arrived = false; // pushed off it: walk back all the way
                _holdProgressAt = Time.time;
                _bestHoldDist = float.MaxValue;
                return;
            }
            ai.Halt();
        }

        // Tell the owner once when a parked gatherer fills up.
        private void TellFull(Hireling h)
        {
            if (_toldFull)
                return;
            _toldFull = true;
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == h.OwnerId)
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$vfh_follow_cargo_full", h.DisplayName));
            VfhLog.I(LogCat.Follow, "follow.cargo_full", ("hid", h.Hid));
        }

        private void Show(Hireling h, string token)
        {
            if (token == _shown)
                return;
            _shown = token;
            h.SetActivity(token);
            VfhLog.D(LogCat.Follow, "follow.state", ("hid", h.Hid), ("state", token));
        }
    }
}
