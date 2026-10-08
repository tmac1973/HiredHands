namespace VikingsForHire.Core
{
    /// <summary>How good hirelings are at fighting, chosen by the server (setting CombatSkill): a few presets, not dials.</summary>
    public enum CombatSkillPreset
    {
        /// <summary>No blocking or dodging: the 0.5.0 fighting.</summary>
        Off,
        /// <summary>Block and roll now and then; you still have to look after them.</summary>
        Green,
        /// <summary>The levels table as measured (the default).</summary>
        Trained,
        /// <summary>Parry most swings, roll often.</summary>
        Veteran,
    }

    /// <summary>
    /// The skills a preset scales. Each is a chance from the levels table (or, for cooldowns, a time). Later fighters add
    /// theirs here (a caster aiming a spell, a healer timing a heal) and get the presets for free.
    /// </summary>
    public enum CombatSkillKind
    {
        /// <summary>Seeing an attack coming in time (readChance).</summary>
        Read,
        /// <summary>Timing a block as a parry (parryChance).</summary>
        Parry,
        /// <summary>Rolling out of a big hit (dodgeChance).</summary>
        Dodge,
    }

    /// <summary>
    /// What a preset does to the per-level numbers. Pure, so it's unit tested; the levels table keeps the shape of the
    /// curve (level 1 to 8) and the preset moves the whole curve up or down.
    /// </summary>
    public static class CombatSkills
    {
        /// <summary>No chance goes above this, whatever the preset (nobody parries everything).</summary>
        public const float MaxChance = 0.95f;

        public static bool On(CombatSkillPreset preset) => preset != CombatSkillPreset.Off;

        /// <summary>The chance multiplier for a skill. All skills scale alike today; a later skill can be given its own.</summary>
        public static float Multiplier(CombatSkillPreset preset, CombatSkillKind kind) => preset switch
        {
            CombatSkillPreset.Off => 0f,
            CombatSkillPreset.Green => 0.6f,
            CombatSkillPreset.Veteran => 1.3f,
            _ => 1f,
        };

        /// <summary>A level's chance for a skill under the preset, capped at <see cref="MaxChance"/> (left alone at Trained).</summary>
        public static float Chance(CombatSkillPreset preset, CombatSkillKind kind, float levelChance)
        {
            float m = Multiplier(preset, kind);
            if (m == 1f)
                return levelChance;
            float c = levelChance * m;
            return c > MaxChance ? MaxChance : c < 0f ? 0f : c;
        }

        /// <summary>A level's cooldown (seconds) under the preset: longer when green, shorter for veterans.</summary>
        public static float Cooldown(CombatSkillPreset preset, float levelCooldown) => preset switch
        {
            CombatSkillPreset.Green => levelCooldown * 1.5f,
            CombatSkillPreset.Veteran => levelCooldown * 0.7f,
            _ => levelCooldown,
        };
    }
}
