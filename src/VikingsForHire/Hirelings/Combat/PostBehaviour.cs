using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// A posted guard (phase 13) stands guard at its post facing the way it was posted, instead of patrolling. Combat
    /// still takes over by stance; its leash is the post, and afterwards it walks back here.
    /// </summary>
    internal sealed class PostBehaviour : IHirelingBehaviour
    {
        private const float AtPost = 0.8f;
        private const float StuckSeconds = 15f;

        private float _bestDistance = float.MaxValue;
        private float _progressAt;
        private bool _reportedUnreachable;

        public string Name => "Post";
        public int Priority => 101; // just above patrol, which a posted guard doesn't do

        public bool Wants(HirelingAI ai) =>
            ai.Hireling.Job.IsGuard() && ai.Hireling.Mode == HirelingMode.Working && ai.Hireling.HasPost;

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            Vector3 post = h.PostPos;
            float dist = Utils.DistanceXZ(ai.transform.position, post);
            if (dist > AtPost)
            {
                if (dist < _bestDistance - 0.3f)
                {
                    _bestDistance = dist;
                    _progressAt = Time.time;
                }
                else if (Time.time - _progressAt > StuckSeconds)
                {
                    // Can't get there (no stairs up, say): stand as close as we got and say so once.
                    if (!_reportedUnreachable)
                    {
                        _reportedUnreachable = true;
                        VfhLog.I(LogCat.AI, "post.unreachable", ("hid", h.Hid), ("post", post), ("closest", dist));
                    }
                    Hold(ai, h);
                    return;
                }
                h.SetActivity("$vfh_status_to_post");
                ai.WalkTo(dt, post, AtPost * 0.5f, run: dist > 10f);
                return;
            }
            _bestDistance = float.MaxValue;
            _progressAt = Time.time;
            _reportedUnreachable = false;
            Hold(ai, h);
        }

        private static void Hold(HirelingAI ai, Hireling h)
        {
            h.SetActivity("$vfh_status_on_post");
            ai.Halt();
            ai.Face(ai.transform.position + Quaternion.Euler(0f, h.PostYaw, 0f) * Vector3.forward * 5f + Vector3.up * 1.5f);
        }
    }
}
