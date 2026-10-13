using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Enhancements;
using Xunit;

namespace VikingsForHire.Tests
{
    public class EnhancementMathTests
    {
        private static EnhancementEffects Effects(JobType job, string picks, VfhData? data = null) =>
            new(job, EnhancementPicks.Parse(picks), data ?? DefaultData.Create());

        [Fact]
        public void NoPicksChangeNothing()
        {
            foreach (EnhancementEffects e in new[] { EnhancementEffects.None, Effects(JobType.GuardMelee, null!), Effects(JobType.Miner, "-------") })
            {
                Assert.Equal(1f, e.HealthMult);
                Assert.Equal(1f, e.DamageMult);
                Assert.Equal(1f, e.DamageTakenMult);
                Assert.Equal(1f, e.AttackIntervalMult);
                Assert.Equal(1f, e.MoveSpeedMult);
                Assert.Equal(1f, e.GatherMult);
                Assert.Equal(1f, e.RadiusMult);
                Assert.Equal(0f, e.ArmorBonus);
                Assert.Equal(0, e.CargoSlotsBonus);
                Assert.Equal(0f, e.CargoWeightBonus);
                Assert.Equal(0, e.QualityBonus);
            }
        }

        [Fact]
        public void GuardBoostsStack()
        {
            // Toughened (2A), Heavy armour (3A), Masterwork (5A), Champion (8A).
            EnhancementEffects e = Effects(JobType.GuardMelee, "AA-A--A");
            Assert.Equal(1.21f, e.HealthMult, 3);
            Assert.Equal(1.10f, e.DamageMult, 3);
            Assert.Equal(4f, e.ArmorBonus);
            Assert.Equal(1, e.QualityBonus);
            Assert.Equal(1f, e.AttackIntervalMult);
            Assert.Equal(0.85f, Effects(JobType.GuardMelee, "-B-----").AttackIntervalMult, 3);
        }

        [Fact]
        public void RangedAndWizardBoosts()
        {
            Assert.Equal(1.10f, Effects(JobType.GuardRanged, "A------").DamageMult, 3);
            Assert.Equal(1.21f, Effects(JobType.GuardRanged, "A-----B").DamageMult, 3);
            Assert.Equal(1.10f, Effects(JobType.GuardRanged, "A-----B").HealthMult, 3);
            EnhancementEffects wiz = Effects(JobType.Wizard, EnhancementPicks.Parse(null)
                .With(EnhancementCatalog.LevelOf(JobType.Wizard, EnhancementId.Warding), Side(JobType.Wizard, EnhancementId.Warding))
                .With(EnhancementCatalog.LevelOf(JobType.Wizard, EnhancementId.Archmage), Side(JobType.Wizard, EnhancementId.Archmage))
                .ToString());
            Assert.Equal(0.85f, wiz.DamageTakenMult, 3);
            Assert.Equal(1.15f, wiz.DamageMult, 3);
            Assert.Equal(0.90f, wiz.AttackIntervalMult, 3);
        }

        [Fact]
        public void WorkerBoostsAndTheDataFile()
        {
            // Pack mule (2B), Strong back (3B), Wide range (6B), Swift (7B).
            EnhancementEffects wood = Effects(JobType.Woodcutter, "BB--BB-");
            Assert.Equal(2, wood.CargoSlotsBonus);
            Assert.Equal(50f, wood.CargoWeightBonus);
            Assert.Equal(1.2f, wood.RadiusMult, 3);
            Assert.Equal(1.1f, wood.MoveSpeedMult, 3);
            // Strong swing (2A) and Master woodsman (8A).
            Assert.Equal(1.265f, Effects(JobType.Woodcutter, "A-----A").GatherMult, 3);

            VfhData data = DefaultData.Create();
            data.Enhancements[JobType.Woodcutter]["PackMule.slots"] = 4f;
            Assert.Equal(4, Effects(JobType.Woodcutter, "B------", data).CargoSlotsBonus);
            // A name another job's number shares isn't read across jobs.
            Assert.Equal(0, Effects(JobType.Miner, "-------", data).CargoSlotsBonus);
        }

        private static char Side(JobType job, EnhancementId id) =>
            EnhancementCatalog.Pair(job, EnhancementCatalog.LevelOf(job, id)).A == id ? 'A' : 'B';
    }
}
