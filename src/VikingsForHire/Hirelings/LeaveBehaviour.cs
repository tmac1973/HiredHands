using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// A hireling that's quit, been dismissed or lost its board: drops its cargo (at the board's pile when close to it),
    /// walks 30 m away from home and disappears, then takes itself off the roster. Gives up walking after 60 s.
    /// </summary>
    internal sealed class LeaveBehaviour : IHirelingBehaviour
    {
        private const float WalkAway = 30f;
        private const float GiveUpSeconds = 60f;

        private bool _dropped;
        private float _started = -1f;
        private Vector3 _target;

        public string Name => "Leave";
        public int Priority => 1000;

        public bool Wants(HirelingAI ai) => ai.Hireling.Mode == HirelingMode.Leaving;

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (!_dropped)
            {
                _dropped = true;
                _started = Time.time;
                if (h.CargoInventory != null)
                {
                    HiringBoard? board = HiringBoard.Loaded.Find(b => b != null && b.Id == h.BoardId);
                    bool nearBoard = board != null && Vector3.Distance(board.transform.position, h.transform.position) <= Mathf.Max(10f, h.Radius);
                    DropPile.DropAll(h.CargoInventory, nearBoard ? DropPile.Position(board!) : h.transform.position + Vector3.up * 0.5f,
                        nearBoard ? "leaving at board" : "leaving", h.Hid);
                }
                Vector3 away = Vector3.ProjectOnPlane(h.transform.position - h.Home, Vector3.up);
                if (away.sqrMagnitude < 1f)
                    away = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                _target = h.Home + away.normalized * (Vector3.Distance(h.transform.position, h.Home) + WalkAway);
                VfhLog.I(LogCat.Hireling, "hireling.leaving", ("hid", h.Hid), ("name", h.DisplayName), ("status", h.Zdo?.GetString(HirelingZdo.Status)));
            }

            bool arrived = ai.WalkTo(dt, _target, 2f, run: false);
            if (!arrived && Time.time - _started < GiveUpSeconds)
                return;

            VfhLog.I(LogCat.Hireling, "hireling.left", ("hid", h.Hid), ("name", h.DisplayName), ("walked", arrived));
            if (h.BoardId.Length > 0)
                MutationService.SubmitBoard(h.BoardId, new RosterOp { Type = RosterOpType.Remove, Hid = h.Hid });
            h.GetComponent<ZNetView>().ClaimOwnership();
            ZNetScene.instance.Destroy(h.gameObject);
        }
    }
}
