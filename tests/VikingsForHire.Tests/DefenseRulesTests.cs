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
        public void ClosestApproachHeadOnSideAndAway()
        {
            // 20 m away, flying straight at us at 20 m/s: there in a second, dead on.
            (float d, float t) = DefenseRules.ClosestApproach(0, 1, -20, 0, 0, 20, 0, 1, 0);
            Assert.Equal(0f, d, 3);
            Assert.Equal(1f, t, 3);
            // Passing 3 m to the side.
            (d, t) = DefenseRules.ClosestApproach(3, 1, -20, 0, 0, 20, 0, 1, 0);
            Assert.Equal(3f, d, 3);
            Assert.Equal(1f, t, 3);
            // Flying away: negative time, current distance.
            (d, t) = DefenseRules.ClosestApproach(0, 1, 5, 0, 0, 20, 0, 1, 0);
            Assert.True(t < 0f);
            Assert.Equal(5f, d, 3);
        }

        [Fact]
        public void DodgeDirectionsByKindOfAttack()
        {
            // Attack from straight ahead (+z).
            var area = DefenseRules.DodgeDirections(0, 1, area: true, projectile: false, leftFirst: true);
            Assert.Equal((0f, -1f), (Round(area[0].X), Round(area[0].Z))); // straight away
            var proj = DefenseRules.DodgeDirections(0, 1, area: false, projectile: true, leftFirst: true);
            Assert.Equal((-1f, 0f), (Round(proj[0].X), Round(proj[0].Z))); // left, out of the line
            Assert.Equal((1f, 0f), (Round(proj[1].X), Round(proj[1].Z)));
            var melee = DefenseRules.DodgeDirections(0, 1, area: false, projectile: false, leftFirst: true);
            Assert.True(melee[0].X < 0f && melee[0].Z < 0f); // back-left
            var melee2 = DefenseRules.DodgeDirections(0, 1, area: false, projectile: false, leftFirst: false);
            Assert.True(melee2[0].X > 0f && melee2[0].Z < 0f); // back-right when alternated
            Assert.Equal((0f, -1f), (Round(melee[4].X), Round(melee[4].Z))); // straight back last
            foreach (var d in melee)
                Assert.Equal(1f, (float)System.Math.Sqrt(d.X * d.X + d.Z * d.Z), 3);
        }

        private static float Round(float v) => (float)System.Math.Round(v, 3) + 0f;

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
