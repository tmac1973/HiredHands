using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// A posted hireling stands at its post facing the way it was posted: a guard instead of patrolling (combat still
    /// takes over by stance; its leash is the post, and afterwards it walks back here), a worker instead of idling about
    /// whenever it has no work.
    /// </summary>
    internal sealed class PostBehaviour : IHirelingBehaviour
    {
        private const float AtPost = 0.8f;
        private const float StuckSeconds = 15f;

        private const int TriesBeforeMoving = 3;
        private const float RetrySeconds = 20f;

        private float _bestDistance = float.MaxValue;
        private float _progressAt;
        private int _failed;
        private float _retryAt;

        public string Name => "Post";
        public int Priority => 101; // just above patrol, which a posted guard doesn't do

        // Any posted hireling working at home: guards stand guard there; a gatherer stands there whenever it isn't
        // gathering or delivering (its work outranks this), e.g. with "Works at home" off, instead of wandering about.
        public bool Wants(HirelingAI ai) =>
            ai.Hireling.Mode == HirelingMode.Working && ai.Hireling.HasPost;

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            Vector3 post = h.PostPos;
            float dist = Vector3.Distance(ai.transform.position, post); // height counts: a tower post is up the stairs
            if (dist > AtPost)
            {
                // Waiting to try again after a failed walk: stand where we got to and say we can't get there.
                if (Time.time < _retryAt)
                {
                    ai.Halt();
                    h.SetActivity("$vfh_status_post_unreachable");
                    return;
                }
                if (dist < _bestDistance - 0.3f)
                {
                    _bestDistance = dist;
                    _progressAt = Time.time;
                }
                else if (Time.time - _progressAt > StuckSeconds)
                {
                    // Can't get there (walled in from where it arrived, no stairs up…): wait and try again; after a few
                    // tries, when no one's looking, go straight to the post (as a follower catches up). The player put
                    // the post there, so that's where it belongs; standing 30 m off saying "on guard" helped nobody.
                    _failed++;
                    VfhLog.I(LogCat.AI, "post.unreachable", ("hid", h.Hid), ("post", post), ("closest", dist), ("tries", _failed));
                    if (_failed >= TriesBeforeMoving && !Followers.FollowCatchUp.OwnerSees(ai))
                    {
                        Vector3 from = ai.transform.position;
                        Followers.FollowCatchUp.Place(ai, post, post + Quaternion.Euler(0f, h.PostYaw, 0f) * Vector3.forward * 5f);
                        VfhLog.I(LogCat.AI, "post.moved", ("hid", h.Hid), ("from", from), ("to", post));
                        Reached();
                        return;
                    }
                    _retryAt = Time.time + RetrySeconds;
                    _bestDistance = float.MaxValue;
                    _progressAt = _retryAt;
                    return;
                }
                h.SetActivity("$vfh_status_to_post");
                ai.WalkTo(dt, post, AtPost * 0.5f, run: dist > 10f);
                return;
            }
            Reached();
            Hold(ai, h);
        }

        private void Reached()
        {
            _bestDistance = float.MaxValue;
            _progressAt = Time.time;
            _failed = 0;
            _retryAt = 0f;
        }

        private static void Hold(HirelingAI ai, Hireling h)
        {
            h.SetActivity("$vfh_status_on_post");
            ai.Halt();
            ai.Face(ai.transform.position + Quaternion.Euler(0f, h.PostYaw, 0f) * Vector3.forward * 5f + Vector3.up * 1.5f);
        }
    }
}
