using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using Xunit;
using W = VikingsForHire.Tests.HirelingRecordTests.Writer;
using R = VikingsForHire.Tests.HirelingRecordTests.Reader;

namespace VikingsForHire.Tests
{
    public class RosterTests
    {
        private static ContractEntry Entry(string id, JobType job = JobType.Woodcutter, int level = 1) =>
            new() { ContractId = id, Name = "N" + id, Job = job, Level = level, Radius = 20f, Stance = StanceRules.Default(job), Snapshot = new byte[] { 1, 2, 3 }, Paid = "CookedMeat:4" };

        private static Roster WithActive(params string[] ids)
        {
            var r = new Roster();
            foreach (string id in ids)
            {
                r.Post(Entry(id), 99);
                r.Activate(id, "h" + id);
            }
            return r;
        }

        [Fact]
        public void PostRespectsCapIncludingPendingAndLeaving()
        {
            var r = WithActive("a");
            r.Dismiss("ha");
            Assert.Equal(OpOutcome.Ok, r.Post(Entry("b"), 2));
            Assert.Equal(OpOutcome.CapReached, r.Post(Entry("c"), 2));
            Assert.Equal(2, r.Count);
            Assert.Equal(1, r.Pending);
            Assert.Equal(1, r.Leaving);
        }

        [Fact]
        public void ActivateMovesPendingToActive()
        {
            var r = new Roster();
            r.Post(Entry("a"), 5);
            Assert.Equal(OpOutcome.Ok, r.Activate("a", "hid1"));
            Assert.Equal(ContractState.Active, r.ByHid("hid1")!.State);
            Assert.Equal(OpOutcome.WrongState, r.Activate("a", "hid1"));
            Assert.Equal(OpOutcome.NotFound, r.Activate("zz", "x"));
        }

        [Fact]
        public void CancelOnlyPendingAndReturnsPayment()
        {
            var r = WithActive("a");
            r.Post(Entry("b"), 5);
            Assert.Null(r.CancelPending("a"));
            ContractEntry? cancelled = r.CancelPending("b");
            Assert.NotNull(cancelled);
            Assert.Equal(new List<(string, int)> { ("CookedMeat", 4) }, Roster.ParsePaid(cancelled!.Paid));
            Assert.Equal(1, r.Count);
        }

        [Fact]
        public void PromoteChecksLevels()
        {
            var r = WithActive("a");
            Assert.Equal(OpOutcome.BadLevel, r.Promote("ha", 1, 4));
            Assert.Equal(OpOutcome.BadLevel, r.Promote("ha", 5, 4));
            Assert.Equal(OpOutcome.Ok, r.Promote("ha", 3, 4));
            Assert.Equal(3, r.ByHid("ha")!.Level);
        }

        [Fact]
        public void EditRejectsStanceOutsideJob()
        {
            var r = WithActive("a");
            Assert.Equal(OpOutcome.BadValue, r.Edit("ha", 25f, Stance.Aggressive));
            Assert.Equal(OpOutcome.Ok, r.Edit("ha", 25f, Stance.Flee));
            Assert.Equal(Stance.Flee, r.ByHid("ha")!.Stance);
        }

        [Fact]
        public void UpkeepAllOrNothingInOrderAndLeavingAfterLimit()
        {
            var r = WithActive("a", "b");
            int wallet = 1; // enough for one hireling's day
            UpkeepResult day1 = r.ApplyUpkeepDay(_ => wallet-- > 0, 2);
            Assert.Equal(new[] { "ha" }, day1.Paid);
            Assert.Equal(new[] { ("hb", 1) }, day1.Unpaid);

            r.ApplyUpkeepDay(_ => false, 2);
            UpkeepResult day3 = r.ApplyUpkeepDay(_ => false, 2);
            Assert.Contains("hb", day3.NowLeaving);
            Assert.Equal(ContractState.Leaving, r.ByHid("hb")!.State);
            Assert.Equal(2, r.ByHid("ha")!.UnpaidDays);

            UpkeepResult paid = r.ApplyUpkeepDay(_ => true, 2);
            Assert.Equal(new[] { "ha" }, paid.Paid); // leaving hirelings aren't charged
            Assert.Equal(0, r.ByHid("ha")!.UnpaidDays);
        }

        [Fact]
        public void DeathPermadeathRemovesOtherwiseRespawns()
        {
            var r = WithActive("a", "b");
            Assert.Equal(OpOutcome.Ok, r.MarkDied("ha", true, 100, 600));
            Assert.Null(r.ByHid("ha"));
            Assert.Equal(OpOutcome.Ok, r.MarkDied("hb", false, 100, 600));
            ContractEntry b = r.ByHid("hb")!;
            Assert.Equal(ContractState.Pending, b.State);
            Assert.True(b.RespawnPending);
            Assert.Equal(700, b.ArriveAt);
        }

        [Fact]
        public void VoidAndRemove()
        {
            var r = WithActive("a", "b");
            Assert.True(r.RemoveEntry("ha"));
            Assert.False(r.RemoveEntry("ha"));
            Assert.Single(r.VoidBoard());
            Assert.Equal(0, r.Count);
        }

        [Fact]
        public void RosterRoundTrip()
        {
            var r = WithActive("a");
            r.Post(Entry("b", JobType.GuardRanged, 4), 9);
            r.ByContract("b")!.ArriveAt = 1234.5;
            var w = new W();
            r.Write(w);
            Roster back = Roster.Read(new R(w.Stream.ToArray()));
            Assert.Equal(2, back.Count);
            Assert.Equivalent(r.Entries, back.Entries);
        }

        [Fact]
        public void OpsRoundTrip()
        {
            var op = new RosterOp { Type = RosterOpType.Post, ContractId = "c", Hid = "h", Name = "Sigrun", Job = JobType.Miner, Level = 3, Radius = 25, Stance = Stance.Defend, Snapshot = new byte[] { 9 } };
            var w = new W();
            op.Write(w);
            Assert.Equivalent(op, RosterOp.Read(new R(w.Stream.ToArray())));

            var h = new HirelingOp { Mode = HirelingMode.Leaving, LeavingSince = 99, Status = "$x" };
            var w2 = new W();
            h.Write(w2);
            HirelingOp back = HirelingOp.Read(new R(w2.Stream.ToArray()));
            Assert.Equal(HirelingMode.Leaving, back.Mode);
            Assert.Equal(99, back.LeavingSince);
            Assert.Null(back.Level);
            Assert.Equal("$x", back.Status);

            var f = new HirelingOp { Mode = HirelingMode.Following, Owner = 123456789012L, OwnerName = "Tim", FollowMode = FollowMode.Stay,
                StayPos = (1.5f, 2f, -3.25f), DeliverPending = true };
            var w3 = new W();
            f.Write(w3);
            HirelingOp f2 = HirelingOp.Read(new R(w3.Stream.ToArray()));
            Assert.Equal(HirelingMode.Following, f2.Mode);
            Assert.Equal(123456789012L, f2.Owner);
            Assert.Equal("Tim", f2.OwnerName);
            Assert.Equal(FollowMode.Stay, f2.FollowMode);
            Assert.Equal((1.5f, 2f, -3.25f), f2.StayPos);
            Assert.True(f2.DeliverPending);
            Assert.Null(f2.Stance);
        }

        [Fact]
        public void PaidStringsRoundTrip()
        {
            var items = new List<(string, int)> { ("CookedMeat", 3), ("Coins", 50) };
            Assert.Equal(items, Roster.ParsePaid(Roster.FormatPaid(items)));
            Assert.Empty(Roster.ParsePaid("garbage;x:y"));
        }
    }
}
