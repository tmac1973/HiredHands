using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Followers;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Testing
{
    /// <summary>Phase 12 (Command Stone, followers) fixtures and checks.</summary>
    internal static class FixturesFollow
    {
        public static void Register()
        {
            Fixtures.Add("stone", "<quality> - put a Command Stone of that quality in your hands", Stone);
            Fixtures.Add("stone_mats", "<quality> - give yourself the materials that quality needs", StoneMats);
            Fixtures.Add("recruit_posted", "- ask the server to recruit the hireling from your last contract (as the stone does)", _ => Op(FollowerServer.Kind.Recruit, Posted()));
            Fixtures.Add("release_posted", "- ask the server to send the hireling from your last contract back to work", _ => Op(FollowerServer.Kind.Release, Posted()));
            Fixtures.Add("release_all", "- ask the server to send every follower that's home back to work", _ => Op(FollowerServer.Kind.ReleaseAll, ""));

            TestHarness.RegisterCheck("followers", "- how many followers you have (server's count)",
                args => FollowerServer.FollowersOf(long.Parse(args.ElementAtOrDefault(0) ?? "0", CultureInfo.InvariantCulture)).Count.ToString(),
                serverSide: true,
                prepareArgs: _ => new[] { (Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L).ToString(CultureInfo.InvariantCulture) });
            TestHarness.RegisterCheck("craftable", "<quality> - whether you could make that Command Stone quality at the nearest workbench right now", args =>
            {
                int q = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
                Recipe recipe = CommandStoneItem.Recipe ?? throw new InvalidOperationException("Command Stone recipe not registered");
                Inventory inv = Player.m_localPlayer.GetInventory();
                bool mats = recipe.m_resources.All(r => r.m_resItem == null || inv.CountItems(r.m_resItem.m_itemData.m_shared.m_name) >= r.GetAmount(q));
                CraftingStation? bench = Workbench();
                bool board = StoneCraftingGate.BoardOk(q, bench, out _);
                VfhLog.I(LogCat.Test, "check.craftable", ("quality", q), ("materials", mats), ("board", board), ("workbench", bench != null));
                return mats && board ? "true" : "false";
            });
        }

        private static string Posted()
        {
            string hid = BoardContracts.LastPostedHid;
            if (hid.Length == 0)
                throw new InvalidOperationException("no contract posted yet");
            return hid;
        }

        private static int StoneQuality() =>
            Player.m_localPlayer.GetInventory().GetAllItems().Where(CommandStoneItem.IsStone).Select(i => i.m_quality).DefaultIfEmpty(0).Max();

        private static IEnumerator Op(FollowerServer.Kind kind, string hid)
        {
            int quality = StoneQuality();
            FollowerServer.Send(kind, hid, quality);
            VfhLog.I(LogCat.Test, "fixture.follow_op", ("kind", kind), ("hid", hid), ("quality", quality));
            yield return new WaitForSeconds(1f); // the server's answer and the ZDO change
        }

        private static IEnumerator Stone(string[] args)
        {
            int q = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            Player p = Player.m_localPlayer;
            Inventory inv = p.GetInventory();
            foreach (ItemDrop.ItemData old in inv.GetAllItems().Where(CommandStoneItem.IsStone).ToList())
                inv.RemoveItem(old);
            ItemDrop.ItemData? stone = inv.AddItem(CommandStoneItem.PrefabName, 1, q, 0, p.GetPlayerID(), p.GetPlayerName(), false);
            if (stone == null)
                throw new InvalidOperationException("couldn't add a Command Stone (inventory full?)");
            p.EquipItem(stone);
            VfhLog.I(LogCat.Test, "fixture.stone", ("quality", q), ("equipped", p.GetRightItem() == stone));
            yield return null;
        }

        private static IEnumerator StoneMats(string[] args)
        {
            int q = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            Recipe recipe = CommandStoneItem.Recipe ?? throw new InvalidOperationException("Command Stone recipe not registered");
            Inventory inv = Player.m_localPlayer.GetInventory();
            foreach (Piece.Requirement r in recipe.m_resources)
            {
                int n = r.GetAmount(q);
                if (r.m_resItem != null && n > 0)
                    inv.AddItem(r.m_resItem.gameObject, n);
            }
            VfhLog.I(LogCat.Test, "fixture.stone_mats", ("quality", q));
            yield return null;
        }

        private static CraftingStation? Workbench() =>
            UnityEngine.Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None)
                .Where(s => s.m_name == "$piece_workbench" && Vector3.Distance(s.transform.position, Player.m_localPlayer.transform.position) < 20f)
                .OrderBy(s => Vector3.Distance(s.transform.position, Player.m_localPlayer.transform.position)).FirstOrDefault();
    }
}
