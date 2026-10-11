using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core.Orders;
using Xunit;

namespace VikingsForHire.Tests
{
    public class RanchPlannerTests
    {
        private static readonly List<AnimalKind> Kinds = new()
        {
            new AnimalKind { Prefab = "Boar", YoungPrefabs = { "Boar_piggy" }, Meats = { ["RawMeat"] = 1f }, Foods = { "Raspberry" } },
            new AnimalKind { Prefab = "Wolf", YoungPrefabs = { "Wolf_cub" }, Meats = { ["WolfMeat"] = 1f }, Foods = { "RawMeat" } },
            new AnimalKind { Prefab = "Hen", Meats = { ["ChickenMeat"] = 1f }, EggItem = "ChickenEgg", Foods = { "BarleyFlour" } },
            // A modded animal dropping the same meat as boars.
            new AnimalKind { Prefab = "Pig_mod", Meats = { ["RawMeat"] = 2f } },
        };

        private static int _id;

        // Spread out (2 m apart) unless placed: partners in reach, no crowding.
        private static HerdAnimal A(string kind, int stars = 0, bool adult = true, bool named = false, float x = -1f, bool hungry = false) =>
            new() { Id = (++_id).ToString("D4"), Kind = kind, Stars = stars, Adult = adult, Named = named, X = x < 0 ? _id * 2f : x, Hungry = hungry };

        private static OrderList Orders(params (string Item, OrderKind Kind, int Target)[] orders)
        {
            var l = new OrderList();
            foreach (var o in orders)
                l.Add(o.Item, o.Kind, o.Target);
            return l;
        }

