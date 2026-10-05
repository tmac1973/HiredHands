using System.Collections.Generic;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Nothing to do: stand about near home (the board). It picks an open-air spot on the ground a few metres from the
    /// board that it has a full route to (on the board's floor when the board is indoors), walks there and waits, moving
    /// to another such spot now and then. Wandering at random sent hirelings into the buildings round the board, up the
    /// stairs and back down again, over and over.
    /// </summary>
    internal sealed class IdleBehaviour : IHirelingBehaviour
    {
        private const float MinRing = 3f;
        private const float MaxRing = 7f;
        private const float AtSpot = 0.8f;
        private const float GiveUpSeconds = 15f;
        private static readonly int RoofMask = LayerMask.GetMask("piece", "piece_nonsolid", "static_solid");

        private Vector3? _spot;
        private Vector3 _spotHome;
        private float _moveOnAt;
        private readonly List<Vector3> _failed = new();

        public string Name => "Idle";
        public int Priority => 0;

        public bool Wants(HirelingAI ai) => true;

        public void Tick(HirelingAI ai, float dt)
        {
            Vector3 home = ai.Hireling.Home;
            if (_spot == null || Vector3.Distance(_spotHome, home) > 1f || Time.time >= _moveOnAt)
                Pick(ai, home);
            if (_spot is not Vector3 spot)
            {
                // Nowhere suitable (all built over): stand where we are rather than roam.
                ai.Halt();
                return;
            }
            if (Vector3.Distance(ai.transform.position, spot) > AtSpot + 0.5f)
            {
                if (ai.StuckSeconds(spot) > GiveUpSeconds)
                {
                    VfhLog.D(LogCat.AI, "idle.spot_unreachable", ("hid", ai.Hireling.Hid), ("spot", spot));
                    _failed.Add(spot);
                    _spot = null;
                    return;
                }
                ai.WalkTo(dt, spot, AtSpot * 0.5f, run: false);
                return;
            }
            ai.Halt();
            ai.Face(new Vector3(home.x, ai.transform.position.y + 1.5f, home.z));
        }

        private void Pick(HirelingAI ai, Vector3 home)
        {
            if (Vector3.Distance(_spotHome, home) > 1f)
                _failed.Clear();
            _spotHome = home;
            _moveOnAt = Time.time + Random.Range(30f, 75f);
            Vector3 me = ai.transform.position;
            Vector3? best = null;
            float bestScore = float.MaxValue;
            // Open-air ground first; for a board indoors (a hall), the board's own floor.
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                float start = Random.Range(0f, 360f);
                for (int i = 0; i < 10; i++)
                {
                    float angle = start + i * 36f;
                    float ring = Random.Range(MinRing, MaxRing);
                    Vector3 p = home + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * ring;
                    p.y = pass == 0 ? ZoneSystem.instance.GetGroundHeight(p) : home.y;
                    if ((pass == 0 && Covered(p)) || _failed.Exists(f => Vector3.Distance(f, p) < 1.5f) || !ai.CanReach(p))
                        continue;
                    // Prefer somewhere new but not far from where we stand.
                    float score = Vector3.Distance(me, p) + (_spot is Vector3 old && Vector3.Distance(old, p) < 1.5f ? 5f : 0f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }
            }
            _spot = best;
            if (_failed.Count > 20)
                _failed.RemoveRange(0, 10);
        }

        // Under a roof or floor (inside a building, under a deck): not a place to loiter.
        private static bool Covered(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, 30f, RoofMask);
    }
}
