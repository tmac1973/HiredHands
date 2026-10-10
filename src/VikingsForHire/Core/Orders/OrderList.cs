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
        public const int MaxOrders = 64;
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
                existing.Target = Clamp(target);
                return true;
            }
            if (Orders.Count >= MaxOrders)
                return false;
            Orders.Add(new ProductionOrder { Item = item, Kind = kind, Target = Clamp(target) });
            return true;
        }

        public bool Remove(string item) => Find(item) is ProductionOrder o && Orders.Remove(o);

        public bool SetTarget(string item, int target)
        {
            if (Find(item) is not ProductionOrder o)
                return false;
            o.Target = Clamp(target);
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
        /// The most of a station product the Steward may have in the chests before it stops making it: null with no
        /// Steward order for it (no limit), 0 while the order is paused (none made).
        /// </summary>
        public int? StationCap(string item) =>
            Find(item) is { Kind: OrderKind.Station } o ? (o.Paused ? 0 : o.Target) : null;

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
                list.Orders.Add(new ProductionOrder { Item = f[0], Target = Clamp(target), Kind = kind, Paused = f[3] == "1" });
            }
            return list;
        }

        private static int Clamp(int target) => Math.Max(0, Math.Min(MaxTarget, target));

        private static string KindCode(OrderKind k) => k switch
        {
            OrderKind.Seed => "s",
            OrderKind.Crop => "c",
            OrderKind.Station => "m",
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
                default: kind = OrderKind.Crop; return false;
            }
        }
    }
}
