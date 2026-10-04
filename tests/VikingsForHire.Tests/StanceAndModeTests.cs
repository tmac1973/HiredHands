using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class StanceAndModeTests
    {
        [Fact]
        public void WorkersAndGuardsHaveSeparateStances()
        {
            Assert.Equal(new[] { Stance.Flee, Stance.Defend }, StanceRules.Allowed(JobType.Miner));
            Assert.Equal(new[] { Stance.Passive, Stance.Defensive, Stance.Aggressive }, StanceRules.Allowed(JobType.GuardRanged));
            Assert.Equal(Stance.Defend, StanceRules.Default(JobType.Smelter));
            Assert.Equal(Stance.Defensive, StanceRules.Default(JobType.GuardMelee));
            Assert.False(StanceRules.IsAllowed(JobType.Woodcutter, Stance.Aggressive));
        }

        [Fact]
        public void PersistedModeValuesNeverChange()
        {
            Assert.Equal(0, (int)HirelingMode.Working);
            Assert.Equal(1, (int)HirelingMode.Following);
            Assert.Equal(3, (int)HirelingMode.Idle);
            Assert.Equal(4, (int)HirelingMode.Leaving);
            Assert.Equal(2, (int)FollowMode.GatherHere);
        }
    }
}
