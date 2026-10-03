using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class DataTests
    {
        [Fact]
        public void DefaultsPassValidation() => Assert.Empty(DataValidator.Validate(DefaultData.Create()));

        [Fact]
        public void DefaultsHave60NamesEach()
        {
            NameData names = DefaultData.Create().Names;
            Assert.Equal(60, names.Male.Distinct().Count());
            Assert.Equal(60, names.Female.Distinct().Count());
        }

        [Fact]
        public void RoundTripPreservesEverything()
        {
            VfhData original = DefaultData.Create();
            string yaml = DataYaml.Serialize(original);
            VfhData back = DataYaml.Deserialize(yaml);
            Assert.Equal(yaml, DataYaml.Serialize(back));
            Assert.Empty(DataValidator.Validate(back));
            Assert.Equal(40, back.BoardLevels[0].Cost["Wood"]);
            Assert.Equal("BowAshlands", back.Jobs[JobType.GuardRanged].Gear[7].Main);
            Assert.Contains("charcoal_kiln", back.Jobs[JobType.Smelter].Stations);
        }

        [Fact]
        public void YamlHasComments() => Assert.Contains("# Hiring board levels 1-8", DataYaml.Serialize(DefaultData.Create()));

        [Fact]
        public void UnknownKeyIsRejected() =>
            Assert.ThrowsAny<System.Exception>(() => DataYaml.Deserialize("boardLevelz: []\n"));

        [Fact]
        public void BadIndentationIsRejected() =>
            Assert.ThrowsAny<System.Exception>(() => DataYaml.Deserialize("boardLevels:\n  - level: 1\n   hirelingCap: 2\n"));

        [Fact]
        public void ValidatorCatchesStructuralErrors()
        {
            VfhData data = DefaultData.Create();
            data.BoardLevels.RemoveAt(7);
            data.HirelingLevels[2].CargoSlots = 40;
            data.Jobs.Remove(JobType.Miner);
            data.CommandStone[0].Cost["Bronze"] = -1;
            List<string> errors = DataValidator.Validate(data);
            Assert.Contains(errors, e => e.StartsWith("boardLevels"));
            Assert.Contains(errors, e => e.Contains("cargoSlots"));
            Assert.Contains(errors, e => e.Contains("jobs.Miner is missing"));
            Assert.Contains(errors, e => e.Contains("commandStone[1].cost.Bronze"));
        }

        [Fact]
        public void SanitizeDropsAndFallsBack()
        {
            VfhData data = DefaultData.Create();
            var unknown = new HashSet<string> { "MoltenCore", "SwordNiedhogg", "Grausten" };
            List<string> warnings = DataValidator.Sanitize(data, n => !unknown.Contains(n));
            Assert.False(data.BoardLevels[7].Cost.ContainsKey("MoltenCore"));
            Assert.Equal("SwordMistwalker", data.Jobs[JobType.GuardMelee].Gear[7].Main);
            Assert.DoesNotContain("Grausten", data.Jobs[JobType.Miner].PickupItems);
            Assert.Equal(4, warnings.Count); // board L8, stone Q4, gear L8 main, miner pickups
        }

        [Fact]
        public void SanitizeLeavesKnownDataAlone() => Assert.Empty(DataValidator.Sanitize(DefaultData.Create(), _ => true));

        [Fact]
        public void HashIsStableAndShort()
        {
            Assert.Equal(DataYaml.Hash("abc"), DataYaml.Hash("abc"));
            Assert.Equal(8, DataYaml.Hash("abc").Length);
        }
    }
}
