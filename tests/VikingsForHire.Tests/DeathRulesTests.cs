using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class DeathRulesTests
    {
        [Fact]
        public void ModesEndPayOrWait()
        {
            Assert.True(DeathRules.EndsContract(DeathMode.Permadeath));
            Assert.False(DeathRules.EndsContract(DeathMode.PayToRespawn));
            Assert.False(DeathRules.EndsContract(DeathMode.ReturnAfterDays));
            Assert.True(DeathRules.Pays(DeathMode.PayToRespawn));
            Assert.False(DeathRules.Pays(DeathMode.ReturnAfterDays));
        }

        [Fact]
        public void ReturnDelayIsTheCooldownOrTheDays()
        {
            Assert.Equal(600.0, DeathRules.ReturnDelay(DeathMode.PayToRespawn, 600, 3, 1800));
            Assert.Equal(5400.0, DeathRules.ReturnDelay(DeathMode.ReturnAfterDays, 600, 3, 1800)); // 3 days of 30 minutes
            Assert.Equal(900.0, DeathRules.ReturnDelay(DeathMode.ReturnAfterDays, 600, 0.5, 1800));
            Assert.Equal(0.0, DeathRules.ReturnDelay(DeathMode.ReturnAfterDays, 600, -1, 1800));
        }

        [Fact]
        public void OldPermadeathSettingMapsAcross()
        {
            Assert.Equal(DeathMode.Permadeath, DeathRules.FromPermadeathSetting("true"));
            Assert.Equal(DeathMode.PayToRespawn, DeathRules.FromPermadeathSetting(" False "));
            Assert.Null(DeathRules.FromPermadeathSetting("maybe"));
        }
    }
}
