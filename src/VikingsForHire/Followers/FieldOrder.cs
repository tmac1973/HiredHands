using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// An explicit order from the Command Stone, held by the follower's AI on its owner's machine (followers are
    /// simulated there). Harvest: work this tree or rock and what it leaves behind. Attack: go for this enemy. Orders
    /// expire after two minutes; afterwards the follower goes back to its follow mode.
    /// </summary>
    internal sealed class FieldOrder
    {
        public const float Lifetime = 120f;
        public const float HarvestRadius = 8f;

        public enum OrderKind
        {
            Harvest,
            Attack,
        }

        public OrderKind Kind;
        public Component? Target;
        public Character? Enemy;
        public Vector3 Position;
        public float Until;

        public bool Expired => Time.time > Until || (Kind == OrderKind.Attack ? Enemy == null || Enemy.IsDead() : false);
    }
}
