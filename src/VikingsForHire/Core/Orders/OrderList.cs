using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VikingsForHire.Core.Orders
{
    /// <summary>
    /// A board's production orders in the player's order. Seed orders always run first (in their own order), then the
    /// rest top to bottom. Saved on the board as one string.
    /// </summary>
    public sealed class OrderList
    {
        public const int MaxOrders = 128;
        public const int MaxTarget = 9999;
        private const string Version = "v1";

        public List<ProductionOrder> Orders { get; } = new();

        public ProductionOrder? Find(string item) => Orders.FirstOrDefault(o => string.Equals(o.Item, item, StringComparison.OrdinalIgnoreCase));

        /// <summary>Adds an order, or updates the target of the one already there for that item.</summary>
        public bool Add(string item, OrderKind kind, int target)
        {
            if (string.IsNullOrWhiteSpace(item) || item.Contains(":") || item.Contains(";") || item.Contains("|"))
                return false;
            if (Find(item) is ProductionOrder existing)
            {
                existing.Target = Clamp(target, existing.Kind);
                return true;
            }
            if (Orders.Count >= MaxOrders)
                return false;
            Orders.Add(new ProductionOrder { Item = item, Kind = kind, Target = Clamp(target, kind) });
            return true;
        }

        public bool Remove(string item) => Find(item) is ProductionOrder o && Orders.Remove(o);

        public bool SetTarget(string item, int target)
        {
            if (Find(item) is not ProductionOrder o)
                return false;
            o.Target = Clamp(target, o.Kind);
            return true;
        }

        public bool SetPaused(string item, bool paused)
        {
            if (Find(item) is not ProductionOrder o)
                return false;
            o.Paused = paused;
            return true;
        }

        /// <summary>
        /// The most of a station product the Steward may have in the chests before it stops making it: its own order's
        /// target (∞ = no end; 0 while paused), else the catch-all "everything else" order's (∞ while there's one), else
        /// null (no order for it).
        /// </summary>
        public int? StationCap(string item)
        {
            if (Find(item) is { Kind: OrderKind.Station } o)
                return o.Paused ? 0 : o.Target;
            if (Find(ProductionOrder.Everything) is { Kind: OrderKind.Station } all)
                return all.Paused ? 0 : ProductionOrder.Infinite;
            return null;
        }

        /// <summary>Moves an order up (-1) or down (+1) past the next order of the same group (farm, kitchen, Steward).</summary>
        public bool Move(string item, int direction)
        {
            if (Find(item) is not ProductionOrder o)
                return false;
            int i = Orders.IndexOf(o);
            int j = i;
            do
                j += direction;
            while (j >= 0 && j < Orders.Count && Orders[j].Group != o.Group);
            if (j < 0 || j >= Orders.Count)
                return false;
            Orders[i] = Orders[j];
            Orders[j] = o;
            return true;
        }

        /// <summary>Unpaused orders in the order they're worked: seed orders first, then the rest.</summary>
        public List<ProductionOrder> Active() =>
            Orders.Where(o => !o.Paused && o.Target > 0 && o.Kind == OrderKind.Seed)
                .Concat(Orders.Where(o => !o.Paused && o.Target > 0 && o.Kind is OrderKind.Crop or OrderKind.Kitchen))
                .ToList();

        /// <summary>The Rancher's unpaused orders (meat, eggs, breeding pools) in board order.</summary>
        public List<ProductionOrder> Ranch() => Orders.Where(o => !o.Paused && o.Target > 0 && o.IsRanch).ToList();

        public string Serialize() =>
            Version + "|" + string.Join(";", Orders.Select(o =>
                $"{o.Item}:{o.Target.ToString(CultureInfo.InvariantCulture)}:{KindCode(o.Kind)}:{(o.Paused ? 1 : 0)}"));

        /// <summary>Never throws: unknown versions and malformed entries are skipped.</summary>
        public static OrderList Parse(string? text)
        {
            var list = new OrderList();
            if (string.IsNullOrEmpty(text) || !text!.StartsWith(Version + "|"))
                return list;
            foreach (string entry in text.Substring(Version.Length + 1).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = entry.Split(':');
                if (f.Length != 4 || f[0].Length == 0 || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int target) ||
                    !TryKind(f[2], out OrderKind kind) || list.Find(f[0]) != null || list.Orders.Count >= MaxOrders)
                    continue;
                list.Orders.Add(new ProductionOrder { Item = f[0], Target = Clamp(target, kind), Kind = kind, Paused = f[3] == "1" });
            }
            return list;
        }

        /// <summary>Fewer than 2 can't breed: a breeding pool is never below it.</summary>
        public const int MinHerd = 2;

        private static int Clamp(int target, OrderKind kind) =>
            target == ProductionOrder.Infinite ? target : Math.Max(kind == OrderKind.Herd ? MinHerd : 0, Math.Min(MaxTarget, target));

        private static string KindCode(OrderKind k) => k switch
        {
            OrderKind.Seed => "s",
            OrderKind.Crop => "c",
            OrderKind.Station => "m",
            OrderKind.Meat => "r",
            OrderKind.Egg => "e",
            OrderKind.Herd => "h",
            _ => "k",
        };

        private static bool TryKind(string code, out OrderKind kind)
        {
            switch (code)
            {
                case "s": kind = OrderKind.Seed; return true;
                case "c": kind = OrderKind.Crop; return true;
                case "k": kind = OrderKind.Kitchen; return true;
                case "m": kind = OrderKind.Station; return true;
                case "r": kind = OrderKind.Meat; return true;
                case "e": kind = OrderKind.Egg; return true;
                case "h": kind = OrderKind.Herd; return true;
                default: kind = OrderKind.Crop; return false;
            }
        }
    }
}
