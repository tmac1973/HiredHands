using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class StanceRulesTests
    {
        private static CombatAction D(Stance s, float? dist, bool inRadius = false, bool inExtended = false, bool attacked = false, bool ally = false) =>
            StanceRules.Decide(s, dist, inRadius, inExtended, attacked, ally);

        [Fact]
        public void FleeOnNearbyThreatOrHit()
        {
            Assert.Equal(CombatAction.Flee, D(Stance.Flee, 10f));
            Assert.Equal(CombatAction.None, D(Stance.Flee, 20f));
            Assert.Equal(CombatAction.Flee, D(Stance.Flee, null, attacked: true));
        }

        [Fact]
        public void DefendOnlyWhenClose()
        {
            Assert.Equal(CombatAction.Engage, D(Stance.Defend, 3f));
            Assert.Equal(CombatAction.None, D(Stance.Defend, 8f, inRadius: true));
            Assert.Equal(CombatAction.Engage, D(Stance.Defend, 30f, attacked: true));
        }

        [Fact]
        public void PassiveOnlyRetaliates()
        {
            Assert.Equal(CombatAction.None, D(Stance.Passive, 2f, inRadius: true, ally: true));
            Assert.Equal(CombatAction.Engage, D(Stance.Passive, 2f, attacked: true));
        }

        [Fact]
        public void DefensiveGuardsRadiusAndAllies()
        {
            Assert.Equal(CombatAction.Engage, D(Stance.Defensive, 15f, inRadius: true));
            Assert.Equal(CombatAction.None, D(Stance.Defensive, 25f, inExtended: true));
            Assert.Equal(CombatAction.Engage, D(Stance.Defensive, null, ally: true));
        }

        [Fact]
        public void AggressiveHuntsFurther()
        {
            Assert.Equal(CombatAction.Engage, D(Stance.Aggressive, 25f, inExtended: true));
            Assert.Equal(CombatAction.None, D(Stance.Aggressive, null));
        }

        [Fact]
        public void RetreatWithHysteresis()
        {
            Assert.True(StanceRules.ShouldRetreat(Stance.Defend, false, 0.2f, false));
            Assert.False(StanceRules.ShouldRetreat(Stance.Defend, false, 0.3f, false));
            Assert.True(StanceRules.ShouldRetreat(Stance.Defend, false, 0.3f, true));
            Assert.False(StanceRules.ShouldRetreat(Stance.Defend, false, 0.45f, true));
            Assert.False(StanceRules.ShouldRetreat(Stance.Aggressive, true, 0.1f, false));
            Assert.True(StanceRules.ShouldRetreat(Stance.Defensive, true, 0.1f, false));
        }

        [Fact]
        public void LeashLongerWhenAggressive()
        {
            Assert.Equal(25f, StanceRules.LeashBeyondRadius(Stance.Aggressive));
            Assert.Equal(15f, StanceRules.LeashBeyondRadius(Stance.Defensive));
        }
    }
}
