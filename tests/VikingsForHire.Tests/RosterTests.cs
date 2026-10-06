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
        public void OneFarmerAndOneCookPerBoard()
        {
            var r = new Roster();
            Assert.Equal(OpOutcome.Ok, r.Post(Entry("f1", JobType.Farmer), 9));
            Assert.Equal(OpOutcome.JobTaken, r.Post(Entry("f2", JobType.Farmer), 9)); // a pending one counts
            Assert.Equal(OpOutcome.Ok, r.Post(Entry("c1", JobType.Cook), 9));
            Assert.Equal(OpOutcome.Ok, r.Post(Entry("w1"), 9));
            Assert.Equal(OpOutcome.Ok, r.Post(Entry("w2"), 9)); // other jobs aren't limited
            r.Activate("f1", "hf1");
            Assert.Equal(OpOutcome.JobTaken, r.Post(Entry("f3", JobType.Farmer), 9));
            r.Dismiss("hf1");
            Assert.Equal(OpOutcome.Ok, r.Post(Entry("f4", JobType.Farmer), 9)); // one on its way out doesn't
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
        public void GatherSettingsRoundTripAndAreTidied()
        {
            Roster r = WithActive("a");
            Assert.Equal(OpOutcome.Ok, r.SetGather("ha", " Stone,CopperOre,,Stone ", true));
            Assert.Equal("CopperOre,Stone", r.ByHid("ha")!.SkipItems);
            Assert.True(r.ByHid("ha")!.NoHomeWork);
            Assert.Equal(OpOutcome.NotFound, r.SetGather("nobody", "", false));
            var w = new W();
            r.Write(w);
            ContractEntry back = Roster.Read(new R(w.Stream.ToArray())).ByHid("ha")!;
            Assert.Equal("CopperOre,Stone", back.SkipItems);
            Assert.True(back.NoHomeWork);

            var op = new RosterOp { Type = RosterOpType.SetGather, Hid = "ha", SkipItems = "TinOre", NoHomeWork = true };
            var w2 = new W();
            op.Write(w2);
            Assert.Equivalent(op, RosterOp.Read(new R(w2.Stream.ToArray())));

            var h = new HirelingOp { SkipItems = "Wood", NoHomeWork = false };
            var w3 = new W();
            h.Write(w3);
            HirelingOp hb = HirelingOp.Read(new R(w3.Stream.ToArray()));
            Assert.Equal("Wood", hb.SkipItems);
            Assert.False(hb.NoHomeWork);
            Assert.Null(hb.Parked);

            var park = new HirelingOp { Parked = true, Stance = Stance.Defensive };
            var w4 = new W();
            park.Write(w4);
            HirelingOp pb = HirelingOp.Read(new R(w4.Stream.ToArray()));
            Assert.True(pb.Parked);
            Assert.Equal(Stance.Defensive, pb.Stance);
        }

        private static readonly string[] Wood = { "Wood", "FineWood", "RoundLog", "ElderBark" };
        private static readonly string[] Rock = { "Stone", "CopperOre", "TinOre" };

        [Theory]
        [InlineData(new[] { "Wood", "BeechSeeds" }, "W", "Wood")]
        [InlineData(new[] { "Wood", "FineWood" }, "W", "FineWood")]
        [InlineData(new[] { "Wood", "RoundLog", "PineCone" }, "W", "RoundLog")]
        [InlineData(new[] { "Resin" }, "W", null)]
        [InlineData(new[] { "Stone" }, "R", "Stone")]
        [InlineData(new[] { "CopperOre", "Stone" }, "R", "CopperOre")]
        [InlineData(new[] { "Stone", "TinOre" }, "R", "TinOre")]
        public void PrimaryIsTheBestDropOnTheList(string[] drops, string list, string? expected) =>
            Assert.Equal(expected, GatherRules.Primary(drops, list == "W" ? Wood : Rock));

        [Fact]
        public void PostsRoundTripForAnyJob()
        {
            var r = WithActive("a", "b");
            r.Post(Entry("g", JobType.GuardRanged, 3), 9);
            r.Activate("g", "hg");
            // A worker can be posted too (it waits there when it has no work).
            Assert.Equal(OpOutcome.Ok, r.SetPost("ha", new GuardPost { X = 1 }));
            Assert.Equal(OpOutcome.Ok, r.SetPost("hg", new GuardPost { X = 1.5f, Y = 2f, Z = -3f, Yaw = 90f }));
            Assert.Equal(OpOutcome.NotFound, r.SetPost("nobody", new GuardPost()));
            var w = new W();
            r.Write(w);
            Roster back = Roster.Read(new R(w.Stream.ToArray()));
            Assert.Equivalent(r.Entries, back.Entries);
            Assert.Equal(90f, back.ByHid("hg")!.Post!.Yaw);
            Assert.Equal(1f, back.ByHid("ha")!.Post!.X);
            Assert.Null(back.ByHid("hb")!.Post);
            Assert.Equal(OpOutcome.Ok, back.SetPost("hg", null));
            Assert.Null(back.ByHid("hg")!.Post);
        }

        [Fact]
        public void VersionOneRostersStillLoad()
        {
            // A roster saved before guard posts existed (format 1, no post fields).
            var w = new W();
            w.Write((byte)1);
            w.Write(1);
            w.Write("c1"); w.Write("h1"); w.Write("Sigrun"); w.Write((int)JobType.GuardMelee); w.Write(2); w.Write(20f);
            w.Write((int)Stance.Defensive); w.Write((int)ContractState.Active); w.Write(0); w.Write(10.0); w.Write(new byte[] { 1 });
            w.Write(0); w.Write("Coins:5");
            Roster back = Roster.Read(new R(w.Stream.ToArray()));
            Assert.Equal("Sigrun", back.ByHid("h1")!.Name);
            Assert.Null(back.ByHid("h1")!.Post);
        }

        [Fact]
        public void VersionTwoRostersStillLoad()
        {
            // A roster saved by 0.1.x after guard posts (format 2, a post, no gather settings).
            var w = new W();
            w.Write((byte)2);
            w.Write(1);
            w.Write("c1"); w.Write("h1"); w.Write("Ragnhild"); w.Write((int)JobType.GuardRanged); w.Write(3); w.Write(25f);
            w.Write((int)Stance.Aggressive); w.Write((int)ContractState.Active); w.Write(0); w.Write(0.0); w.Write(new byte[] { 1 });
            w.Write(0); w.Write("");
            w.Write(1); w.Write(1f); w.Write(2f); w.Write(3f); w.Write(90f);
            ContractEntry e = Roster.Read(new R(w.Stream.ToArray())).ByHid("h1")!;
            Assert.Equal("Ragnhild", e.Name);
            Assert.Equal(90f, e.Post!.Yaw);
            Assert.Equal("", e.SkipItems);
            Assert.False(e.NoHomeWork);
        }

        [Fact]
        public void RenameChangesTheContractName()
        {
            Roster r = WithActive("a");
            Assert.Equal(OpOutcome.Ok, r.Rename("ha", "Bjorn the Bold"));
            Assert.Equal("Bjorn the Bold", r.ByHid("ha")!.Name);
            Assert.Equal(OpOutcome.BadValue, r.Rename("ha", ""));
            Assert.Equal(OpOutcome.NotFound, r.Rename("nobody", "X"));
        }

        [Theory]
        [InlineData("  Bjorn  ", "Bjorn")]
        [InlineData("Bjorn   the\tBold", "Bjorn the Bold")]
        [InlineData("<color=red>Red</color>", "color=redRed/color")]
        [InlineData("   ", "")]
        [InlineData(null, "")]
        [InlineData("Abcdefghijklmnopqrstuvwxyz", "Abcdefghijklmnopqrstuvwx")]
        [InlineData("Twentythree characters x", "Twentythree characters x")]
        public void NamesAreCleaned(string? raw, string expected) =>
            Assert.Equal(expected, HirelingNames.Clean(raw));

        [Fact]
        public void RenameOpRoundTrips()
        {
            var op = new HirelingOp { Name = "Astrid" };
            var w = new W();
            op.Write(w);
            HirelingOp back = HirelingOp.Read(new R(w.Stream.ToArray()));
            Assert.Equal("Astrid", back.Name);
            Assert.Null(back.Mode);
        }

        [Fact]
        public void OpsRoundTrip()
        {
            var op = new RosterOp { Type = RosterOpType.Post, ContractId = "c", Hid = "h", Name = "Sigrun", Job = JobType.Miner, Level = 3, Radius = 25, Stance = Stance.Defend, Snapshot = new byte[] { 9 } };
            var w = new W();
            op.Write(w);
            Assert.Equivalent(op, RosterOp.Read(new R(w.Stream.ToArray())));

            var orders = new RosterOp { Type = RosterOpType.Orders, Edit = OrderEdit.Pause, OrderItem = "CarrotSeeds", OrderKind = 0, OrderTarget = 12, OrderPaused = true };
            var wo = new W();
            orders.Write(wo);
            Assert.Equivalent(orders, RosterOp.Read(new R(wo.Stream.ToArray())));

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

            var p = new HirelingOp { Post = (1f, 2f, 3f, 45f) };
            var w4 = new W();
            p.Write(w4);
            Assert.Equal((1f, 2f, 3f, 45f), HirelingOp.Read(new R(w4.Stream.ToArray())).Post);
            var c = new HirelingOp { ClearPost = true };
            var w5 = new W();
            c.Write(w5);
            Assert.True(HirelingOp.Read(new R(w5.Stream.ToArray())).ClearPost);

            var setPost = new RosterOp { Type = RosterOpType.SetPost, Hid = "h", Post = new GuardPost { X = 4, Y = 5, Z = 6, Yaw = 7 } };
            var w6 = new W();
            setPost.Write(w6);
            Assert.Equivalent(setPost, RosterOp.Read(new R(w6.Stream.ToArray())));
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
