using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Called to the board from the Roster tab ("Call to board"): drop the work, come and stand beside the board until
    /// sent back to work, so a hireling you can't find comes to you. Walks there; with no route, or no progress for a
    /// while, it's moved there (it's your hireling, at your base). Combat and fleeing still take over.
    /// </summary>
    internal sealed class ParkBehaviour : IHirelingBehaviour
    {
        private const float AtSpot = 0.8f;
        private const float GiveUpSeconds = 20f;
        private const float NoRouteSeconds = 5f;

        private Vector3? _spot;
        private float _noRouteSince = -1f;
        private bool _arrivedLogged;

        public string Name => "Park";
        public int Priority => 350; // above work and delivering, below following, combat and leaving

        public bool Wants(HirelingAI ai)
        {
            bool want = ai.Hireling.Mode == HirelingMode.Working && ai.Hireling.IsParked;
            if (!want)
            {
                _spot = null;
                _arrivedLogged = false;
            }
            return want;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            HiringBoard? board = HiringBoard.Loaded.FirstOrDefault(b => b != null && b.Id == h.BoardId);
            Vector3 boardPos = board != null ? board.transform.position : h.Home;
            _spot ??= Spot(ai, board, boardPos);
            Vector3 spot = _spot.Value;

            if (Vector3.Distance(ai.transform.position, spot) <= AtSpot + 0.4f)
            {
                if (!_arrivedLogged)
                {
                    _arrivedLogged = true;
                    VfhLog.I(LogCat.AI, "park.arrived", ("hid", h.Hid), ("spot", spot));
                }
                h.SetActivity("$vfh_status_parked");
                ai.Halt();
                Player near = Player.GetClosestPlayer(ai.transform.position, 8f);
                ai.Face(near != null ? near.GetHeadPoint() : new Vector3(boardPos.x, ai.transform.position.y + 1.5f, boardPos.z));
                return;
            }

            h.SetActivity("$vfh_status_to_board");
            bool route = ai.CanReach(spot);
            if (route)
                _noRouteSince = -1f;
            else if (_noRouteSince < 0f)
                _noRouteSince = Time.time;
            bool stuck = ai.StuckSeconds(spot) > GiveUpSeconds;
            if (stuck || (_noRouteSince >= 0f && Time.time - _noRouteSince > NoRouteSeconds))
            {
                VfhLog.I(LogCat.AI, "park.moved", ("hid", h.Hid), ("from", ai.transform.position), ("to", spot),
                    ("why", stuck ? "stuck" : "no route"));
                Followers.FollowCatchUp.Place(ai, spot, boardPos);
                _noRouteSince = -1f;
                return;
            }
            ai.WalkTo(dt, spot, AtSpot * 0.5f, run: Vector3.Distance(ai.transform.position, spot) > 8f);
        }

        // In front of the board (or behind it) if there's a route there, else anywhere a couple of metres round it.
        private static Vector3 Spot(HirelingAI ai, HiringBoard? board, Vector3 boardPos)
        {
            Vector3 fwd = board != null ? board.transform.forward : Vector3.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 first = Ground(boardPos + fwd * 2.5f, boardPos.y);
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = Ground(boardPos + Quaternion.Euler(0f, i * 45f, 0f) * fwd * 2.5f, boardPos.y);
                if (!WaterRules.Under(p, 0f) && ai.CanReach(p))
                    return p;
            }
            return first;
        }

        // The ground there, or the board's own floor when the board stands on a floor above the ground.
        private static Vector3 Ground(Vector3 p, float boardY)
        {
            float ground = ZoneSystem.instance.GetGroundHeight(p);
            p.y = Mathf.Abs(ground - boardY) < 1f ? ground : boardY;
            return p;
        }
    }
}
