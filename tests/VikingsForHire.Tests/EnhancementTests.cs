using System;
using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Enhancements;
using Xunit;

namespace VikingsForHire.Tests
{
    public class EnhancementTests
    {
        [Fact]
        public void EveryJobHasSevenPairsOfDistinctNames()
        {
            foreach (JobType job in Enum.GetValues(typeof(JobType)))
            {
                Assert.Contains(job, EnhancementCatalog.Jobs);
                var names = Enumerable.Range(2, 7).SelectMany(l => new[] { EnhancementCatalog.Pair(job, l).A, EnhancementCatalog.Pair(job, l).B }).ToList();
                Assert.Equal(14, names.Distinct().Count());
            }
            Assert.Equal(126, EnhancementCatalog.All().Count());
        }

        [Fact]
        public void LevelOfFindsEachNameForItsJobOnly()
        {
            foreach ((JobType job, EnhancementId id) in EnhancementCatalog.All())
            {
                int level = EnhancementCatalog.LevelOf(job, id);
                Assert.InRange(level, 2, 8);
                (EnhancementId a, EnhancementId b) = EnhancementCatalog.Pair(job, level);
                Assert.True(a == id || b == id);
            }
            // A shared name sits at different levels in different jobs.
            Assert.Equal(7, EnhancementCatalog.LevelOf(JobType.Woodcutter, EnhancementId.Swift));
            Assert.Equal(6, EnhancementCatalog.LevelOf(JobType.Miner, EnhancementId.Swift));
            Assert.Equal(0, EnhancementCatalog.LevelOf(JobType.GuardMelee, EnhancementId.Swift));
            Assert.False(EnhancementCatalog.Offers(JobType.Cook, EnhancementId.FireArrows));
        }

        [Fact]
        public void PicksRoundTripAndTolerateBadText()
        {
            EnhancementPicks p = EnhancementPicks.Parse(null).With(2, 'A').With(5, 'B');
            Assert.Equal("A--B---", p.ToString());
            Assert.Equal(p, EnhancementPicks.Parse(p.ToString()));
            Assert.Equal("-------", EnhancementPicks.Parse("xyz").ToString());
            Assert.Equal("AB-----", EnhancementPicks.Parse("AB").ToString());
            Assert.Equal("A------", EnhancementPicks.Parse("A------BBB").ToString());
            Assert.Throws<ArgumentOutOfRangeException>(() => p.With(1, 'A'));
            Assert.Throws<ArgumentOutOfRangeException>(() => p.With(9, 'A'));
            Assert.Throws<ArgumentOutOfRangeException>(() => p.With(3, 'C'));
            Assert.True(p.Has(JobType.GuardRanged, EnhancementId.SteadyAim));
            Assert.True(p.Has(JobType.GuardRanged, EnhancementId.Hunter));
            Assert.False(p.Has(JobType.GuardRanged, EnhancementId.QuickDraw));
            Assert.Equal(new[] { EnhancementId.SteadyAim, EnhancementId.Hunter }, p.Ids(JobType.GuardRanged));
            Assert.False(EnhancementPicks.Parse("").Any);
        }

        [Fact]
        public void OwedAndCanChoose()
        {
            EnhancementPicks none = EnhancementPicks.Parse(null);
            Assert.Empty(EnhancementRules.Owed(none, 1));
            Assert.Equal(new[] { 2, 3, 4, 5 }, EnhancementRules.Owed(none, 5));
            EnhancementPicks some = none.With(3, 'B');
            Assert.Equal(new[] { 2, 4, 5 }, EnhancementRules.Owed(some, 5));
            Assert.True(EnhancementRules.CanChoose(some, 5, 5));
            Assert.False(EnhancementRules.CanChoose(some, 5, 6));
            Assert.False(EnhancementRules.CanChoose(some, 5, 3));
            Assert.False(EnhancementRules.CanChoose(some, 5, 1));
        }

