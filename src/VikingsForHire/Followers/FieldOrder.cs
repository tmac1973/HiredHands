using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// An explicit order from the Command Stone, held by the follower's AI on its owner's machine (followers are
    /// simulated there). Harvest: work this tree or rock and what it leaves behind. Attack: go for this enemy. Orders
    /// expire after ten minutes; afterwards the follower goes back to its follow mode (Stay at the job, for a harvest).
    /// </summary>
    internal sealed class FieldOrder
    {
        // Long enough for a big tree and all its logs, or a whole copper deposit; the follower stays at the spot anyway.
        public const float Lifetime = 600f;
        public const float HarvestRadius = 8f;

        public enum OrderKind
        {
            Harvest,
            Attack,
            /// <summary>Put what this chest already holds from the cargo into it, then carry on.</summary>
            Unload,
        }

        /// <summary>How long an unload order lasts before it's dropped (a chest it can't reach).</summary>
        public const float UnloadLifetime = 60f;

        public static FieldOrder Unload(Container chest) => new()
        {
            Kind = OrderKind.Unload, Target = chest, Position = chest.transform.position, Until = Time.time + UnloadLifetime,
        };

        /// <summary>The cargo items (prefab -> count) a chest already holds some of: the only ones an unload puts in it.</summary>
        public static Dictionary<string, int> ForChest(Inventory cargo, Container chest)
        {
            var have = new HashSet<string>();
            foreach (ItemDrop.ItemData i in chest.GetInventory().GetAllItems())
                if (i.m_dropPrefab != null)
                    have.Add(i.m_dropPrefab.name);
            var take = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData i in cargo.GetAllItems())
                if (i.m_dropPrefab != null && have.Contains(i.m_dropPrefab.name))
                    take[i.m_dropPrefab.name] = (take.TryGetValue(i.m_dropPrefab.name, out int n) ? n : 0) + i.m_stack;
            return take;
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

        public bool Allows(Component c) => Allowed == null || c == Target || Allowed.Contains(PrefabOf(c));

        // The prefab of the world object a component belongs to. A broken copper deposit's MineRock5 sits on a child
        // object ("___MineRock5 m_meshFilter"), so the component's own object name doesn't say what it is.
        private static string PrefabOf(Component c)
        {
            ZNetView? view = c.GetComponentInParent<ZNetView>();
            return Utils.GetPrefabName(view != null ? view.gameObject : c.gameObject);
        }

        private static HashSet<string> Remains(Component target)
        {
            var names = new HashSet<string> { PrefabOf(target) };
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
