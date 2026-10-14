using System.Linq;
using System.Text.RegularExpressions;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class JobHatTests
    {
        private static readonly System.Func<string, bool> Everything = _ => true;

        [Fact]
        public void EveryWorkerHasAHatAndNoFighterDoes()
        {
            VfhData data = DefaultData.Create();
            var rules = new LevelRules(data);
            foreach (JobType job in System.Enum.GetValues(typeof(JobType)).Cast<JobType>())
            {
                if (job.IsCombat())
                    Assert.Null(rules.Hat(job));
                else
                    Assert.False(string.IsNullOrEmpty(rules.Hat(job)), $"{job} has no hat");
            }
            Assert.Equal(DefaultData.ChefHatItem, rules.Hat(JobType.Cook));
            Assert.Equal("HelmetStrawHat", rules.Hat(JobType.Farmer));
            // Six workers, six different hats: the hat tells the job.
            var hats = System.Enum.GetValues(typeof(JobType)).Cast<JobType>().Where(j => !j.IsCombat()).Select(j => rules.Hat(j)).ToList();
            Assert.Equal(hats.Count, hats.Distinct().Count());
        }

        [Fact]
        public void AFightersHatInTheDataIsIgnored()
        {
            VfhData data = DefaultData.Create();
            data.Jobs[JobType.GuardMelee].Hat = "HelmetStrawHat";
            Assert.Null(new LevelRules(data).Hat(JobType.GuardMelee));
        }

        [Fact]
        public void TheHatTakesTheHelmetsPlaceAtEveryLevel()
        {
            VfhData data = DefaultData.Create();
            var rules = new LevelRules(data);
            // Level 1 has no helmet in the shared sets: the hat still goes on.
            Assert.Equal(new[] { "HelmetStrawHat", "ArmorRagsChest", "ArmorRagsLegs", "" },
                GearRules.Armor(1, rules.Outfit(JobType.Farmer), data.ArmorSets, Everything, rules.Hat(JobType.Farmer)));
            Assert.Equal(new[] { "HelmetStrawHat", "ArmorBronzeChest", "ArmorBronzeLegs", "" },
                GearRules.Armor(3, rules.Outfit(JobType.Farmer), data.ArmorSets, Everything, rules.Hat(JobType.Farmer)));
            // A hat that doesn't exist (a removed mod's): the level's helmet.
            Assert.Equal("HelmetBronze",
                GearRules.Armor(3, rules.Outfit(JobType.Farmer), data.ArmorSets, n => n != "HelmetStrawHat", rules.Hat(JobType.Farmer))[0]);
            // Guards keep their helmets.
            Assert.Equal("HelmetBronze", GearRules.Armor(3, rules.Outfit(JobType.GuardMelee), data.ArmorSets, Everything, rules.Hat(JobType.GuardMelee))[0]);
        }

        [Fact]
        public void OlderFilesGetTheShippedHats()
        {
            // A 0.9.3 file: no hat in its jobs.
            string yaml = Regex.Replace(DataYaml.Serialize(DefaultData.Create()), @"^\s*hat:.*\n", "", RegexOptions.Multiline);
            Assert.DoesNotContain("hat:", yaml);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal("HelmetStrawHat", data.Jobs[JobType.Farmer].Hat);
            Assert.Contains("jobs.Farmer.hat", filled);
            Assert.Empty(DataValidator.Validate(data));
        }

        [Fact]
        public void AnUnknownHatIsDroppedWithAWarning()
        {
            VfhData data = DefaultData.Create();
            data.Jobs[JobType.Miner].Hat = "HelmetNoSuchThing";
            var warnings = DataValidator.Sanitize(data, n => n != "HelmetNoSuchThing");
            Assert.Equal("", data.Jobs[JobType.Miner].Hat);
            Assert.Contains(warnings, w => w.StartsWith("jobs.Miner.hat"));
        }
    }
}
