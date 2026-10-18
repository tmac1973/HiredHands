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
        public void StationOrdersAreCapsNotWork()
        {
            OrderList l = Orders(("Coal", OrderKind.Station, 200), ("Carrot", OrderKind.Crop, 10), ("Copper", OrderKind.Station, 50));
            Assert.Equal(200, l.StationCap("Coal"));
            Assert.Null(l.StationCap("Iron"));
            Assert.Null(l.StationCap("Carrot"));
            Assert.Equal(new[] { "Carrot" }, l.Active().Select(o => o.Item)); // the Farmer and Cook don't see them
            Assert.False(l.Find("Coal")!.IsFarm);
            Assert.True(l.Move("Copper", -1)); // past Coal, its own group, not Carrot
            Assert.Equal(new[] { "Copper", "Carrot", "Coal" }, l.Orders.Select(o => o.Item));
            l.SetPaused("Coal", true);
            Assert.Equal(0, l.StationCap("Coal"));
            OrderList back = OrderList.Parse(l.Serialize());
            Assert.Equal(OrderKind.Station, back.Find("Coal")!.Kind);
            Assert.Equal(0, back.StationCap("Coal"));
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
            // At the reserve with carrots to plant for seeds, the reserve holds (spares are grown instead; see below).
            FarmPlan atReserve = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 6), ("Carrot", 2)), D(), 2, Spots(20));
            Assert.DoesNotContain(atReserve.Plant, x => x.Crop.Plant == "sapling_carrot");
            Assert.Contains("$vfh_need_seed_reserve|Carrot|CarrotSeeds", atReserve.Missing);
            Assert.Equal(6, atReserve.Protected()["CarrotSeeds"]);
            // At the reserve with no carrots anywhere (the Cook used them): half the seeds go into carrots (Grima's onions).
            FarmPlan stuck = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 6)), D(), 2, Spots(20));
            Assert.Equal(("sapling_carrot", 3), (stuck.Plant.Single().Crop.Plant, stuck.Plant.Single().Count));
        }

        [Fact]
        public void SeedsAtTheReserveGrowSparesForTheProduce()
        {
            // Seeds exactly at their reserve and carrots short: plant carrots for seeds (spares above the reserve),
            // never the reserve itself.
            OrderList l = Orders(("CarrotSeeds", OrderKind.Seed, 6), ("Carrot", OrderKind.Crop, 9));
            FarmPlan p = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 6), ("Carrot", 5)), D(), 2, Spots(20));
            Assert.Equal(("sapling_seedcarrot", 2), (p.Plant.Single().Crop.Plant, p.Plant.Single().Count)); // 4 carrots short: 2 seed carrots, 6 seeds
            Assert.Equal(6, p.Protected()["CarrotSeeds"]);
            // Once the spares are growing nothing more is planted for seeds; once harvested, carrots are planted from them.
            Assert.Empty(FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 6), ("Carrot", 3)), D(("CarrotSeeds", 6)), 2, Spots(20)).Plant);
            FarmPlan later = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 12), ("Carrot", 3)), D(), 2, Spots(20));
            Assert.Equal(("sapling_carrot", 6), (later.Plant.Single().Crop.Plant, later.Plant.Single().Count));
            // A big order asks for no more spares than the free field can take.
            OrderList big = Orders(("CarrotSeeds", OrderKind.Seed, 6), ("Carrot", OrderKind.Crop, 500));
            FarmPlan capped = FarmPlanner.Plan(Crops, big, D(("CarrotSeeds", 6), ("Carrot", 50)), D(), 2, Spots(4));
            Assert.Equal(("sapling_seedcarrot", 2), (capped.Plant.Single().Crop.Plant, capped.Plant.Single().Count)); // 4 spots: 4 seeds wanted, 2 seed carrots
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
            // A turnip isn't edible raw but goes into soup: produce, though planting one grows turnip seeds.
            var turnips = new List<CropInfo>
            {
                new CropInfo { Plant = "sapling_turnip", Consumes = "TurnipSeeds", Yields = "Turnip" },
                new CropInfo { Plant = "sapling_seedturnip", Consumes = "Turnip", Yields = "TurnipSeeds" },
            };
            Assert.False(CropInfo.IsSeedItem("Turnip", turnips, i => i == "Turnip"));
            Assert.True(CropInfo.IsSeedItem("TurnipSeeds", turnips, i => i == "Turnip"));
        }

        private static readonly List<KitchenInfo> Kitchen = new()
        {
            new KitchenInfo { Output = "CookedMeat", Station = "piece_cookingstation", Kind = StationKind.Stove, Inputs = new() { ["RawMeat"] = 1 }, Level = 1 },
            new KitchenInfo { Output = "CarrotSoup", Station = "piece_cauldron", Kind = StationKind.Craft, Inputs = new() { ["Mushroom"] = 1, ["Carrot"] = 3 }, Level = 2 },
            new KitchenInfo { Output = "BreadDough", OutputAmount = 2, Station = "piece_preptable", Kind = StationKind.Craft, Inputs = new() { ["BarleyFlour"] = 10 }, Level = 6 },
            new KitchenInfo { Output = "Bread", Station = "piece_oven", Kind = StationKind.Stove, Inputs = new() { ["BreadDough"] = 1 }, Level = 5 },
        };

        private static readonly Dictionary<string, int> AllStations = new() { ["piece_cookingstation"] = 1, ["piece_cauldron"] = 1, ["piece_preptable"] = 1, ["piece_oven"] = 1 };

        [Fact]
        public void AStuckSeedOrderDoesntHoldItsCropBack()
        {
            // 7 seeds under a 100-seed order, no carrots: the seed order can't plant (no carrots), so the carrot order may
            // plant half the seeds (4, keeping 3); the carrots then feed the seed order.
            OrderList l = Orders(("CarrotSeeds", OrderKind.Seed, 100), ("Carrot", OrderKind.Crop, 20));
            FarmPlan p = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 7)), D(), 2, Spots(20));
            Assert.Contains(p.Plant, x => x.Crop.Plant == "sapling_carrot" && x.Count == 3);
            // With carrots to plant for seeds the reserve holds as before.
            FarmPlan q = FarmPlanner.Plan(Crops, l, D(("CarrotSeeds", 7), ("Carrot", 2)), D(), 2, Spots(20));
            Assert.DoesNotContain(q.Plant, x => x.Crop.Plant == "sapling_carrot");
        }

        [Fact]
        public void FarmGivesAReasonForEveryBlockedOrder()
        {
            OrderList l = Orders(("CarrotSeeds", OrderKind.Seed, 6), ("Barley", OrderKind.Crop, 10), ("Wood", OrderKind.Crop, 5),
                ("Raspberry", OrderKind.Crop, 5));
            FarmPlan p = FarmPlanner.Plan(Crops, l, D(), D(), 2, Spots(20));
            Assert.Equal("$vfh_need_seed|CarrotSeeds|Carrot", p.Why["CarrotSeeds"]);
            Assert.Equal("$vfh_need_farmer_level|Barley|5", p.Why["Barley"]);
            Assert.Equal("$vfh_need_no_crop|Wood", p.Why["Wood"]);
            Assert.False(p.Why.ContainsKey("Raspberry")); // picked from bushes: being worked
            // The hireling's own line keeps only crop problems, first first.
            Assert.Equal(new[] { "$vfh_need_seed|CarrotSeeds|Carrot", "$vfh_need_farmer_level|Barley|5" }, p.Missing);
            // No free ground: that order says so; one being planted has no reason.
            FarmPlan full = FarmPlanner.Plan(Crops, Orders(("Carrot", OrderKind.Crop, 4)), D(("CarrotSeeds", 9)), D(), 2, Spots(0));
            Assert.Equal("$vfh_need_field", full.Why["Carrot"]);
            Assert.Empty(FarmPlanner.Plan(Crops, Orders(("Carrot", OrderKind.Crop, 4)), D(("CarrotSeeds", 9)), D(), 2, Spots(20)).Why);
        }

        [Fact]
        public void KitchenGivesAReasonForEveryShortOrderNotJustTheFirst()
        {
            OrderList l = Orders(("CookedMeat", OrderKind.Kitchen, 4), ("CarrotSoup", OrderKind.Kitchen, 5), ("Bread", OrderKind.Kitchen, 2),
                ("Unicorn", OrderKind.Kitchen, 1));
            KitchenResult r = KitchenPlanner.Next(Kitchen, l, D(("RawMeat", 10), ("Carrot", 3), ("Mushroom", 10)), D(("Carrot", 3)), 4, AllStations);
            Assert.Equal("CookedMeat", r.Task!.Info.Output);
            Assert.Null(r.Missing); // it has work: the hover shows that
            Assert.False(r.Why.ContainsKey("CookedMeat"));
            Assert.Equal("$vfh_need_ingredient|CarrotSoup|3|Carrot", r.Why["CarrotSoup"]);
            Assert.Equal("$vfh_need_cook_level|Bread|5", r.Why["Bread"]);
            Assert.Equal("$vfh_need_recipe|Unicorn", r.Why["Unicorn"]);
            // Without the meat the first order is stuck too, and the first reason is still the hover's.
            KitchenResult none = KitchenPlanner.Next(Kitchen, l, D(("Carrot", 3), ("Mushroom", 10)), D(("Carrot", 3)), 4, AllStations);
            Assert.Null(none.Task);
            Assert.Equal(none.Why["CookedMeat"], none.Missing);
            Assert.Equal(4, none.Why.Count);
        }

        [Fact]
        public void WhatsInTheBoardsPileCountsTowardTheOrderButIsNoIngredient()
        {
            OrderList l = Orders(("CookedMeat", OrderKind.Kitchen, 4));
            // 4 cooked meat in the pile: the order is met, nothing more is cooked.
            Assert.Null(KitchenPlanner.Next(Kitchen, l, D(("RawMeat", 10)), D(), 1, AllStations, D(("CookedMeat", 4))).Task);
            // Raw meat in the pile only: nothing to cook with.
            KitchenResult r = KitchenPlanner.Next(Kitchen, l, D(), D(), 1, AllStations, D(("RawMeat", 10)));
            Assert.Null(r.Task);
        }

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
        public void InfiniteOrdersKeepTheirTargetAndNeverEnd()
        {
            OrderList l = Orders(("Carrot", OrderKind.Crop, ProductionOrder.Infinite), ("IronBar", OrderKind.Station, ProductionOrder.Infinite));
            OrderList back = OrderList.Parse(l.Serialize());
            Assert.True(back.Find("Carrot")!.IsInfinite);
            Assert.Equal(ProductionOrder.Infinite, back.StationCap("IronBar"));
            Assert.True(back.SetTarget("Carrot", 40));
            Assert.Equal(40, back.Find("Carrot")!.Target);
            Assert.True(back.SetTarget("Carrot", 123456));
            Assert.Equal(OrderList.MaxTarget, back.Find("Carrot")!.Target); // a number is still capped; only ∞ isn't
            // A Cook's ∞ order: as many batches as the ingredients allow.
            KitchenResult r = KitchenPlanner.Next(Kitchen, Orders(("CookedMeat", OrderKind.Kitchen, ProductionOrder.Infinite)), D(("RawMeat", 10)), D(), 4, AllStations);
            Assert.Equal("CookedMeat", r.Task!.Info.Output);
        }

        [Fact]
        public void TheStewardsCatchAllCoversWhatHasNoOrderOfItsOwn()
        {
            OrderList none = new OrderList();
            Assert.Null(none.StationCap("Coal")); // no order: not made (once the board's orders have begun)
            OrderList l = Orders(("IronBar", OrderKind.Station, 100), (ProductionOrder.Everything, OrderKind.Station, ProductionOrder.Infinite));
            Assert.Equal(100, l.StationCap("IronBar"));
            Assert.Equal(ProductionOrder.Infinite, l.StationCap("Coal"));
            l.SetPaused(ProductionOrder.Everything, true);
            Assert.Equal(0, l.StationCap("Coal"));
            Assert.Equal(100, l.StationCap("IronBar"));
        }

        [Fact]
        public void PrepChefMakesDoughBeforeThereIsAnOven()
        {
            OrderList l = Orders(("Bread", OrderKind.Kitchen, 4));
            var noOven = new Dictionary<string, int> { ["piece_preptable"] = 1 };
            KitchenResult plain = KitchenPlanner.Next(Kitchen, l, D(("BarleyFlour", 30)), D(), 8, noOven);
            Assert.Null(plain.Task);
            Assert.Equal("$vfh_need_kitchen|Bread", plain.Why["Bread"]);
            KitchenResult prep = KitchenPlanner.Next(Kitchen, l, D(("BarleyFlour", 30)), D(), 8, noOven, prep: true);
            Assert.Equal("BreadDough", prep.Task!.Info.Output);
            Assert.Equal(2, prep.Task.Batches); // 4 bread need 4 dough: 2 batches of 2
            Assert.Null(prep.Task.Then);
            // Dough already on hand: nothing to prepare.
            Assert.Null(KitchenPlanner.Next(Kitchen, l, D(("BarleyFlour", 30), ("BreadDough", 4)), D(), 8, noOven, prep: true).Task);
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
            Assert.Equal("$vfh_need_kitchen|Bread", KitchenPlanner.Next(Kitchen, l, D(("BreadDough", 5)), D(), 6, new Dictionary<string, int>()).Missing);
        }
    }
}
