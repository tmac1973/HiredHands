using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class SmelterPlannerTests
    {
        private static StationState Smelter(string id, int queue = 0, float fuel = 0, float dist = 5) => new()
        {
            Id = id, Prefab = "smelter", Distance = dist, Queue = queue, MaxOre = 10, Fuel = fuel, MaxFuel = 20,
            FuelItem = "Coal", Inputs = new[] { "CopperOre", "TinOre" },
        };

        private static StationState Kiln(string id, int queue = 0) => new()
        {
            Id = id, Prefab = "charcoal_kiln", Distance = 5, Queue = queue, MaxOre = 25, Inputs = new[] { "Wood" },
        };

        private static Dictionary<string, int> D(params (string, int)[] items) => items.ToDictionary(i => i.Item1, i => i.Item2);

        private static SmelterPlan Plan(IReadOnlyList<StationState> stations, Dictionary<string, int> stock, Dictionary<string, int>? cargo = null, int slots = 8) =>
            SmelterPlanner.Plan(stations, stock, cargo ?? D(), 0.5f, slots, _ => 50);

        [Fact]
        public void EmptySmelterIsFilledFromChests()
        {
            SmelterPlan p = Plan(new[] { Smelter("s1") }, D(("CopperOre", 30), ("Coal", 40)));
            Assert.Contains(p.Loads, l => l.StationId == "s1" && l.Prefab == "CopperOre" && l.Amount == 10 && !l.IsFuel);
            Assert.Contains(p.Loads, l => l.StationId == "s1" && l.Prefab == "Coal" && l.Amount == 20 && l.IsFuel);
            Assert.Equal(10, p.Fetch["CopperOre"]);
            Assert.Equal(20, p.Fetch["Coal"]);
        }

        [Fact]
        public void StationAboveThresholdIsLeftAlone()
        {
            SmelterPlan p = Plan(new[] { Smelter("s1", queue: 6, fuel: 12) }, D(("CopperOre", 30), ("Coal", 40)));
            Assert.True(p.Idle);
            Assert.Empty(p.Fetch);
        }

        [Fact]
        public void LowFuelAloneTriggersATopUpOfBoth()
        {
            SmelterPlan p = Plan(new[] { Smelter("s1", queue: 8, fuel: 3) }, D(("CopperOre", 30), ("Coal", 40)));
            Assert.Contains(p.Loads, l => l.Prefab == "Coal" && l.Amount == 17);
            Assert.Contains(p.Loads, l => l.Prefab == "CopperOre" && l.Amount == 2);
        }

        [Fact]
        public void EmptiestStationFirst()
        {
            SmelterPlan p = Plan(new[] { Smelter("half", queue: 4, fuel: 8), Smelter("empty", dist: 20) }, D(("CopperOre", 100), ("Coal", 100)));
            Assert.Equal("empty", p.Loads[0].StationId);
        }

        [Fact]
        public void CargoIsUsedBeforeChests()
        {
            SmelterPlan p = Plan(new[] { Smelter("s1") }, D(("CopperOre", 30), ("Coal", 40)), D(("CopperOre", 4), ("Coal", 20)));
            Assert.Equal(6, p.Fetch["CopperOre"]);
            Assert.False(p.Fetch.ContainsKey("Coal"));
        }

        [Fact]
        public void NoFuelAnywhereMeansStarvedNotHalfLoaded()
        {
            SmelterPlan p = Plan(new[] { Smelter("s1") }, D(("CopperOre", 30)));
            Assert.True(p.Idle);
            Assert.Equal(new[] { "s1" }, p.Starved);
        }

        [Fact]
        public void KilnNeedsNoFuel()
        {
            SmelterPlan p = Plan(new[] { Kiln("k1") }, D(("Wood", 100), ("FineWood", 50)));
            Assert.Single(p.Loads);
            Assert.Equal("Wood", p.Loads[0].Prefab);
            Assert.Equal(25, p.Loads[0].Amount);
        }

        [Fact]
        public void TwoStationsShareLimitedStock()
        {
            SmelterPlan p = Plan(new[] { Smelter("a"), Smelter("b", dist: 9) }, D(("CopperOre", 12), ("Coal", 100)));
            Assert.Equal(12, p.Loads.Where(l => l.Prefab == "CopperOre").Sum(l => l.Amount));
        }

        [Fact]
        public void FetchIsLimitedByCargoSlotsAndSharedWithFuel()
        {
            // One free slot of 50: ore and coal alternate a stack at a time, so ore takes it and coal gets none,
            // but with two slots both fit.
            SmelterPlan one = Plan(new[] { Smelter("s1") }, D(("CopperOre", 30), ("Coal", 40)), slots: 1);
            Assert.Equal(10, one.Fetch.Values.Sum());
            SmelterPlan two = Plan(new[] { Smelter("s1") }, D(("CopperOre", 30), ("Coal", 40)), slots: 2);
            Assert.Equal(10, two.Fetch["CopperOre"]);
            Assert.Equal(20, two.Fetch["Coal"]);
        }

        [Fact]
        public void PicksTheInputWithMostOnHand()
        {
            SmelterPlan p = Plan(new[] { Smelter("s1") }, D(("CopperOre", 3), ("TinOre", 20), ("Coal", 40)));
            Assert.Contains(p.Loads, l => l.Prefab == "TinOre" && l.Amount == 10);
        }
    }
}
