using System;
using System.Collections;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Testing
{
    internal static class FixturesUpgrade
    {
        private const float Range = 50f;

        public static void Register()
        {
            Fixtures.Add("upgrade_mats", "<toLevel> - add exactly the cost of upgrading a board to that level to your inventory", UpgradeMats);
            Fixtures.Add("upgrade", "- upgrade the nearest board through the real request (pays from your inventory) and wait for the answer", Upgrade);

            TestHarness.RegisterCheck("inv", "<item> - how many of an item you carry", args =>
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException($"no item {args.ElementAtOrDefault(0)}");
                return Player.m_localPlayer.GetInventory().CountItems(prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name).ToString();
            });
            TestHarness.RegisterCheck("upgrade_last", "- last upgrade outcome: ok, missing, max, busy, conflict, timeout, no_access", _ => BoardUpgrade.LastResult);
        }

        private static IEnumerator UpgradeMats(string[] args)
        {
            if (args.Length != 1 || !int.TryParse(args[0], out int level))
                throw new ArgumentException("usage: upgrade_mats <toLevel>");
            BoardLevelData data = DataStore.Current.BoardLevels.FirstOrDefault(b => b.Level == level) ?? throw new ArgumentException($"no board level {level}");
            Inventory inv = Player.m_localPlayer.GetInventory();
            foreach (var c in data.Cost)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(c.Key) ?? throw new InvalidOperationException($"no item {c.Key}");
                // One stack at a time so items heavier than a stack (e.g. 50 Wood) all land.
                int left = c.Value;
                int stack = Math.Max(1, prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize);
                while (left > 0)
                {
                    int n = Math.Min(left, stack);
                    if (!inv.AddItem(prefab, n))
                        throw new InvalidOperationException($"inventory full adding {c.Key}");
                    left -= n;
                }
            }
            VfhLog.I(LogCat.Test, "fixture.upgrade_mats", ("level", level), ("items", string.Join(" ", data.Cost.Select(c => $"{c.Key}x{c.Value}"))));
            yield return null;
        }

        private static IEnumerator Upgrade(string[] args)
        {
            HiringBoard board = HiringBoard.Nearest(Player.m_localPlayer.transform.position, Range) ?? throw new InvalidOperationException("no hiring board within 50m");
            string outcome = BoardUpgrade.TryUpgrade(board);
            VfhLog.I(LogCat.Test, "fixture.upgrade", ("board", board.Id), ("outcome", outcome));
            for (float waited = 0f; BoardUpgrade.InProgress && waited < 7f; waited += Time.deltaTime)
                yield return null;
            // Let the 2s level poll catch up so board_level reads the new value.
            yield return new WaitForSeconds(0.2f);
        }
    }
}
