using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// A recruited hireling with its owner. Follow: keep within a few metres, running to catch up when far behind.
    /// Stay: hold the stored position, walking back if pushed off it. (Gather Nearby comes with field orders.) Combat
    /// and fleeing still take over by priority; their leash is the owner (or the stay spot) while following.
    /// </summary>
    internal sealed class FollowBehaviour : IHirelingBehaviour
    {
        private const float FollowDistance = 3f;
        private const float RunBeyond = 10f;
        private const float StaySlack = 3f;

        private string _shown = "";
        private bool _toldFull;

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

            // Gatherers with a harvest order or parked in Gather Here work while there's something to work; the shared
            // gather behaviour does the chopping and mining, inside the follower's work area.
            if (ai.Gather != null && ai.WorkArea != null)
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

            if (h.FollowMode != FollowMode.Follow)
            {
                Show(h, h.CargoFull && h.FollowMode == FollowMode.GatherHere ? "$vfh_status_cargo_full" : "$vfh_status_staying");
                Vector3 spot = h.StayPos;
                if (Utils.DistanceXZ(ai.transform.position, spot) > StaySlack)
                    ai.WalkTo(dt, spot, 1f, run: false);
                else
                    ai.Halt();
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
            Show(h, "$vfh_status_following");
            float dist = Utils.DistanceXZ(ai.transform.position, owner.transform.position);
            if (dist > FollowDistance)
                ai.WalkTo(dt, owner.transform.position, FollowDistance * 0.8f, run: dist > RunBeyond);
            else
            {
                ai.Halt();
                ai.Face(owner.GetHeadPoint());
            }
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
