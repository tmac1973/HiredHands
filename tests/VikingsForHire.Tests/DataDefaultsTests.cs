using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class DataDefaultsTests
    {
        [Fact]
        public void SettingsMissingFromAnOlderFileComeFromTheDefaults()
        {
            // A file from before minBoardLevel / workRadiusMultiplier existed.
            string yaml = DataYaml.Serialize(DefaultData.Create())
                .Replace("    minBoardLevel: 2\n", "").Replace("    workRadiusMultiplier: 2\n", "");
            Assert.DoesNotContain("workRadiusMultiplier: 2", yaml);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal(2, data.Jobs[JobType.Miner].MinBoardLevel);
            Assert.Equal(2f, data.Jobs[JobType.Miner].WorkRadiusMultiplier);
            Assert.Equal(2f, data.Jobs[JobType.Woodcutter].WorkRadiusMultiplier);
            Assert.Contains("jobs.Miner.minBoardLevel", filled);
        }

        [Fact]
        public void WhatTheFileSaysIsKept()
        {
            string yaml = DataYaml.Serialize(DefaultData.Create()).Replace("    minBoardLevel: 2\n", "    minBoardLevel: 4\n");
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal(4, data.Jobs[JobType.Miner].MinBoardLevel);
            Assert.Empty(filled);
        }

        [Fact]
        public void AFullDefaultFileFillsNothing()
        {
            DataYaml.Deserialize(DataYaml.Serialize(DefaultData.Create()), out var filled);
            Assert.Empty(filled);
        }
    }
}
