using System.Collections.Generic;
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

        /// <summary>
        /// For a harvest: the prefabs that count as "this tree or rock and what it leaves behind" (the tree, its logs and
        /// their smaller logs, its stump; a rock and the pieces it breaks into). Anything else nearby, like saplings or
        /// another tree, isn't part of the order.
        /// </summary>
        public HashSet<string>? Allowed;

        public static FieldOrder Harvest(Component target) => new()
        {
            Kind = OrderKind.Harvest, Target = target, Position = target.transform.position, Until = Time.time + Lifetime, Allowed = Remains(target),
        };

        public bool Allows(Component c) => Allowed == null || c == Target || Allowed.Contains(Utils.GetPrefabName(c.gameObject));

        private static HashSet<string> Remains(Component target)
        {
            var names = new HashSet<string> { Utils.GetPrefabName(target.gameObject) };
            switch (target)
            {
                case TreeBase tree:
                    Add(names, tree.m_stubPrefab);
                    AddLogs(names, tree.m_logPrefab);
                    break;
                case TreeLog log:
                    AddLogs(names, log.m_subLogPrefab);
                    break;
                case Destructible d:
                    Add(names, d.m_spawnWhenDestroyed);
                    break;
            }
            return names;
        }

        private static void AddLogs(HashSet<string> names, GameObject? log)
        {
            for (int depth = 0; log != null && depth < 5; depth++)
            {
                if (!names.Add(log.name))
                    break;
                log = log.GetComponent<TreeLog>()?.m_subLogPrefab;
            }
        }

        private static void Add(HashSet<string> names, GameObject? prefab)
        {
            if (prefab != null)
                names.Add(prefab.name);
        }

        public bool Expired => Time.time > Until || (Kind == OrderKind.Attack ? Enemy == null || Enemy.IsDead() : false);
    }
}
