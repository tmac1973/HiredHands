using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class CombatSkillTests
    {
        [Fact]
        public void TrainedIsTheLevelsTable()
        {
            Assert.Equal(0.63f, CombatSkills.Chance(CombatSkillPreset.Trained, CombatSkillKind.Read, 0.63f));
            Assert.Equal(0.97f, CombatSkills.Chance(CombatSkillPreset.Trained, CombatSkillKind.Read, 0.97f)); // a data file's own number stands
            Assert.Equal(4.9f, CombatSkills.Cooldown(CombatSkillPreset.Trained, 4.9f));
        }

        [Fact]
        public void PresetsMoveTheCurve()
        {
            Assert.Equal(0f, CombatSkills.Chance(CombatSkillPreset.Off, CombatSkillKind.Dodge, 0.5f));
            Assert.Equal(0.3f, CombatSkills.Chance(CombatSkillPreset.Green, CombatSkillKind.Dodge, 0.5f), 4);
            Assert.Equal(0.65f, CombatSkills.Chance(CombatSkillPreset.Veteran, CombatSkillKind.Dodge, 0.5f), 4);
            Assert.Equal(CombatSkills.MaxChance, CombatSkills.Chance(CombatSkillPreset.Veteran, CombatSkillKind.Read, 0.95f)); // capped
            Assert.Equal(9f, CombatSkills.Cooldown(CombatSkillPreset.Green, 6f), 4);
            Assert.Equal(1.4f, CombatSkills.Cooldown(CombatSkillPreset.Veteran, 2f), 4);
        }

        [Fact]
        public void OnlyOffTurnsItOff()
        {
            Assert.False(CombatSkills.On(CombatSkillPreset.Off));
            Assert.True(CombatSkills.On(CombatSkillPreset.Green));
            Assert.True(CombatSkills.On(CombatSkillPreset.Veteran));
        }
    }
}
