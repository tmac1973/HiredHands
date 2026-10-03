using System;
using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>
    /// Taking fees from, and refunding to, a board's storage. Works on the loaded board's Container when there is one,
    /// or on a copy loaded from the ZDO (saved back with <see cref="Wallet.Commit"/>) when the server is applying an op
    /// to a board nobody has loaded.
    /// </summary>
    internal static class BoardLedger
    {
        internal sealed class Wallet
        {
            public readonly Inventory Inventory;
            public readonly Vector3 Position;
            private readonly ZDO _zdo;
            private readonly bool _detached;

            public Wallet(ZDO zdo)
            {
                _zdo = zdo;
                Position = zdo.GetPosition();
                HiringBoard? board = ZNetScene.instance?.FindInstance(zdo)?.GetComponent<HiringBoard>();
                if (board != null && board.Inventory != null)
                {
                    Inventory = board.Inventory;
                    return;
                }
                _detached = true;
                Inventory = new Inventory(BoardStorage.InventoryName, null, 4, 2);
                string saved = zdo.GetString(ZDOVars.s_items);
                if (saved.Length > 0)
                    Inventory.Load(new ZPackage(saved));
            }

            public Cost Funds => BoardStorage.Totals(Inventory);

            /// <summary>Writes a detached copy back to the ZDO (a loaded Container saves itself).</summary>
            public void Commit()
            {
                if (!_detached)
                    return;
                var pkg = new ZPackage();
                Inventory.Save(pkg);
                _zdo.Set(ZDOVars.s_items, pkg.GetBase64());
            }
        }

        /// <summary>Takes the cost (cheapest food first, then coins) if all of it is there. <paramref name="paid"/> lists what was taken.</summary>
        public static bool TryPay(Wallet wallet, Cost cost, out string paid)
        {
            paid = "";
            Inventory inv = wallet.Inventory;
            var food = inv.GetAllItems().Where(i => !BoardStorage.IsCoins(i) && BoardStorage.IsFood(i) && i.m_dropPrefab != null)
                .GroupBy(i => i.m_dropPrefab.name)
                .Select(g => new FoodStack(g.Key, BoardStorage.PointsPerItem(g.First()), g.Sum(i => i.m_stack)))
                .ToList();
            PaymentPlan plan = FoodPoints.PlanPayment(food, cost.FoodPoints);
            int coins = inv.GetAllItems().Where(BoardStorage.IsCoins).Sum(i => i.m_stack);
            if (!plan.Sufficient || coins < cost.Coins)
                return false;

            var taken = new List<(string, int)>();
            foreach ((string prefab, int count) in plan.Take)
            {
                Remove(inv, prefab, count);
                taken.Add((prefab, count));
            }
            if (cost.Coins > 0)
            {
                Remove(inv, BoardStorage.CoinsPrefab, cost.Coins);
                taken.Add((BoardStorage.CoinsPrefab, cost.Coins));
            }
            paid = Roster.FormatPaid(taken);
            wallet.Commit();
            VfhLog.D(LogCat.Payment, "pay", ("cost", cost.ToString()), ("took", paid), ("overpay", plan.Overpay), ("left", wallet.Funds.ToString()));
            return true;
        }

        /// <summary>Puts items back; anything that doesn't fit is dropped beside the board.</summary>
        public static void Refund(Wallet wallet, string paid)
        {
            Inventory inv = wallet.Inventory;
            foreach ((string prefab, int count) in Roster.ParsePaid(paid))
            {
                GameObject? itemPrefab = ObjectDB.instance.GetItemPrefab(prefab);
                if (itemPrefab == null)
                    continue;
                int stack = Math.Max(1, itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize);
                int left = count;
                while (left > 0)
                {
                    int n = Math.Min(stack, left);
                    if (!inv.AddItem(itemPrefab, n))
                    {
                        ItemDrop.ItemData data = itemPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                        data.m_dropPrefab = itemPrefab;
                        ItemDrop.DropItem(data, n, wallet.Position + Vector3.up, Quaternion.identity);
                    }
                    left -= n;
                }
            }
            wallet.Commit();
            VfhLog.D(LogCat.Payment, "refund", ("items", paid), ("now", wallet.Funds.ToString()));
        }

        private static void Remove(Inventory inv, string prefab, int count)
        {
            ItemDrop.ItemData? any = inv.GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab);
            if (any != null)
                inv.RemoveItem(any.m_shared.m_name, count);
        }
    }
}