        [Fact]
        public void RespecCostsAShareOfTheHireFee()
        {
            var costs = new CostCalculator(DefaultData.Create(), 0.5f);
            Cost fee4 = costs.HireCost(JobType.GuardRanged, 4);
            Cost r4 = EnhancementRules.RespecCost(costs, JobType.GuardRanged, 4, 0.5f);
            Assert.Equal((int)Math.Ceiling(fee4.Coins * 0.5), r4.Coins);
            Assert.Equal((int)Math.Ceiling(fee4.FoodPoints * 0.5), r4.FoodPoints);
            Assert.True(EnhancementRules.RespecCost(costs, JobType.GuardRanged, 8, 0.5f).Coins > r4.Coins);
            Assert.Equal(Cost.Zero, EnhancementRules.RespecCost(costs, JobType.GuardRanged, 4, 0f));
        }

        [Fact]
        public void EveryNumberHasADefaultAndOneShowsThroughTheData()
        {
            var defaults = EnhancementDefaults.Create();
            Assert.Equal(9, defaults.Count);
            Assert.Equal(0.10f, defaults[JobType.GuardMelee]["Toughened.health"], 3);
            Assert.Equal(0.5f, defaults[JobType.Miner]["HeavyHauler.oreWeight"], 3);
            // Every number name is attached to a name the job offers.
            foreach (var job in defaults)
                foreach (string key in job.Value.Keys)
                {
                    var id = (EnhancementId)Enum.Parse(typeof(EnhancementId), key.Substring(0, key.IndexOf('.')));
                    Assert.True(EnhancementCatalog.Offers(job.Key, id), $"{job.Key}.{key}");
                }
            VfhData data = DefaultData.Create();
            data.Enhancements[JobType.GuardMelee]["Toughened.health"] = 0.2f;
            Assert.Equal(0.2f, EnhancementNumbers.Get(data, JobType.GuardMelee, EnhancementId.Toughened, "health"), 3);
            data.Enhancements[JobType.GuardMelee].Remove("Toughened.health");
            Assert.Equal(0.10f, EnhancementNumbers.Get(data, JobType.GuardMelee, EnhancementId.Toughened, "health"), 3);
            Assert.Equal(0f, EnhancementNumbers.Get(data, JobType.GuardMelee, EnhancementId.Toughened, "nothing"));
        }

        [Fact]
        public void OlderFilesGainTheEnhancementSectionsAndBadNumbersAreRefused()
        {
            string full = DataYaml.Serialize(DefaultData.Create());
            int start = full.IndexOf("enhancements:");
            Assert.True(start > 0, "default file should have enhancements");
            string yaml = full.Substring(0, full.LastIndexOf('\n', start) + 1);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal(0.10f, data.Enhancements[JobType.Woodcutter]["StrongSwing.gather"], 3);
            Assert.Equal(0.5f, data.EnhancementSettings.RespecCostFraction, 3);
            Assert.Empty(DataValidator.Validate(data));

            // A number added later reaches a file that has the section.
            VfhData some = DataYaml.Deserialize(full.Replace("    Toughened.health: 0.1\n", ""), out var filled2);
            Assert.Equal(0.10f, some.Enhancements[JobType.GuardMelee]["Toughened.health"], 3);
            Assert.Contains("enhancements.GuardMelee.Toughened.health", filled2);

            VfhData bad = DefaultData.Create();
            bad.Enhancements[JobType.GuardRanged]["Headshot.chance"] = 1.5f;
            bad.Enhancements[JobType.Miner]["DeepDigger.depth"] = -1f;
            bad.EnhancementSettings.RespecCostFraction = 2f;
            var errors = DataValidator.Validate(bad);
            Assert.Contains(errors, e => e.Contains("Headshot.chance"));
            Assert.Contains(errors, e => e.Contains("DeepDigger.depth"));
            Assert.Contains(errors, e => e.Contains("respecCostFraction"));
        }
    }
}
