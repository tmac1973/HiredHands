using System;
using System.Collections.Generic;
using VikingsForHire.Core.Data;
using static VikingsForHire.Core.Enhancements.EnhancementId;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>
    /// The shared number boosts (plan/enhancements phase 04): what a hireling's picks do to its health, armour, damage,
    /// attack speed, move speed, cargo, gathering, radius and gear quality. Multipliers stack multiplicatively with each
    /// other and on top of the level table and the server's damage settings, which apply first. Everything is 1 (or 0)
    /// with no picks.
    /// </summary>
    public sealed class EnhancementEffects
    {
        public static readonly EnhancementEffects None = new(JobType.Woodcutter, EnhancementPicks.Parse(null), null);

        private readonly JobType _job;
        private readonly EnhancementPicks _picks;
        private readonly VfhData? _data;

        public EnhancementEffects(JobType job, EnhancementPicks picks, VfhData? data)
        {
            _job = job;
            _picks = picks;
            _data = data;
        }

        public bool Has(EnhancementId id) => _picks.Has(_job, id);

        /// <summary>Any pick at all (a quick way out for the techniques).</summary>
        public bool AnyPicks => _picks.Any;

        /// <summary>The number <paramref name="name"/> of an enhancement it has, else 0.</summary>
        public float Value(EnhancementId id, string name) => Has(id) ? EnhancementNumbers.Get(_data, _job, id, name) : 0f;

        // Unconditional boosts only; the ones that depend on a target or a situation live with their behaviour.
        private static readonly EnhancementId[] Health = { Toughened, Champion, Elite };
        private static readonly EnhancementId[] Damage = { SteadyAim, Champion, Elite, Focus, Archmage };
        private static readonly EnhancementId[] Interval = { SwiftStrikes, QuickDraw, Haste, Archmage };
        private static readonly EnhancementId[] Speed = { Swift, LightFeet, Brisk };
        private static readonly EnhancementId[] Gather = { StrongSwing, MasterWoodsman, MasterMiner };
        private static readonly EnhancementId[] Radius = { WideRange, BiggerFields };

        public float HealthMult => Grow(Health, "health");
        public float DamageMult => Grow(Damage, "damage");
        public float DamageTakenMult => 1f - Value(Warding, "share");
        public float AttackIntervalMult => Shrink(Interval, "interval");
        public float MoveSpeedMult => Grow(Speed, "speed");
        public float GatherMult => Grow(Gather, "gather");
        public float RadiusMult => Grow(Radius, "radius");
        public float ArmorBonus => Value(HeavyArmour, "armor");
        public int CargoSlotsBonus => (int)Math.Round(Value(PackMule, "slots"));
        public float CargoWeightBonus => Value(StrongBack, "weight");
        public int QualityBonus => (int)Math.Round(Value(Masterwork, "quality"));

        private float Grow(IEnumerable<EnhancementId> ids, string name)
        {
            float m = 1f;
            foreach (EnhancementId id in ids)
                m *= 1f + Value(id, name);
            return m;
        }

        private float Shrink(IEnumerable<EnhancementId> ids, string name)
        {
            float m = 1f;
            foreach (EnhancementId id in ids)
                m *= 1f - Value(id, name);
            return m;
        }
    }
}
