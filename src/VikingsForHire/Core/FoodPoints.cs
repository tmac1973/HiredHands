using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core
{
    public readonly record struct FoodStack(string Prefab, int PointsPerItem, int Count);

    public sealed class PaymentPlan
    {
        public static readonly PaymentPlan Insufficient = new(false, new List<(string, int)>(), 0, 0);

        public bool Sufficient { get; }
        public IReadOnlyList<(string Prefab, int Count)> Take { get; }
        public int Paid { get; }
        /// <summary>Points paid beyond the requirement. Not refunded: food comes in whole items.</summary>
        public int Overpay { get; }

        public PaymentPlan(bool sufficient, IReadOnlyList<(string, int)> take, int paid, int overpay)
        {
            Sufficient = sufficient;
            Take = take;
            Paid = paid;
            Overpay = overpay;
        }
    }

    public static class FoodPoints
    {
        public static int PointsFor(float health, float stamina, float eitr) =>
            (int)Math.Round(health + stamina + eitr, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Whether an item pays upkeep. Raw food doesn't (unless allowed): what the raw list names, and, once the game's
        /// recipes are known (<paramref name="prepared"/>), anything edible that nothing makes, so a mod's raw mushroom or
        /// meat is raw without being listed.
        /// </summary>
        public static bool IsAcceptable(string prefab, bool hasFoodValue, bool allowRaw, ICollection<string> rawFoods, ICollection<string>? prepared = null) =>
            hasFoodValue && (allowRaw || (!rawFoods.Contains(prefab) && (prepared == null || prepared.Contains(prefab))));

        public static int Total(IEnumerable<FoodStack> available) => available.Sum(s => s.PointsPerItem * s.Count);

        /// <summary>
        /// Picks whole items, cheapest per item first so good food is kept for the player, until the requirement is met.
        /// </summary>
        public static PaymentPlan PlanPayment(IEnumerable<FoodStack> available, int required)
        {
            if (required <= 0)
                return new PaymentPlan(true, new List<(string, int)>(), 0, 0);

            var stacks = available.Where(s => s.PointsPerItem > 0 && s.Count > 0)
                .OrderBy(s => s.PointsPerItem).ThenBy(s => s.Prefab, StringComparer.Ordinal).ToList();
            if (Total(stacks) < required)
                return PaymentPlan.Insufficient;

            var take = new Dictionary<string, int>();
            var order = new List<string>();
            int paid = 0;
            foreach (FoodStack s in stacks)
            {
                int n = Math.Min(s.Count, (required - paid + s.PointsPerItem - 1) / s.PointsPerItem);
                if (n <= 0)
                    continue;
                if (!take.ContainsKey(s.Prefab))
                    order.Add(s.Prefab);
                take[s.Prefab] = take.TryGetValue(s.Prefab, out int prev) ? prev + n : n;
                paid += n * s.PointsPerItem;
                if (paid >= required)
                    break;
            }

            return new PaymentPlan(true, order.Select(p => (p, take[p])).ToList(), paid, paid - required);
        }
    }
}
