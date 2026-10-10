using System.Collections.Generic;
using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class FoodPointsTests
    {
        [Fact]
        public void PointsSumAndRound() => Assert.Equal(46, FoodPoints.PointsFor(30f, 10f, 5.5f));

        [Fact]
        public void RawFoodRejectedUnlessAllowed()
        {
            var raw = new HashSet<string> { "RawMeat" };
            Assert.False(FoodPoints.IsAcceptable("RawMeat", true, false, raw));
            Assert.True(FoodPoints.IsAcceptable("RawMeat", true, true, raw));
            Assert.True(FoodPoints.IsAcceptable("CookedMeat", true, false, raw));
            Assert.False(FoodPoints.IsAcceptable("Wood", false, true, raw));
        }

        [Fact]
        public void UnmadeFoodIsRaw()
        {
            var raw = new HashSet<string> { "RawMeat" };
            var prepared = new HashSet<string> { "CookedMeat" };
            Assert.True(FoodPoints.IsAcceptable("CookedMeat", true, false, raw, prepared));
            Assert.False(FoodPoints.IsAcceptable("WitchEye", true, false, raw, prepared));
            Assert.True(FoodPoints.IsAcceptable("WitchEye", true, true, raw, prepared));
            Assert.False(FoodPoints.IsAcceptable("RawMeat", true, false, raw, new HashSet<string> { "RawMeat" }));
        }

        [Fact]
        public void CheapestFirstWithOverpay()
        {
            var stacks = new[]
            {
                new FoodStack("Sausages", 98, 5),
                new FoodStack("CookedMeat", 40, 3),
                new FoodStack("QueensJam", 52, 2),
            };
            PaymentPlan plan = FoodPoints.PlanPayment(stacks, 150);
            Assert.True(plan.Sufficient);
            // 3 meat (120) then 1 jam (52) = 172.
            Assert.Equal(new List<(string, int)> { ("CookedMeat", 3), ("QueensJam", 1) }, plan.Take);
            Assert.Equal(172, plan.Paid);
            Assert.Equal(22, plan.Overpay);
        }

        [Fact]
        public void InsufficientTakesNothing()
        {
            PaymentPlan plan = FoodPoints.PlanPayment(new[] { new FoodStack("CookedMeat", 40, 2) }, 100);
            Assert.False(plan.Sufficient);
            Assert.Empty(plan.Take);
        }

        [Fact]
        public void ZeroRequirementIsFree() => Assert.True(FoodPoints.PlanPayment(new FoodStack[0], 0).Sufficient);

        [Fact]
        public void ExactPaymentNoOverpay()
        {
            PaymentPlan plan = FoodPoints.PlanPayment(new[] { new FoodStack("CookedMeat", 40, 10) }, 120);
            Assert.Equal(0, plan.Overpay);
            Assert.Equal(new List<(string, int)> { ("CookedMeat", 3) }, plan.Take);
        }
    }
}
