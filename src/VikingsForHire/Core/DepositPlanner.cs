using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core
{
    /// <summary>A chest as the planner sees it: its slot stacks and how many slots are empty.</summary>
    public sealed class ChestInfo
    {
        public string Id { get; }
        public float Distance { get; }
        public IReadOnlyList<(string Prefab, int Count)> Stacks { get; }
        public int EmptySlots { get; }

        public ChestInfo(string id, float distance, IReadOnlyList<(string Prefab, int Count)> stacks, int emptySlots)
        {
            Id = id;
            Distance = distance;
            Stacks = stacks;
            EmptySlots = emptySlots;
        }

        public bool Holds(string prefab) => Stacks.Any(s => s.Prefab == prefab && s.Count > 0);
    }

    public readonly record struct DepositStep(string ChestId, string Prefab, int Amount);

    public sealed class DepositPlan
    {
        public IReadOnlyList<DepositStep> Steps { get; }
        public IReadOnlyDictionary<string, int> ToDropPile { get; }

        public DepositPlan(IReadOnlyList<DepositStep> steps, IReadOnlyDictionary<string, int> toDropPile)
        {
            Steps = steps;
            ToDropPile = toDropPile;
        }
    }

    public static class DepositPlanner
    {
        /// <summary>
        /// Each item type goes only to chests that already hold it, nearest first: topping up existing stacks, then
        /// empty slots. Whatever doesn't fit goes to the drop pile. Item types are never mixed into a chest that lacks them.
        /// </summary>
        public static DepositPlan Plan(IReadOnlyDictionary<string, int> cargo, IReadOnlyList<ChestInfo> chests,
            Func<string, int> maxStack)
        {
            var steps = new List<DepositStep>();
            var drop = new Dictionary<string, int>();
            var emptyLeft = chests.ToDictionary(c => c.Id, c => c.EmptySlots);
            var ordered = chests.OrderBy(c => c.Distance).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

            // Bigger loads first so they get first claim on shared empty slots; name breaks ties for a stable plan.
            foreach (KeyValuePair<string, int> item in cargo.Where(c => c.Value > 0)
                         .OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal))
            {
                string prefab = item.Key;
                int left = item.Value;
                int stack = Math.Max(1, maxStack(prefab));

                foreach (ChestInfo chest in ordered)
                {
                    if (left == 0)
                        break;
                    if (!chest.Holds(prefab))
                        continue;

                    int room = chest.Stacks.Where(s => s.Prefab == prefab).Sum(s => Math.Max(0, stack - s.Count));
                    int put = Math.Min(left, room);
                    left -= put;

                    while (left > 0 && emptyLeft[chest.Id] > 0)
                    {
                        int n = Math.Min(left, stack);
                        put += n;
                        left -= n;
                        emptyLeft[chest.Id]--;
                    }

                    if (put > 0)
                        steps.Add(new DepositStep(chest.Id, prefab, put));
                }

                if (left > 0)
                    drop[prefab] = left;
            }

            return new DepositPlan(steps, drop);
        }
    }
}
