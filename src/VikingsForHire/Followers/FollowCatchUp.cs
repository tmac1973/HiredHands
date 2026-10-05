using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// Keeps a following follower with its owner: a sprint boost when it's well behind, an unstick routine (jump, then a
    /// sideways detour) when it isn't getting anywhere, and as the safety net a teleport to just behind the owner when it's
    /// far behind or stuck, but only while the owner can't see it. Followers simulate on their owner's machine, so the
    /// local camera is the owner's.
    /// </summary>
    internal sealed class FollowCatchUp
    {
        private const float BoostBeyond = 15f;
        private const float DoubleBoostBeyond = 25f;
        private const float StuckCheckSeconds = 2f;
        private const float StuckMoveLess = 1f;
        private const float DetourSeconds = 1.5f;
        private const float LagLogSeconds = 5f;
        private static readonly int GroundMask = LayerMask.GetMask("terrain", "static_solid", "Default", "piece");
        private static readonly int ViewBlockMask = LayerMask.GetMask("terrain", "static_solid", "Default", "piece");

        /// <summary>The largest distance any follower has been behind its owner since the last reset (for tests).</summary>
        public static float MaxLag { get; private set; }
        public static int Teleports { get; private set; }

        public static void ResetStats()
        {
            MaxLag = 0f;
            Teleports = 0;
        }

        private Vector3 _checkPos;
        private float _checkAt = -1f;
        private float _stuckSince = -1f;
        private int _unstickStep;
        private Vector3 _detour;
        private float _detourUntil;
        private float _nextLagLog;
        private float _lagMaxSinceLog;

        /// <summary>
        /// Call every follow tick while the follower should be with its owner. Returns true when it took care of the
        /// movement this tick (teleported, or walking a detour), so the caller shouldn't walk too.
        /// </summary>
        public bool Tick(HirelingAI ai, Player owner, float dist, float dt)
        {
            Hireling h = ai.Hireling;
            Log(h, dist);
            // The bonus matches a player at full run skill; well behind, double it so the gap closes even against that.
            if (dist > BoostBeyond)
                ai.BoostRun(VfhConfig.FollowerCatchUpSpeedBonus.Value * (dist > DoubleBoostBeyond ? 2f : 1f));

            bool shouldMove = dist > 6f;
            UpdateStuck(ai, shouldMove);
            float teleportAt = VfhConfig.FollowerCatchUpTeleportDistance.Value;
            bool stuckLong = _stuckSince >= 0f && Time.time - _stuckSince > VfhConfig.FollowerStuckTeleportSeconds.Value;
            if (teleportAt > 0f && (dist > teleportAt || stuckLong) && CanTeleport(owner) && !OwnerSees(ai) && Teleport(ai, owner, dist, stuckLong ? "stuck" : "far"))
                return true;

            if (Time.time < _detourUntil)
            {
                ai.MoveAround(dt, _detour);
                return true;
            }
            return false;
        }

        // Not getting anywhere: under 1 m moved in 2 s while it should be walking. Then jump, then detour sideways
        // (alternating sides), and keep counting toward the teleport.
        private void UpdateStuck(HirelingAI ai, bool shouldMove)
        {
            Vector3 pos = ai.transform.position;
            // On its way through a door or up a stair at home: not stuck, however slowly it's going.
            if (!shouldMove || (Hirelings.Nav.NavLinkRegistry.Enabled && ai.Links.Progressing))
            {
                _stuckSince = -1f;
                _unstickStep = 0;
                _checkAt = -1f;
                return;
            }
            if (_checkAt < 0f)
            {
                _checkPos = pos;
                _checkAt = Time.time;
                return;
            }
            if (Time.time - _checkAt < StuckCheckSeconds)
                return;
            bool moved = Vector3.Distance(pos, _checkPos) >= StuckMoveLess;
            _checkPos = pos;
            _checkAt = Time.time;
            if (moved)
            {
                _stuckSince = -1f;
                _unstickStep = 0;
                return;
            }
            if (_stuckSince < 0f)
                _stuckSince = Time.time - StuckCheckSeconds;
            Humanoid hum = ai.Hireling.Humanoid;
            string how;
            if (_unstickStep % 2 == 0)
            {
                hum.Jump();
                how = "jump";
            }
            else
            {
                Vector3 side = (_unstickStep / 2 % 2 == 0 ? 1f : -1f) * ai.transform.right;
                _detour = pos + side * 4f - ai.transform.forward * 1.5f;
                _detourUntil = Time.time + DetourSeconds;
                how = "detour";
            }
            _unstickStep++;
            VfhLog.D(LogCat.Follow, "follow.unstick", ("hid", ai.Hireling.Hid), ("how", how), ("stuckFor", Time.time - _stuckSince), ("pos", pos));
        }

        // Only on foot, on land, not aboard a ship: portals and ships carry followers their own way (TeleportTravel,
        // ShipStowage).
        private static bool CanTeleport(Player owner) =>
            owner == Player.m_localPlayer && owner.IsOnGround() && !owner.IsSwimming() && !owner.IsAttached() && !owner.IsTeleporting() &&
            owner.GetStandingOnShip() == null;

        // Whether the owner could see the follower right now: inside the camera's view and not hidden behind terrain or
        // buildings (checked to its head and its middle).
        private static bool OwnerSees(HirelingAI ai)
        {
            Camera? cam = GameCamera.instance != null ? GameCamera.instance.m_camera : null;
            if (cam == null)
                return true;
            Humanoid hum = ai.Hireling.Humanoid;
            foreach (Vector3 point in new[] { hum.GetHeadPoint(), hum.GetCenterPoint() })
            {
                Vector3 vp = cam.WorldToViewportPoint(point);
                if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f)
                    continue;
                Vector3 from = cam.transform.position;
                if (!Physics.Linecast(from, point, ViewBlockMask))
                    return true;
            }
            return false;
        }

        private bool Teleport(HirelingAI ai, Player owner, float dist, string why)
        {
            if (SpotNear(owner, outOfSight: true) is not Vector3 spot)
            {
                VfhLog.D(LogCat.Follow, "follow.teleport_no_spot", ("hid", ai.Hireling.Hid), ("why", why), ("dist", dist));
                return false;
            }
            Vector3 from = ai.transform.position;
            Place(ai, spot, owner.transform.position);
            Teleports++;
            _stuckSince = -1f;
            _unstickStep = 0;
            _checkAt = -1f;
            _detourUntil = 0f;
            VfhLog.I(LogCat.Follow, "follow.teleport", ("hid", ai.Hireling.Hid), ("why", why), ("dist", dist), ("from", from), ("to", spot));
            return true;
        }

        /// <summary>
        /// A spot on solid ground 3–5 m behind the owner (away from the camera's look), level with them and with a clear
        /// line to them (not through a wall into the next room); the sides if straight behind won't do. With outOfSight,
        /// also one the owner can't see. <paramref name="slot"/> turns the search so several followers get different spots.
        /// </summary>
        public static Vector3? SpotNear(Player owner, bool outOfSight, int slot = 0)
        {
            Camera? cam = GameCamera.instance != null ? GameCamera.instance.m_camera : null;
            Vector3 back = cam != null ? -Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : -owner.transform.forward;
            back = Quaternion.Euler(0f, (slot % 2 == 0 ? 1f : -1f) * 30f * ((slot + 1) / 2), 0f) * back;
            Vector3 origin = owner.transform.position;
            foreach (float angle in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f })
            {
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * back;
                foreach (float range in new[] { 5f, 4f, 3f })
                {
                    Vector3 probe = origin + dir * range;
                    // From chest height down, not from high above: indoors (dungeons, houses) a ray from above lands on the
                    // ceiling or the roof.
                    if (!Physics.Raycast(probe + Vector3.up * 1.5f, Vector3.down, out RaycastHit ground, 4.5f, GroundMask))
                        continue;
                    Vector3 spot = ground.point + Vector3.up * 0.1f;
                    if (Mathf.Abs(spot.y - origin.y) > 2.5f || ZoneSystem.instance != null && spot.y < ZoneSystem.instance.m_waterLevel - 0.3f)
                        continue;
                    if (Physics.Linecast(origin + Vector3.up * 1f, spot + Vector3.up * 1f, ViewBlockMask))
                        continue;
                    if (outOfSight && cam != null && Visible(cam, spot + Vector3.up * 1f))
                        continue;
                    return spot;
                }
            }
            return null;
        }

        private static bool Visible(Camera cam, Vector3 point)
        {
            Vector3 vp = cam.WorldToViewportPoint(point);
            return vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f &&
                   !Physics.Linecast(cam.transform.position, point, ViewBlockMask);
        }

        /// <summary>Puts a follower down at a spot (owner side), facing a point, with no fall damage from the move.</summary>
        public static void Place(HirelingAI ai, Vector3 spot, Vector3 face)
        {
            Transform t = ai.transform;
            t.position = spot;
            Vector3 look = face - spot;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                t.rotation = Quaternion.LookRotation(look);
            if (t.GetComponent<Rigidbody>() is Rigidbody body)
            {
                body.position = spot;
                body.linearVelocity = Vector3.zero;
            }
            ai.Hireling.Humanoid.m_maxAirAltitude = spot.y;
            ai.Halt();
            ai.ResetPath();
            ai.Hireling.Zdo?.SetPosition(spot);
        }

        private void Log(Hireling h, float dist)
        {
            if (dist > MaxLag)
                MaxLag = dist;
            if (dist > _lagMaxSinceLog)
                _lagMaxSinceLog = dist;
            if (Time.time < _nextLagLog)
                return;
            _nextLagLog = Time.time + LagLogSeconds;
            Vector3 v = h.GetComponent<Rigidbody>() is Rigidbody body ? body.linearVelocity : Vector3.zero;
            v.y = 0f;
            VfhLog.D(LogCat.Follow, "follow.lag", ("hid", h.Hid), ("dist", dist), ("max5s", _lagMaxSinceLog), ("stuck", _stuckSince >= 0f),
                ("speed", v.magnitude), ("running", h.Humanoid.IsRunning()), ("direct", h.Ai.ChasingDirect), ("runSpeed", h.Humanoid.m_runSpeed));
            _lagMaxSinceLog = 0f;
        }
    }
}
