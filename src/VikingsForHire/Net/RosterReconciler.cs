using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Net
{
    /// <summary>
    /// Server, every minute: a board's contract whose hireling no longer exists anywhere in the world (the server holds
    /// every ZDO) for 10 minutes is settled, so a board never keeps an "away" hireling forever. An active contract is
    /// marked lost: it comes back after the usual walk-in time, free (a hireling deleted mid-trip by another mod isn't
    /// dead); a leaving one is removed (it had gone).
    /// </summary>
    internal static class RosterReconciler
    {
        private const float TickSeconds = 60f;
        private const float MissingSeconds = 600f;
        private static readonly Dictionary<string, float> MissingSince = new();
        private static float _next;

        public static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Time.realtimeSinceStartup < _next)
                return;
            _next = Time.realtimeSinceStartup + TickSeconds;
            float now = Time.realtimeSinceStartup;
            var seen = new HashSet<string>();
            foreach (ZDO board in WorldIndex.AllBoards().ToList())
            {
                string boardId = board.GetString(BoardZdo.Id);
                Roster roster = BoardRosterOps.Read(board);
                foreach (ContractEntry e in roster.Entries.Where(e => e.State != ContractState.Pending && e.Hid.Length > 0))
                {
                    if (WorldIndex.Hireling(e.Hid) != null)
                        continue;
                    seen.Add(e.Hid);
                    if (!MissingSince.TryGetValue(e.Hid, out float since))
                    {
                        MissingSince[e.Hid] = now;
                        VfhLog.D(LogCat.Roster, "roster.hireling_missing", ("board", boardId), ("hid", e.Hid), ("name", e.Name), ("state", e.State));
                        continue;
                    }
                    if (now - since < MissingSeconds)
                        continue;
                    MissingSince.Remove(e.Hid);
                    // Gone without a death on record: lost (deleted by something), not dead, so it comes back soon and free.
                    // A death whose news was lost would have left its grave; this can't tell the two apart, and losing a
                    // hireling to someone else's bug shouldn't cost 3 days or a permadeath.
                    RosterOp op = e.State == ContractState.Leaving
                        ? new RosterOp { Type = RosterOpType.Remove, Hid = e.Hid }
                        : new RosterOp { Type = RosterOpType.MarkLost, Hid = e.Hid, Name = e.Name };
                    VfhLog.W(LogCat.Roster, "roster.settled_missing", ("board", boardId), ("hid", e.Hid), ("name", e.Name), ("state", e.State), ("as", op.Type));
                    MutationService.SubmitBoard(boardId, op);
                }
            }
            foreach (string hid in MissingSince.Keys.Where(k => !seen.Contains(k)).ToList())
                MissingSince.Remove(hid);
        }
    }
}
