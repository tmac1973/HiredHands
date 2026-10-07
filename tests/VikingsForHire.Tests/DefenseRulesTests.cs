using System.Linq;
using System.Text.RegularExpressions;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class DefenseRulesTests
    {
        [Fact]
        public void BigHitsAreOverAQuarterOfCurrentHealthOrArea()
        {
            Assert.False(DefenseRules.WouldHurtALot(25f, 100f, area: false));
            Assert.True(DefenseRules.WouldHurtALot(25.1f, 100f, area: false));
            Assert.True(DefenseRules.WouldHurtALot(1f, 100f, area: true));
            Assert.True(DefenseRules.WouldHurtALot(10f, 30f, area: false)); // hurt already: the same hit counts for more
        }

        [Fact]
        public void RollsAndCooldown()
        {
            Assert.False(DefenseRules.Roll(0f, 0f));
            Assert.True(DefenseRules.Roll(1f, 0.999f));
            Assert.True(DefenseRules.Roll(0.5f, 0.49f));
            Assert.False(DefenseRules.Roll(0.5f, 0.5f));
            Assert.False(DefenseRules.DodgeReady(10f, 5f, 6f));
            Assert.True(DefenseRules.DodgeReady(11f, 5f, 6f));
        }

        [Fact]
        public void LeadsFitVanillasParryWindow()
        {
            Assert.True(DefenseRules.ParryLeadSeconds < 0.25f);
            Assert.True(DefenseRules.EarlyBlockLeadSeconds > 0.25f);
            Assert.True(DefenseRules.DodgeLatestSeconds < DefenseRules.DodgeLeadSeconds);
        }

        [Fact]
        public void DefaultCurveRisesWithLevel()
        {
            var levels = DefaultData.Create().HirelingLevels.OrderBy(h => h.Level).ToList();
            Assert.Equal(8, levels.Count);
            for (int i = 1; i < levels.Count; i++)
            {
                Assert.True(levels[i].ReadChance > levels[i - 1].ReadChance);
                Assert.True(levels[i].ParryChance > levels[i - 1].ParryChance);
                Assert.True(levels[i].DodgeChance > levels[i - 1].DodgeChance);
                Assert.True(levels[i].DodgeCooldown < levels[i - 1].DodgeCooldown);
            }
            Assert.Equal((0.50f, 0.15f, 0.30f, 6.0f), (levels[0].ReadChance, levels[0].ParryChance, levels[0].DodgeChance, levels[0].DodgeCooldown));
            Assert.Equal((0.95f, 0.70f, 0.80f, 2.0f), (levels[7].ReadChance, levels[7].ParryChance, levels[7].DodgeChance, levels[7].DodgeCooldown));
        }

        [Fact]
        public void OlderFilesGetTheShippedDefenseNumbers()
        {
            // A 0.5.0 file: no readChance/parryChance/dodgeChance/dodgeCooldown in its levels.
            string yaml = Regex.Replace(DataYaml.Serialize(DefaultData.Create()),
                @"^\s*(readChance|parryChance|dodgeChance|dodgeCooldown):.*\n", "", RegexOptions.Multiline);
            Assert.DoesNotContain("readChance", yaml);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            HirelingLevelData l3 = data.HirelingLevels.Single(h => h.Level == 3);
            Assert.Equal((0.63f, 0.31f, 0.44f, 4.9f), (l3.ReadChance, l3.ParryChance, l3.DodgeChance, l3.DodgeCooldown));
            Assert.Contains("hirelingLevels[2].readChance", filled); // FillMissing fills list entries from the shipped ones
            Assert.Empty(DataValidator.Validate(data));
        }

        [Fact]
        public void ChancesOutsideZeroToOneAreRejected()
        {
            VfhData data = DefaultData.Create();
            data.HirelingLevels[0].DodgeChance = 1.5f;
            Assert.Contains(DataValidator.Validate(data), e => e.Contains("dodgeChance"));
        }
    }
}
