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
        private const float PressedDistance = 0.9f;
        private const float PressedSeconds = 2f;
        private const float PassThroughSeconds = 1.5f;

        private sealed class State
        {
            public float SidestepUntil;
            public float PressedSince = -1f;
        }

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
                    if (dist < PressedDistance)
                        pressed = other;
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
                    st.SidestepUntil = Time.time + SidestepSeconds;
                    VfhLog.T(LogCat.AI, "nav.pass", ("hid", ai.Hireling.Hid), ("other", ahead.Hid));
                }
                if (Time.time < st.SidestepUntil)
                    dir = Quaternion.Euler(0f, SidestepAngle, 0f) * dir; // to our right

                // Wedged against another one, getting nowhere: let them pass through each other for a moment.
                bool stuck = ai.Hireling.Humanoid.GetVelocity().sqrMagnitude < 0.09f;
                if (pressed != null && stuck)
                {
                    if (st.PressedSince < 0f)
                        st.PressedSince = Time.time;
                    else if (Time.time - st.PressedSince > PressedSeconds)
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
