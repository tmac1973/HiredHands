using System.Collections.Generic;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class RancherJobTests
    {
        [Fact]
        public void TheRancherIsALevelTwoWorker()
        {
            Assert.False(JobType.Rancher.IsCombat());
            JobData j = DefaultData.Create().Jobs[JobType.Rancher];
            Assert.Equal(2, j.MinBoardLevel);
            Assert.Equal(0.9f, j.CostMult);
            Assert.Equal(1, j.MaxPerBoard);
            Assert.Equal(new[] { ChoreKind.RanchFeed, ChoreKind.Slaughter }, ChoreRules.ChoresFor(JobType.Rancher));
            Assert.True(ChoreKeys.TryParse("ranch_feed", out ChoreKind k) && k == ChoreKind.RanchFeed);
        }

        [Fact]
        public void OlderFilesGetTheRancher()
        {
            VfhData old = DefaultData.Create();
            old.Jobs.Remove(JobType.Rancher);
            VfhData data = DataYaml.Deserialize(DataYaml.Serialize(old), out List<string> filled);
            Assert.Contains("jobs.Rancher", filled);
            Assert.Equal(2, data.Jobs[JobType.Rancher].MinBoardLevel);
            Assert.Empty(DataValidator.Validate(data));
        }
    }
}
