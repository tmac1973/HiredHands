using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>One item of an upgrade cost, resolved against the running game.</summary>
    internal readonly struct UpgradeRequirement
    {
        public readonly string Prefab;
        public readonly ItemDrop Item;
        public readonly int Need;

        public UpgradeRequirement(string prefab, ItemDrop item, int need)
        {
            Prefab = prefab;
            Item = item;
            Need = need;
        }

        public string SharedName => Item.m_itemData.m_shared.m_name;
        public int Have(Inventory inv) => inv.CountItems(SharedName);

        /// <summary>The inventory plus, with AzuCraftyBoxes, the chests near the board it may pull from.</summary>
        public int Have(Inventory inv, Component board) => Have(inv) + Compat.CraftyBoxesCompat.Count(board, SharedName, Prefab);
    }

    /// <summary>
    /// Board upgrades. The client pays first, then asks the board's ZDO owner to apply "level N → N+1". The owner only
    /// accepts if the board is still at N, so two players upgrading at once can't both succeed; the loser gets a full
    /// refund, as does anyone whose request times out.
    /// </summary>
    internal static class BoardUpgrade
    {
        public const string RequestRpc = "VFH_RequestUpgrade";
        public const string ResultRpc = "VFH_UpgradeResult";
        public const string SetLevelRpc = "VFH_SetLevel";
        private const float TimeoutSeconds = 5f;

        private sealed class Pending
        {
            public int Id;
            public ZDOID Board;
            public string BoardId = "";
            public int FromLevel;
            public float SentAt;
            public readonly List<(GameObject Prefab, int Amount)> Paid = new();
        }

        private static Pending? _pending;
        private static int _nextId = 1;

        /// <summary>Outcome of the last upgrade attempt on this client: ok, missing, max, busy, conflict, timeout, no_access.</summary>
        public static string LastResult { get; private set; } = "none";

        public static bool InProgress => _pending != null;

        public static int MaxLevel => DataStore.Current.BoardLevels.Count;

        /// <summary>The cost of going from <paramref name="currentLevel"/> to the next level; empty at max level.</summary>
        public static List<UpgradeRequirement> Requirements(int currentLevel)
        {
            BoardLevelData? next = DataStore.Current.BoardLevels.FirstOrDefault(b => b.Level == currentLevel + 1);
            var list = new List<UpgradeRequirement>();
            if (next == null)
                return list;
            foreach (KeyValuePair<string, int> c in next.Cost)
            {
                GameObject? prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(c.Key) : null;
                ItemDrop? item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (item != null && c.Value > 0)
                    list.Add(new UpgradeRequirement(c.Key, item, c.Value));
            }
            return list;
        }

        public static bool CanAfford(Inventory inv, Component board, List<UpgradeRequirement> reqs) => reqs.All(r => r.Have(inv, board) >= r.Need);

        /// <summary>Called on every client in HiringBoard.Awake.</summary>
        public static void RegisterRpcs(HiringBoard board, ZNetView nview)
        {
            nview.Register<int, int>(RequestRpc, (sender, expected, id) =>
                VfhLog.Guard(LogCat.Board, "upgrade.request_failed", () => OnRequest(board, nview, sender, expected, id)));
            nview.Register<bool, int, int>(ResultRpc, (_, ok, level, id) =>
                VfhLog.Guard(LogCat.Board, "upgrade.result_failed", () => OnResult(board, ok, level, id)));
            nview.Register<int>(SetLevelRpc, (sender, level) =>
                VfhLog.Guard(LogCat.Board, "upgrade.setlevel_failed", () => OnSetLevel(board, nview, sender, level)));
        }

        /// <summary>Pays and sends the request. Returns the immediate outcome ("sent" when the owner will answer).</summary>
        public static string TryUpgrade(HiringBoard board)
        {
            Player player = Player.m_localPlayer;
            if (player == null || board.Zdo == null)
                return Finish("no_access");
            if (_pending != null)
                return Finish("busy");
            if (!PrivateArea.CheckAccess(board.transform.position))
                return Finish("no_access");

            int level = board.Level;
            if (level >= MaxLevel)
                return Finish("max");
            List<UpgradeRequirement> reqs = Requirements(level);
            Inventory inv = player.GetInventory();
            if (!CanAfford(inv, board, reqs))
                return Finish("missing");

            var pending = new Pending
            {
                Id = _nextId++, Board = board.Zdo.m_uid, BoardId = board.Id, FromLevel = level, SentAt = Time.realtimeSinceStartup,
            };
            var fromChests = new List<string>();
            foreach (UpgradeRequirement r in reqs)
            {
                // The inventory first, the rest from nearby chests (AzuCraftyBoxes). A refund always goes to the player.
                int own = Mathf.Min(r.Need, r.Have(inv));
                if (own > 0)
                    inv.RemoveItem(r.SharedName, own);
                int chests = own < r.Need ? Compat.CraftyBoxesCompat.Take(board, r.SharedName, r.Prefab, r.Need - own) : 0;
                if (chests > 0)
                    fromChests.Add($"{r.Prefab}x{chests}");
                pending.Paid.Add((r.Item.gameObject, own + chests));
            }
            _pending = pending;
            LastResult = "sent";
            VfhLog.I(LogCat.Board, "upgrade.requested", ("board", board.Id), ("from", level), ("id", pending.Id),
                ("paid", string.Join(" ", reqs.Select(r => $"{r.Prefab}x{r.Need}"))), ("from_chests", string.Join(" ", fromChests)));

            // When this machine owns the board (single-player, host) the owner's handler and its reply run inside this
            // call, so everything above must already be in place, and nothing after it may overwrite the outcome.
            board.GetComponent<ZNetView>().InvokeRPC(RequestRpc, level, pending.Id);
            return "sent";
        }

        /// <summary>Called every frame: refunds a request the owner never answered.</summary>
        public static void Tick()
        {
            if (_pending == null || Time.realtimeSinceStartup - _pending.SentAt < TimeoutSeconds)
                return;
            VfhLog.W(LogCat.Board, "upgrade.timeout", ("board", _pending.BoardId), ("id", _pending.Id));
            Refund(_pending);
            _pending = null;
            Finish("timeout");
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$vfh_upgrade_timeout");
        }

        public static void RequestSetLevel(HiringBoard board, int level)
        {
            board.GetComponent<ZNetView>().InvokeRPC(SetLevelRpc, level);
            VfhLog.I(LogCat.Board, "upgrade.setlevel_requested", ("board", board.Id), ("to", level));
        }

        // Runs on the board's ZDO owner.
        private static void OnRequest(HiringBoard board, ZNetView nview, long sender, int expected, int id)
        {
            if (!nview.IsOwner())
                return;
            int level = board.Level;
            bool ok = level == expected && level < MaxLevel;
            if (ok)
            {
                level++;
                nview.GetZDO().Set(BoardZdo.Level, level);
            }
            VfhLog.I(LogCat.Board, ok ? "board.upgraded" : "upgrade.rejected", ("board", board.Id), ("from", expected), ("to", level),
                ("by", sender), ("id", id));
            nview.InvokeRPC(sender, ResultRpc, ok, level, id);
        }

        // Runs on the requesting client.
        private static void OnResult(HiringBoard board, bool ok, int level, int id)
        {
            if (_pending == null || _pending.Id != id || _pending.Board != board.Zdo?.m_uid)
                return;
            Pending pending = _pending;
            _pending = null;
            VfhLog.I(LogCat.Board, "upgrade.result", ("board", board.Id), ("ok", ok), ("level", level), ("id", id));
            if (ok)
            {
                Finish("ok");
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$vfh_upgrade_done", level.ToString()));
                GameObject vfx = ZNetScene.instance.GetPrefab("vfx_Place_workbench");
                if (vfx != null)
                    Object.Instantiate(vfx, board.transform.position, Quaternion.identity);
                board.ApplyLevelVisual();
            }
            else
            {
                Refund(pending);
                Finish("conflict");
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$vfh_upgrade_conflict");
            }
        }

        private static void OnSetLevel(HiringBoard board, ZNetView nview, long sender, int level)
        {
            if (!nview.IsOwner())
                return;
            level = Mathf.Clamp(level, 1, MaxLevel);
            nview.GetZDO().Set(BoardZdo.Level, level);
            VfhLog.I(LogCat.Board, "board.level_set", ("board", board.Id), ("to", level), ("by", sender), ("cheat", true));
        }

        private static void Refund(Pending pending)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;
            Inventory inv = player.GetInventory();
            foreach ((GameObject prefab, int amount) in pending.Paid)
            {
                if (inv.AddItem(prefab, amount))
                    continue;
                // Inventory full: drop the rest at the player's feet.
                ItemDrop.ItemData data = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                data.m_dropPrefab = prefab;
                ItemDrop.DropItem(data, amount, player.transform.position + Vector3.up, Quaternion.identity);
            }
            VfhLog.I(LogCat.Board, "upgrade.refunded", ("board", pending.BoardId), ("id", pending.Id),
                ("items", string.Join(" ", pending.Paid.Select(p => $"{p.Prefab.name}x{p.Amount}"))));
        }

        private static string Finish(string result)
        {
            LastResult = result;
            if (result != "ok")
                VfhLog.D(LogCat.Board, "upgrade.outcome", ("result", result));
            return result;
        }
    }
}
