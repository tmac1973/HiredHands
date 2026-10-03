using System.Linq;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Commands
{
    internal static class BoardCommands
    {
        private const float InfoRange = 50f;

        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_board_info",
                "- id, level and funds of the nearest hiring board (within 50m)", false, _ => Info()));
            DebugCommands.DumpStateSections.Add(DumpBoards);
        }

        private static void Info()
        {
            if (Player.m_localPlayer == null)
            {
                VfhCommand.Print("VikingsForHire: no player");
                return;
            }
            HiringBoard? board = HiringBoard.Nearest(Player.m_localPlayer.transform.position, InfoRange);
            if (board == null || board.Zdo == null)
            {
                VfhCommand.Print("VikingsForHire: no hiring board within 50m");
                return;
            }
            Cost funds = board.Inventory != null ? BoardStorage.Totals(board.Inventory) : Cost.Zero;
            float dist = UnityEngine.Vector3.Distance(board.transform.position, Player.m_localPlayer.transform.position);
            VfhCommand.Print($"Hiring board {board.Id}: level {board.Level}, funds {funds.FoodPoints} food pts + {funds.Coins} coins, {dist:0.0}m away, owner {board.Zdo.GetOwner()}");
            VfhLog.I(LogCat.Board, "board.info", ("board", board.Id), ("level", board.Level), ("food", funds.FoodPoints), ("coins", funds.Coins),
                ("dist", dist), ("owner", board.Zdo.GetOwner()));
        }

        private static void DumpBoards()
        {
            var boards = HiringBoard.Loaded.Where(b => b != null && b.Zdo != null).ToList();
            VfhLog.I(LogCat.Board, "dump.boards", ("loaded", boards.Count));
            foreach (HiringBoard b in boards)
            {
                ZDO zdo = b.Zdo!;
                Cost funds = b.Inventory != null ? BoardStorage.Totals(b.Inventory) : Cost.Zero;
                string items = b.Inventory == null ? "" : string.Join(",", b.Inventory.GetAllItems().Select(i => $"{(i.m_dropPrefab != null ? i.m_dropPrefab.name : i.m_shared.m_name)}x{i.m_stack}"));
                VfhLog.I(LogCat.Board, "dump.board", ("board", b.Id), ("level", b.Level), ("pos", b.transform.position), ("zdo", zdo.m_uid.ToString()),
                    ("owner", zdo.GetOwner()), ("food", funds.FoodPoints), ("coins", funds.Coins), ("items", items),
                    ("rosterBytes", zdo.GetByteArray(BoardZdo.Roster)?.Length ?? 0), ("lastUpkeepDay", zdo.GetInt(BoardZdo.LastUpkeepDay)));
            }
        }
    }
}
