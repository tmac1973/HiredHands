using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core.Orders;
using Xunit;

namespace VikingsForHire.Tests
{
    public class OrdersTests
    {
        private static OrderList Orders(params (string Item, OrderKind Kind, int Target)[] orders)
        {
            var list = new OrderList();
            foreach (var o in orders)
                list.Add(o.Item, o.Kind, o.Target);
            return list;
        }

        private static readonly List<CropInfo> Crops = new()
        {
            new CropInfo { Plant = "sapling_carrot", Consumes = "CarrotSeeds", Yields = "Carrot", YieldPerPlant = 1, Level = 2 },
            new CropInfo { Plant = "sapling_seedcarrot", Consumes = "Carrot", Yields = "CarrotSeeds", YieldPerPlant = 3, Level = 2 },
            new CropInfo { Plant = "sapling_barley", Consumes = "Barley", Yields = "Barley", YieldPerPlant = 2, Level = 5 },
            new CropInfo { Plant = "RaspberryBush", Yields = "Raspberry", YieldPerPlant = 1, Regrowing = true, Level = 1 },
        };

        private static Dictionary<string, int> D(params (string, int)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

        private static Dictionary<string, int> Spots(int n) => Crops.ToDictionary(c => c.Plant, _ => n);

        [Fact]
        public void SeedOrdersComeFirstAndMoveWithinTheirGroup()
        {
            OrderList l = Orders(("Carrot", OrderKind.Crop, 10), ("CookedMeat", OrderKind.Kitchen, 5), ("CarrotSeeds", OrderKind.Seed, 6));
            Assert.Equal(new[] { "CarrotSeeds", "Carrot", "CookedMeat" }, l.Active().Select(o => o.Item));
            Assert.True(l.Move("CarrotSeeds", -1)); // up past Carrot (same group), skipping nothing else
            Assert.Equal("CarrotSeeds", l.Orders[0].Item);
            Assert.False(l.Move("CookedMeat", -1)); // no other kitchen order above
            l.SetPaused("Carrot", true);
            Assert.DoesNotContain(l.Active(), o => o.Item == "Carrot");
        }

        [Fact]
        public void SerializeRoundTripsAndJunkIsEmpty()
        {
            OrderList l = Orders(("CarrotSeeds", OrderKind.Seed, 6), ("Bread", OrderKind.Kitchen, 10));
            l.SetPaused("Bread", true);
            OrderList back = OrderList.Parse(l.Serialize());
            Assert.Equal(2, back.Orders.Count);
            Assert.Equal(OrderKind.Seed, back.Find("CarrotSeeds")!.Kind);
            Assert.True(back.Find("Bread")!.Paused);
            Assert.Empty(OrderList.Parse("garbage").Orders);
            Assert.Empty(OrderList.Parse("v9|x:1:s:0").Orders);
            Assert.Single(OrderList.Parse("v1|a:1:s:0;bad;b:x:c:0;c:3:z:0").Orders);
            Assert.False(l.Add("bad:name", OrderKind.Crop, 1));
        }

        [Fact]
        public void SeedCycleRaisesSeedsBeforeCarrots()
        {
            OrderList l = Orders(("CarrotSeeds", OrderKind.Seed, 6), ("Carrot", OrderKind.Crop, 6));
            FarmPlan p = FarmPlanner.Plan(Crops, l, D(("Carrot", 2)), D(), 2, Spots(20));
            Assert.Single(p.Plant);
            Assert.Equal("sapling_seedcarrot", p.Plant[0].Crop.Plant);
            Assert.Equal(2, p.Plant[0].Count); // 2 carrots -> 6 seeds
            Assert.Equal(2, p.PlantedForSeeds["Carrot"]);
            Assert.Contains("$vfh_need_seed|Carrot|CarrotSeeds", p.Missing);
        }

        [Fact]
        public void CarrotsOnlyFromSeedsAboveTheReserve()
        {
            OrderList l = Orders(("CarrotSeeds", OrderKind.Seed, 6), ("Carrot", OrderKind.Crop, 20));
            FarmPlan p = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 10)), D(), 2, Spots(20));
            Assert.Equal(("sapling_carrot", 4), (p.Plant.Single().Crop.Plant, p.Plant.Single().Count));
            FarmPlan atReserve = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 6)), D(), 2, Spots(20));
            Assert.Empty(atReserve.Plant);
            Assert.Contains("$vfh_need_seed_reserve|Carrot|CarrotSeeds", atReserve.Missing);
            Assert.Equal(6, atReserve.Protected()["CarrotSeeds"]);
        }

        [Fact]
        public void GrowingCountsSpotsCapAndLevelsGate()
        {
            OrderList l = Orders(("Carrot", OrderKind.Crop, 10));
            Assert.Empty(FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 50)), D(("Carrot", 10)), 2, Spots(20)).Plant);
            Assert.Equal(3, FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 50)), D(), 2, Spots(3)).Plant.Single().Count);
            Assert.Contains("$vfh_need_field", FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 50)), D(), 2, Spots(0)).Missing);
            Assert.Contains("$vfh_need_farmer_level|Carrot|2", FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 50)), D(), 1, Spots(9)).Missing);
        }

        [Fact]
        public void BarleyReplantsFromItselfAndBushesArePickedWhileShort()
        {
            OrderList l = Orders(("Barley", OrderKind.Crop, 10), ("Raspberry", OrderKind.Crop, 5));
            FarmPlan p = FarmPlanner.Plan(Crops, l, D(("Barley", 2)), D(), 5, Spots(20));
            Assert.Equal(2, p.Plant.Single().Count);
            Assert.Contains("Raspberry", p.PickRegrowing);
            Assert.DoesNotContain(p.Plant, x => x.Crop.Regrowing);
            Assert.DoesNotContain("Raspberry", FarmPlanner.Plan(Crops, l, D(("Raspberry", 5)), D(), 5, Spots(20)).PickRegrowing);
            Assert.False(CropInfo.IsSeedItem("Barley", Crops, _ => false));
            Assert.True(CropInfo.IsSeedItem("CarrotSeeds", Crops, _ => false));
            Assert.False(CropInfo.IsSeedItem("Carrot", Crops, i => i == "Carrot"));
        }

        private static readonly List<KitchenInfo> Kitchen = new()
        {
            new KitchenInfo { Output = "CookedMeat", Station = "piece_cookingstation", Kind = StationKind.Stove, Inputs = new() { ["RawMeat"] = 1 }, Level = 1 },
            new KitchenInfo { Output = "CarrotSoup", Station = "piece_cauldron", Kind = StationKind.Craft, Inputs = new() { ["Mushroom"] = 1, ["Carrot"] = 3 }, Level = 2 },
            new KitchenInfo { Output = "BreadDough", OutputAmount = 2, Station = "piece_preptable", Kind = StationKind.Craft, Inputs = new() { ["BarleyFlour"] = 10 }, Level = 6 },
            new KitchenInfo { Output = "Bread", Station = "piece_oven", Kind = StationKind.Stove, Inputs = new() { ["BreadDough"] = 1 }, Level = 5 },
        };

        private static readonly HashSet<string> AllStations = new() { "piece_cookingstation", "piece_cauldron", "piece_preptable", "piece_oven" };

        [Fact]
        public void CooksTheFirstShortOrder()
        {
            OrderList l = Orders(("CookedMeat", OrderKind.Kitchen, 4));
            KitchenResult r = KitchenPlanner.Next(Kitchen, l, D(("RawMeat", 10), ("CookedMeat", 1)), D(), 1, AllStations);
            Assert.Equal("CookedMeat", r.Task!.Info.Output);
            Assert.Equal(3, r.Task.Batches);
            Assert.Null(KitchenPlanner.Next(Kitchen, l, D(("RawMeat", 10), ("CookedMeat", 4)), D(), 1, AllStations).Task);
        }

        [Fact]
        public void ProtectedCarrotsArentUsedForSoup()
        {
            OrderList l = Orders(("CarrotSoup", OrderKind.Kitchen, 5));
            KitchenResult r = KitchenPlanner.Next(Kitchen, l, D(("Carrot", 3), ("Mushroom", 10)), D(("Carrot", 3)), 2, AllStations);
            Assert.Null(r.Task);
            Assert.Equal("$vfh_need_ingredient|CarrotSoup|3|Carrot", r.Missing);
            Assert.NotNull(KitchenPlanner.Next(Kitchen, l, D(("Carrot", 3), ("Mushroom", 10)), D(), 2, AllStations).Task);
        }

        [Fact]
        public void BreadChainsThroughDoughAndLevelsGate()
        {
            OrderList l = Orders(("Bread", OrderKind.Kitchen, 2));
            KitchenResult r = KitchenPlanner.Next(Kitchen, l, D(("BarleyFlour", 20)), D(), 6, AllStations);
            Assert.Equal("BreadDough", r.Task!.Info.Output);
            Assert.Equal(1, r.Task.Batches);
            Assert.Equal("Bread", r.Task.Then!.Info.Output);
            Assert.Equal(2, r.Task.Then.Batches);
            Assert.Equal("$vfh_need_cook_level|Bread|5", KitchenPlanner.Next(Kitchen, l, D(("BreadDough", 5)), D(), 4, AllStations).Missing);
            Assert.Equal("$vfh_need_kitchen|Bread", KitchenPlanner.Next(Kitchen, l, D(("BreadDough", 5)), D(), 6, new HashSet<string>()).Missing);
        }
    }
}
