using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core
{
    /// <summary>A food the chests can spare for the board: points per item, how many, and how many fill a slot.</summary>
    public readonly struct BoardFood
    {
        public BoardFood(string prefab, int points, int have, int stack)
        {
            Prefab = prefab;
            Points = points;
            Have = have;
            Stack = Math.Max(1, stack);
        }

        public string Prefab { get; }
        public int Points { get; }
        public int Have { get; }
        public int Stack { get; }
    }

    public sealed class BoardFillPlan
    {
        /// <summary>Food -> how many to put on the board.</summary>
        public Dictionary<string, int> Take { get; } = new();

        /// <summary>The points that brings.</summary>
        public int Points { get; set; }

        /// <summary>Whether it covers what was asked.</summary>
        public bool Enough { get; set; }
    }

    /// <summary>
    /// What a Steward puts on the board. The board has few slots and a big crew eats a lot, so cheap food can fill every
    /// slot and still not pay a day's wages. Cheapest first (the best food stays for players) as long as the rest still
    /// fits: each slot goes to the cheapest food that leaves the remaining slots able to cover what's left, else the
    /// richest food per slot. Room left in stacks already on the board is used first and costs no slot.
    /// </summary>
    public static class BoardFill
    {
        private sealed class Left
        {
            public Left(BoardFood f, int room)
            {
                Food = f;
                Have = f.Have;
                Room = room;
            }

            public BoardFood Food { get; }
            public int Have { get; set; }
            public int Room { get; set; }

            // The next unit: what fits in the stack already on the board (no slot), else a whole slot.
            public (int Items, int Slots) Next => Room > 0 ? (Math.Min(Room, Have), 0) : (Math.Min(Food.Stack, Have), 1);
            public int SlotValue => Math.Min(Food.Stack, Have) * Food.Points;
        }

        /// <param name="foods">What the chests can spare.</param>
        /// <param name="slots">Free slots (on the board and in the Steward's cargo, whichever is fewer).</param>
        /// <param name="room">Food -> room left in its stacks already on the board.</param>
        /// <param name="points">The food points wanted.</param>
        public static BoardFillPlan Plan(IEnumerable<BoardFood> foods, int slots, IReadOnlyDictionary<string, int> room, int points)
        {
            var plan = new BoardFillPlan();
            List<Left> left = foods.Where(f => f.Points > 0 && f.Have > 0)
                .OrderBy(f => f.Points).ThenBy(f => f.Prefab, StringComparer.Ordinal)
                .Select(f => new Left(f, room.TryGetValue(f.Prefab, out int r) ? Math.Max(0, r) : 0)).ToList();
            int need = points;
            while (need > 0)
            {
                List<Left> usable = left.Where(l => l.Have > 0 && (l.Room > 0 || slots > 0)).ToList();
                if (usable.Count == 0)
                    break;
                Left? pick = usable.FirstOrDefault(l =>
                {
                    (int items, int cost) = l.Next;
                    return items * l.Food.Points + Best(left, l, items, slots - cost) >= need;
                });
                pick ??= usable.OrderByDescending(l => l.Next.Items * l.Food.Points).ThenBy(l => l.Next.Slots).First();
                (int n, int used) = pick.Next;
                n = Math.Min(n, (need + pick.Food.Points - 1) / pick.Food.Points);
                pick.Have -= n;
                if (used == 0)
                    pick.Room -= n;
                slots -= used;
                need -= n * pick.Food.Points;
                plan.Take[pick.Food.Prefab] = (plan.Take.TryGetValue(pick.Food.Prefab, out int had) ? had : 0) + n;
                plan.Points += n * pick.Food.Points;
            }
            plan.Enough = need <= 0;
            return plan;
        }

        // The most points the given slots could still bring (richest slots first, room on the board for free), after
        // taking <paramref name="taking"/> of <paramref name="from"/>.
        private static int Best(List<Left> left, Left from, int taking, int slots)
        {
            int total = 0;
            var values = new List<int>();
            foreach (Left l in left)
            {
                int have = l == from ? l.Have - taking : l.Have;
                int room = l == from && l.Room > 0 ? l.Room - taking : l.Room;
                int free = Math.Min(Math.Max(0, room), have);
                total += free * l.Food.Points;
                have -= free;
                while (have > 0)
                {
                    int n = Math.Min(l.Food.Stack, have);
                    values.Add(n * l.Food.Points);
                    have -= n;
                }
            }
            return total + values.OrderByDescending(v => v).Take(Math.Max(0, slots)).Sum();
        }
    }
}
