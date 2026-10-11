using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    // ModPresence is static: these tests set it and put it back, and run one at a time (one class).
    public class WizardRulesTests
    {
        private const string Wizardry = "Therzie.Wizardry";

        private static LevelRules Rules(params string[] loaded)
        {
            ModPresence.SetLoaded(loaded);
            return new LevelRules(DefaultData.Create());
        }

        [Fact]
        public void TheWizardIsACombatJob()
        {
            Assert.True(JobType.Wizard.IsCombat());
            Assert.True(JobType.Wizard.IsGuard());
            Assert.Equal(1.5f, DefaultData.Create().Jobs[JobType.Wizard].CostMult);
            Assert.Equal(Stance.Defensive, StanceRules.Default(JobType.Wizard));
        }

        [Fact]
        public void UnlocksAtSixOrAtTwoWithWizardry()
        {
            try
            {
                LevelRules without = Rules();
                Assert.Equal(6, without.MinBoardLevel(JobType.Wizard));
                Assert.False(without.JobUnlocked(5, JobType.Wizard));
                Assert.True(without.JobUnlocked(6, JobType.Wizard));
                LevelRules with = Rules(Wizardry);
                Assert.Equal(2, with.MinBoardLevel(JobType.Wizard));
                Assert.True(with.JobUnlocked(2, JobType.Wizard));
                Assert.False(with.JobUnlocked(1, JobType.Wizard));
                // Other jobs don't care.
                Assert.Equal(2, with.MinBoardLevel(JobType.Miner));
            }
            finally
            {
                ModPresence.SetLoaded(new string[0]);
            }
        }

        [Fact]
        public void WizardryGearAndRobesOnlyWhenLoaded()
        {
            try
            {
                LevelRules with = Rules(Wizardry);
                Assert.Equal("StaffBlackforest_TW", with.Gear(JobType.Wizard).First(g => g.Level == 2).Main);
                Assert.Equal("StaffMistlands_TW", with.Gear(JobType.Wizard).First(g => g.Level == 6).Main);
                ArmorSetData robes = with.Outfit(JobType.Wizard).First(o => o.Level == 2);
                Assert.Equal("ArmorSpellSlingerChest_BlackForest_TW", robes.Chest);
                Assert.Equal("CapeSpellslinger_Blackforest_TW", robes.Cape);
                // The no-mod staffs stay reachable as the fallback.
                Assert.Equal("StaffFireball", with.GearWithoutMods(JobType.Wizard).First(g => g.Level == 2).Main);

                LevelRules without = Rules();
                Assert.Equal("StaffFireball", without.Gear(JobType.Wizard).First(g => g.Level == 6).Main);
                Assert.Equal("StaffLightning", without.Gear(JobType.Wizard).First(g => g.Level == 8).Main);
                Assert.Equal("ArmorMageChest", without.Outfit(JobType.Wizard).First(o => o.Level == 6).Chest);
                Assert.DoesNotContain(without.Outfit(JobType.Wizard), o => o.Level == 2);
            }
            finally
            {
                ModPresence.SetLoaded(new string[0]);
            }
        }

        [Fact]
        public void TheFirstLoadedModWinsAndAnAbsentOneChangesNothing()
        {
            try
            {
                VfhData data = DefaultData.Create();
                JobData wizard = data.Jobs[JobType.Wizard];
                wizard.WithMods.Insert(0, new JobModOverride { Guid = "Some.Other", MinBoardLevel = 4 });
                var rules = new LevelRules(data);
                ModPresence.SetLoaded(new[] { Wizardry });
                Assert.Equal(2, rules.MinBoardLevel(JobType.Wizard)); // Some.Other isn't loaded
                ModPresence.SetLoaded(new[] { Wizardry, "Some.Other" });
                Assert.Equal(4, rules.MinBoardLevel(JobType.Wizard)); // both loaded: the first entry wins
                Assert.Equal("StaffFireball", rules.Gear(JobType.Wizard).First(g => g.Level == 6).Main); // its gear list is empty: the job's own
                ModPresence.Pretend("Some.Other", false);
                Assert.Equal(2, rules.MinBoardLevel(JobType.Wizard));
                ModPresence.Pretend("Some.Other", null);
                Assert.Equal(4, rules.MinBoardLevel(JobType.Wizard));
            }
            finally
            {
                ModPresence.Pretend("Some.Other", null);
                ModPresence.SetLoaded(new string[0]);
            }
        }

        [Fact]
        public void OlderFilesGetTheWizard()
        {
            VfhData old = DefaultData.Create();
            old.Jobs.Remove(JobType.Wizard);
            string yaml = DataYaml.Serialize(old);
            Assert.DoesNotContain("  Wizard:", yaml);
            VfhData data = DataYaml.Deserialize(yaml, out List<string> filled);
            Assert.Contains("jobs.Wizard", filled);
            Assert.Equal(6, data.Jobs[JobType.Wizard].MinBoardLevel);
            Assert.Equal(Wizardry, data.Jobs[JobType.Wizard].WithMods.Single().Guid);
            Assert.Empty(DataValidator.Validate(data));
        }
    }
}
