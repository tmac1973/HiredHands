using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class DepositPlannerTests
    {
        private static int Stack(string prefab) => prefab == "Wood" ? 50 : 100;

        private static ChestInfo Chest(string id, float dist, int empty, params (string, int)[] stacks) => new(id, dist, stacks, empty);

        [Fact]
        public void EachTypeGoesOnlyToChestsHoldingIt()
        {
            var cargo = new Dictionary<string, int> { ["Wood"] = 40, ["BeechSeeds"] = 6 };
            var chests = new[]
            {
                Chest("A", 5, 10, ("Wood", 10)),
                Chest("B", 8, 10, ("BeechSeeds", 1)),
                Chest("C", 2, 20),
            };
            DepositPlan plan = DepositPlanner.Plan(cargo, chests, Stack);
            Assert.Contains(new DepositStep("A", "Wood", 40), plan.Steps);
            Assert.Contains(new DepositStep("B", "BeechSeeds", 6), plan.Steps);
            Assert.DoesNotContain(plan.Steps, s => s.ChestId == "C");
            Assert.Empty(plan.ToDropPile);
        }

        [Fact]
        public void OverflowGoesToDropPile()
        {
            var cargo = new Dictionary<string, int> { ["Wood"] = 120 };
            // 40 room in the stack + one empty slot of 50 = 90.
            DepositPlan plan = DepositPlanner.Plan(cargo, new[] { Chest("A", 5, 1, ("Wood", 10)) }, Stack);
            Assert.Equal(90, plan.Steps.Single().Amount);
            Assert.Equal(30, plan.ToDropPile["Wood"]);
        }

        [Fact]
        public void NoMatchingChestDropsEverything()
        {
            var cargo = new Dictionary<string, int> { ["CopperOre"] = 12 };
            DepositPlan plan = DepositPlanner.Plan(cargo, new[] { Chest("A", 1, 30) }, Stack);
            Assert.Empty(plan.Steps);
            Assert.Equal(12, plan.ToDropPile["CopperOre"]);
        }

        [Fact]
        public void NearestChestFirstThenNext()
        {
            var cargo = new Dictionary<string, int> { ["Wood"] = 70 };
            var chests = new[] { Chest("far", 20, 5, ("Wood", 1)), Chest("near", 3, 0, ("Wood", 30)) };
            DepositPlan plan = DepositPlanner.Plan(cargo, chests, Stack);
            Assert.Equal(new[] { new DepositStep("near", "Wood", 20), new DepositStep("far", "Wood", 50) }, plan.Steps);
        }

        [Fact]
        public void SharedEmptySlotsAreNotDoubleBooked()
        {
            var cargo = new Dictionary<string, int> { ["Wood"] = 50, ["Stone"] = 100 };
            // One empty slot, both types already present with full stacks: only one type can use the slot.
            DepositPlan plan = DepositPlanner.Plan(cargo, new[] { Chest("A", 1, 1, ("Wood", 50), ("Stone", 100)) }, Stack);
            Assert.Single(plan.Steps);
            Assert.Single(plan.ToDropPile);
        }
    }
}