        private static Dictionary<string, int> D(params (string, int)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

        private static readonly HashSet<string> AllFed = new() { "Boar", "Wolf", "Hen", "Pig_mod" };

        private static RanchPlan Plan(List<HerdAnimal> herd, OrderList orders, Dictionary<string, int>? stock = null) =>
            RanchPlanner.Plan(Kinds, herd, orders, stock ?? D(), AllFed);

        [Fact]
        public void SlaughtersTheLowestStarAboveThePool()
        {
            HerdAnimal one = A("Boar", 1), zero = A("Boar", 0), two = A("Boar", 2), oneB = A("Boar", 1);
            RanchPlan p = Plan(new List<HerdAnimal> { one, zero, two, oneB }, Orders(("RawMeat", OrderKind.Meat, 40), ("Boar", OrderKind.Herd, 2)));
            Assert.Same(zero, p.Slaughter);
            Assert.Equal("RawMeat", p.ForOrder);
            Assert.Equal(4, p.Adults["Boar"]);
            // With the 0-star gone the pool (2 and the first 1-star) stays and the other 1-star goes.
            RanchPlan next = Plan(new List<HerdAnimal> { one, two, oneB }, Orders(("RawMeat", OrderKind.Meat, 40), ("Boar", OrderKind.Herd, 2)));
            Assert.Same(oneB, next.Slaughter);
            RanchPlan done = Plan(new List<HerdAnimal> { one, two }, Orders(("RawMeat", OrderKind.Meat, 40), ("Boar", OrderKind.Herd, 2)));
            Assert.Null(done.Slaughter);
            Assert.Equal("$vfh_ranch_at_pool|RawMeat|Boar|2", done.Why["RawMeat"]);
        }

        [Fact]
        public void NeverYoungNorNamedAndNamedDontCount()
        {
            HerdAnimal pet = A("Boar", 0, named: true), piglet = A("Boar", 0, adult: false), a = A("Boar", 1), b = A("Boar", 1);
            RanchPlan p = Plan(new List<HerdAnimal> { pet, piglet, a, b }, Orders(("RawMeat", OrderKind.Meat, 40), ("Boar", OrderKind.Herd, 2)));
            Assert.Null(p.Slaughter); // the pool is the two unnamed adults; the pet and the piglet aren't in play
            Assert.Equal(2, p.Adults["Boar"]);
            HerdAnimal c = A("Boar", 2);
            Assert.Same(b, Plan(new List<HerdAnimal> { pet, piglet, a, b, c }, Orders(("RawMeat", OrderKind.Meat, 40), ("Boar", OrderKind.Herd, 2))).Slaughter);
        }

        [Fact]
        public void MeatStockedOrNoPoolMeansNoSlaughter()
        {
            var herd = new List<HerdAnimal> { A("Boar"), A("Boar"), A("Boar") };
            Assert.Null(Plan(herd, Orders(("RawMeat", OrderKind.Meat, 10), ("Boar", OrderKind.Herd, 2)), D(("RawMeat", 10))).Slaughter);
            RanchPlan noPool = Plan(herd, Orders(("RawMeat", OrderKind.Meat, 10)));
            Assert.Null(noPool.Slaughter);
            Assert.Equal("$vfh_ranch_need_pool|RawMeat|Boar", noPool.Why["RawMeat"]);
            Assert.Equal("$vfh_ranch_no_animal|LoxMeat", Plan(herd, Orders(("LoxMeat", OrderKind.Meat, 10))).Why["LoxMeat"]);
        }

        [Fact]
        public void TwoKindsCanFillTheSameMeatOrder()
        {
            HerdAnimal boar = A("Boar", 1), pig = A("Pig_mod", 0), pig2 = A("Pig_mod", 0), pig3 = A("Pig_mod", 2);
            RanchPlan p = Plan(new List<HerdAnimal> { A("Boar", 2), A("Boar", 2), boar, pig, pig2, pig3 },
                Orders(("RawMeat", OrderKind.Meat, 40), ("Boar", OrderKind.Herd, 2), ("Pig_mod", OrderKind.Herd, 2)));
            Assert.Same(pig2, p.Slaughter); // lowest stars across both kinds; of two equals the first stays in the pool
        }

        [Fact]
        public void HerdOrdersNeverBelowTwo()
        {
            OrderList l = Orders(("Boar", OrderKind.Herd, 1));
            Assert.Equal(2, l.Find("Boar")!.Target);
            l.SetTarget("Boar", 0);
            Assert.Equal(2, l.Find("Boar")!.Target);
            l.Add("RawMeat", OrderKind.Meat, 0);
            Assert.Equal(0, l.Find("RawMeat")!.Target);
        }

        [Fact]
        public void EggsAreCollectedOnlyWhileTheOrderIsShort()
        {
            var hens = new List<HerdAnimal> { A("Hen"), A("Hen") };
            Assert.Contains("ChickenEgg", Plan(hens, Orders(("ChickenEgg", OrderKind.Egg, 20)), D(("ChickenEgg", 5))).CollectEggs);
            Assert.Empty(Plan(hens, Orders(("ChickenEgg", OrderKind.Egg, 20)), D(("ChickenEgg", 20))).CollectEggs);
            RanchPlan none = Plan(new List<HerdAnimal> { A("Boar") }, Orders(("ChickenEgg", OrderKind.Egg, 20)));
            Assert.Empty(none.CollectEggs);
            Assert.Equal("$vfh_ranch_no_layers|ChickenEgg", none.Why["ChickenEgg"]);
        }

        [Fact]
        public void BreedingProblemsInVanillasOrder()
        {
            OrderList pool = Orders(("Boar", OrderKind.Herd, 2));
            Assert.Equal("$vfh_ranch_need_pair|Boar", Plan(new List<HerdAnimal> { A("Boar") }, pool).Why["Boar"]);
            Assert.Equal("$vfh_ranch_hungry|Boar", Plan(new List<HerdAnimal> { A("Boar", hungry: true), A("Boar") }, pool).Why["Boar"]);
            Assert.Equal("$vfh_ranch_no_feed|Boar", RanchPlanner.Plan(Kinds, new List<HerdAnimal> { A("Boar", hungry: true), A("Boar") }, pool, D(),
                new HashSet<string>()).Why["Boar"]);
            Assert.Equal("$vfh_ranch_no_partner|Boar|3", Plan(new List<HerdAnimal> { A("Boar", x: 0f), A("Boar", x: 20f) }, pool).Why["Boar"]);
            var crowd = Enumerable.Range(0, 5).Select(i => A("Boar", x: i * 0.5f)).ToList();
            Assert.Equal("$vfh_ranch_crowded|Boar|4|10", Plan(crowd, pool).Why["Boar"]);
            Assert.False(Plan(new List<HerdAnimal> { A("Boar", x: 0f), A("Boar", x: 2f) }, pool).Why.ContainsKey("Boar"));
        }

        [Fact]
        public void AnEscapeIsReportedFirstAndTheAnimalStillCounts()
        {
            HerdAnimal outside = A("Boar", x: 0f);
            outside.Escaped = true;
            RanchPlan p = Plan(new List<HerdAnimal> { outside, A("Boar", x: 2f), A("Boar", x: 4f) }, Orders(("Boar", OrderKind.Herd, 2), ("RawMeat", OrderKind.Meat, 40)));
            Assert.Equal("$vfh_ranch_escaped|Boar|1", p.Why["Boar"]);
            Assert.Equal(3, p.Adults["Boar"]);
            Assert.NotNull(p.Slaughter);
        }

        [Fact]
        public void RanchOrdersRoundTripAndOldStringsStillParse()
        {
            OrderList l = Orders(("RawMeat", OrderKind.Meat, 40), ("ChickenEgg", OrderKind.Egg, 20), ("Boar", OrderKind.Herd, 4),
                ("Carrot", OrderKind.Crop, 10), ("CookedMeat", OrderKind.Kitchen, 5), ("Coal", OrderKind.Station, 100), ("CarrotSeeds", OrderKind.Seed, 6));
            OrderList back = OrderList.Parse(l.Serialize());
            Assert.Equal(l.Orders.Select(o => (o.Item, o.Kind, o.Target)), back.Orders.Select(o => (o.Item, o.Kind, o.Target)));
            Assert.Equal(new[] { "RawMeat", "ChickenEgg", "Boar" }, back.Ranch().Select(o => o.Item));
            Assert.DoesNotContain(back.Active(), o => o.IsRanch); // the Farmer and Cook never see them
            Assert.Equal(2, OrderList.Parse("v1|Carrot:10:c:0;CookedMeat:5:k:1").Orders.Count);
            Assert.True(l.Move("Boar", -1)); // within the ranch group
            Assert.Equal("Boar", l.Orders[1].Item);
        }
    }
}
