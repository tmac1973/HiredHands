using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class ChoreRulesTests
    {
        private static JobData Steward() => DefaultData.Create().Jobs[JobType.Smelter];

        [Theory]
        [InlineData("fires", 1)]
        [InlineData("beehives", 1)]
        [InlineData("smelter", 2)]
        [InlineData("charcoal_kiln", 2)]
        [InlineData("animals", 2)]
        [InlineData("repairs", 3)]
        [InlineData("blastfurnace", 5)]
        [InlineData("windmill", 5)]
        [InlineData("piece_spinningwheel", 5)]
        [InlineData("eitrrefinery", 6)]
        [InlineData("sap", 6)]
        public void DefaultLevels(string key, int level) => Assert.Equal(level, ChoreRules.MinLevel(Steward(), key));

        [Fact]
        public void SmeltingNeedsLevelTwo()
        {
            Assert.False(ChoreRules.Unlocked(Steward(), 1, "smelter"));
            Assert.True(ChoreRules.Unlocked(Steward(), 2, "smelter"));
        }

        [Fact]
        public void UnknownStationIsLevelOne() => Assert.Equal(1, ChoreRules.MinLevel(Steward(), "my_modded_forge"));

        [Fact]
        public void MillsAreTheirOwnKind()
        {
            Assert.Equal(ChoreKind.Mills, ChoreRules.KindOfStation("windmill"));
            Assert.Equal(ChoreKind.Mills, ChoreRules.KindOfStation("piece_spinningwheel"));
            Assert.Equal(ChoreKind.Stations, ChoreRules.KindOfStation("smelter"));
            Assert.Equal(new[] { "windmill", "piece_spinningwheel" }, ChoreRules.GateKeys(Steward(), ChoreKind.Mills).ToArray());
            Assert.Equal(2, ChoreRules.FirstUnlock(Steward(), ChoreKind.Stations));
            Assert.Equal(5, ChoreRules.FirstUnlock(Steward(), ChoreKind.Mills));
            Assert.Equal(3, ChoreRules.FirstUnlock(Steward(), ChoreKind.Repairs));
        }

        [Fact]
        public void TogglesRoundTripAndLeaveOtherEntries()
        {
            string skip = ChoreRules.WithChore("Wood", ChoreKind.Fires, on: false);
            skip = ChoreRules.WithChore(skip, ChoreKind.Repairs, on: false);
            Assert.Equal(new[] { ChoreKind.Fires, ChoreKind.Repairs }.ToHashSet(), ChoreRules.ChoresOff(skip));
            Assert.Contains("Wood", GatherRules.ParseSkip(skip));
            skip = ChoreRules.WithChore(skip, ChoreKind.Fires, on: true);
            Assert.Equal(new[] { ChoreKind.Repairs }.ToHashSet(), ChoreRules.ChoresOff(skip));
            Assert.Empty(ChoreRules.ChoresOff(""));
            Assert.Empty(ChoreRules.ChoresOff("chore:nonsense,Stone"));
        }

        [Fact]
        public void KeysParseBothWays()
        {
            foreach (ChoreKind k in ChoreKeys.All)
            {
                Assert.True(ChoreKeys.TryParse(ChoreKeys.Key(k), out ChoreKind back));
                Assert.Equal(k, back);
            }
        }
    }
}
