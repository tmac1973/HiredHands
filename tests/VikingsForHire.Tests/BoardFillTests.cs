using System.Collections.Generic;
using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class BoardFillTests
    {
        private static readonly Dictionary<string, int> NoRoom = new();

        [Fact]
        public void CheapestFirstWhenItFits()
        {
            BoardFillPlan p = BoardFill.Plan(new[] { new BoardFood("Soup", 60, 100, 10), new BoardFood("Meat", 30, 100, 20) }, 8, NoRoom, 1000);
            Assert.True(p.Enough);
            Assert.Equal(new Dictionary<string, int> { ["Meat"] = 34 }, p.Take);
        }

        [Fact]
        public void RicherFoodWhenCheapFoodWouldNotFit()
        {
            // 8 slots of Meat hold 8 * 20 * 30 = 4800; 7000 wanted: some slots must be Stew (20 * 90 = 1800 a slot).
            BoardFillPlan p = BoardFill.Plan(new[] { new BoardFood("Meat", 30, 500, 20), new BoardFood("Stew", 90, 500, 20) }, 8, NoRoom, 7000);
            Assert.True(p.Enough);
            Assert.True(p.Take.ContainsKey("Meat")); // still as much cheap food as the slots allow
            Assert.True(p.Points >= 7000);
            int slots = (p.Take.GetValueOrDefault("Meat") + 19) / 20 + (p.Take.GetValueOrDefault("Stew") + 19) / 20;
            Assert.True(slots <= 8);
        }

        [Fact]
        public void RichestFirstWhenNothingIsEnough()
        {
            BoardFillPlan p = BoardFill.Plan(new[] { new BoardFood("Berry", 10, 500, 50), new BoardFood("Stew", 90, 40, 20) }, 3, NoRoom, 100000);
            Assert.False(p.Enough);
            Assert.Equal(40, p.Take["Stew"]);
            Assert.Equal(50, p.Take["Berry"]);
            Assert.Equal(40 * 90 + 50 * 10, p.Points);
        }

        [Fact]
        public void RoomInStacksOnTheBoardCostsNoSlot()
        {
            BoardFillPlan p = BoardFill.Plan(new[] { new BoardFood("Meat", 30, 100, 20) }, 0, new Dictionary<string, int> { ["Meat"] = 5 }, 1000);
            Assert.False(p.Enough);
            Assert.Equal(5, p.Take["Meat"]);
        }

        [Fact]
        public void NoSlotsNoFood()
        {
            BoardFillPlan p = BoardFill.Plan(new[] { new BoardFood("Meat", 30, 100, 20) }, 0, NoRoom, 1000);
            Assert.Empty(p.Take);
            Assert.False(p.Enough);
        }
    }
}
