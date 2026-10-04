using System;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;

namespace VikingsForHire.Board
{
    /// <summary>What the panel (and test fixtures) call to change a board's contracts. Results come back as a message.</summary>
    internal static class BoardContracts
    {
        /// <summary>Contract id of the last contract this client posted (for tests).</summary>
        public static string LastPostedHid { get; private set; } = "";

        /// <summary>Which board each hireling this client hired belongs to, so cheats can address it from far away.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, string> BoardOfHid = new();

        /// <summary>Outcome of the last op this client submitted (for tests).</summary>
        public static string LastOutcome { get; private set; } = "none";

        public static void Post(HiringBoard board, JobType job, int level, float radius, Stance stance, Action<OpResult>? done = null, bool free = false)
        {
            HirelingRecord record = HirelingFactory.NewRecord(job, level, board, board.transform.position);
            record.Radius = radius;
            record.Stance = stance;
            record.Mode = HirelingMode.Working;
            LastPostedHid = record.Hid;
            BoardOfHid[record.Hid] = board.Id;
            var op = new RosterOp
            {
                Type = RosterOpType.Post, Hid = record.Hid, Name = record.Name, Job = job, Level = level, Radius = radius, Stance = stance,
                Snapshot = HirelingSnapshot.Create(record).ToBytes(), Free = free,
            };
            VfhLog.D(LogCat.UI, "contract.post_clicked", ("board", board.Id), ("job", job), ("level", level), ("radius", radius), ("stance", stance));
            MutationService.SubmitBoard(board.Id, op, r => Show(r, done));
        }

        public static void Cancel(HiringBoard board, string contractId, Action<OpResult>? done = null) =>
            MutationService.SubmitBoard(board.Id, new RosterOp { Type = RosterOpType.Cancel, ContractId = contractId }, r => Show(r, done));

        public static void Edit(HiringBoard board, string hid, float radius, Stance stance, Action<OpResult>? done = null) =>
            MutationService.SubmitBoard(board.Id, new RosterOp { Type = RosterOpType.Edit, Hid = hid, Radius = radius, Stance = stance }, r => Show(r, done));

        public static void ClearPost(HiringBoard board, string hid, Action<OpResult>? done = null) =>
            MutationService.SubmitBoard(board.Id, new RosterOp { Type = RosterOpType.SetPost, Hid = hid, Post = null }, r => Show(r, done));

        public static void Promote(HiringBoard board, string hid, int level, Action<OpResult>? done = null) =>
            MutationService.SubmitBoard(board.Id, new RosterOp { Type = RosterOpType.Promote, Hid = hid, Level = level }, r => Show(r, done));

        public static void Dismiss(string boardId, string hid, Action<OpResult>? done = null) =>
            MutationService.SubmitBoard(boardId, new RosterOp { Type = RosterOpType.Dismiss, Hid = hid }, r => Show(r, done));

        private static void Show(OpResult r, Action<OpResult>? done)
        {
            LastOutcome = r.Outcome.ToString();
            if (r.Message.Length > 0)
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, r.Message);
            done?.Invoke(r);
        }
    }
}
