using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>
    /// Hirelings meeting head-on in a narrow spot: each keeps to its right for a moment so they slide past each other, and
    /// two still pressed together after a couple of seconds stop colliding with each other briefly to pass through. Done
    /// where every hireling's movement ends (BaseAI.MoveTowards), so walking, chasing and routes all get it.
    /// </summary>
    [HarmonyPatch]
    internal static class Passing
    {
        private const float LookAhead = 2.5f;     // how far ahead an oncoming hireling counts
        private const float Lane = 0.9f;          // how far to the side it can be and still be in the way
        private const float SidestepSeconds = 0.8f;
        private const float SidestepAngle = 45f;
        private const float PressedSecondsNarrow = 1f; // no room to step aside: pass through sooner
        private static readonly int WallMask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");
        private const float PassThroughSeconds = 1.5f;

        private sealed class State
        {
            public float SidestepUntil;
            public float PressedSince = -1f;
            public bool Narrow;
            public Vector3 SampledPos;
            public float SampledAt = -1f;
            public float Moved = 99f;
        }

        // Something solid just to the right (a corridor wall): stepping aside would only press into it.
        private static bool WallOnRight(Vector3 at, Vector3 heading) =>
            Physics.Raycast(at + Vector3.up * 0.8f, Quaternion.Euler(0f, 90f, 0f) * heading, VfhConfig.PassSideRoom.Value, WallMask, QueryTriggerInteraction.Ignore);

        private static readonly Dictionary<HirelingAI, State> States = new();
        private static readonly List<(Collider A, Collider B, float Until)> Ignored = new();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
        private static void Prefix(BaseAI __instance, ref Vector3 dir)
        {
            if (__instance is not HirelingAI ai || !VfhConfig.HirelingsPassEachOther.Value || dir.sqrMagnitude < 0.01f)
                return;
            try
            {
                Restore();
                if (!States.TryGetValue(ai, out State st))
                    States[ai] = st = new State();
                Vector3 me = ai.transform.position;
                Vector3 heading = new Vector3(dir.x, 0f, dir.z).normalized;
                Hireling? ahead = null;
                Hireling? pressed = null;
                foreach (Hireling other in Hireling.Loaded)
                {
                    if (other == null || other.Ai == ai || other.Humanoid == null)
                        continue;
                    Vector3 to = other.transform.position - me;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist > LookAhead || Mathf.Abs(other.transform.position.y - me.y) > 1.5f)
                        continue;
                    if (pressed == null || dist < Vector3.Distance(pressed.transform.position, me))
                        pressed = other; // the nearest one about, in case we get nowhere
                    float along = Vector3.Dot(to, heading);
                    float side = Vector3.Cross(heading, to).y; // > 0: to our left
                    if (along <= 0f || Mathf.Abs(side) > Lane)
                        continue;
                    // Coming towards us, or standing in the way.
                    Vector3 v = other.Humanoid.GetVelocity();
                    v.y = 0f;
                    if (v.sqrMagnitude < 0.25f || Vector3.Dot(v.normalized, heading) < -0.3f)
                        ahead = other;
                }
                if (ahead != null && Time.time >= st.SidestepUntil)
                {
                    st.Narrow = WallOnRight(me, heading);
                    if (!st.Narrow)
                        st.SidestepUntil = Time.time + SidestepSeconds;
                    VfhLog.T(LogCat.AI, "nav.pass", ("hid", ai.Hireling.Hid), ("other", ahead.Hid), ("narrow", st.Narrow));
                }
                if (Time.time < st.SidestepUntil)
                    dir = Quaternion.Euler(0f, SidestepAngle, 0f) * dir; // to our right

                // Getting nowhere with another one close by (wedged together, or blocking the way): let the two pass through each
                // other for a moment. "Nowhere" is under half a metre in the last second, so jostling in place still counts.
                if (Time.time - st.SampledAt >= 1f)
                {
                    st.Moved = Utils.DistanceXZ(me, st.SampledPos);
                    st.SampledPos = me;
                    st.SampledAt = Time.time;
                }
                bool stuck = st.Moved < 0.5f;
                if (pressed != null && stuck)
                {
                    if (st.PressedSince < 0f)
                        st.PressedSince = Time.time;
                    else if (Time.time - st.PressedSince > (st.Narrow ? PressedSecondsNarrow : VfhConfig.PassThroughAfter.Value))
                    {
                        PassThrough(ai.Hireling, pressed);
                        st.PressedSince = -1f;
                    }
                }
                else
                {
                    st.PressedSince = -1f;
                }
            }
            catch (System.Exception e)
            {
                VfhLog.PatchFailed("Passing.MoveTowards", e);
            }
        }

        private static void PassThrough(Hireling a, Hireling b)
        {
            Collider? ca = a.Humanoid.m_collider, cb = b.Humanoid.m_collider;
            if (ca == null || cb == null)
                return;
            Physics.IgnoreCollision(ca, cb, true);
            Ignored.Add((ca, cb, Time.time + PassThroughSeconds));
            VfhLog.D(LogCat.AI, "nav.pass_through", ("hid", a.Hid), ("other", b.Hid), ("at", a.transform.position));
        }

        private static void Restore()
        {
            for (int i = Ignored.Count - 1; i >= 0; i--)
            {
                (Collider a, Collider b, float until) = Ignored[i];
                if (a == null || b == null)
                {
                    Ignored.RemoveAt(i);
                    continue;
                }
                if (Time.time < until)
                    continue;
                Physics.IgnoreCollision(a, b, false);
                Ignored.RemoveAt(i);
            }
            if (States.Count > 64)
                foreach (HirelingAI k in States.Keys.Where(k => k == null).ToList())
                    States.Remove(k);
        }
    }
}
