using System.Collections.Generic;
using System.Linq;
using static VikingsForHire.Core.Enhancements.EnhancementId;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>
    /// Each job's seven 1-of-2 pairs, one at each level from 2 to 8 (plan/enhancements). Which pairs exist is fixed here;
    /// their numbers are in the data file (<see cref="EnhancementNumbers"/>).
    /// </summary>
    public static class EnhancementCatalog
    {
        public const int FirstLevel = 2;
        public const int LastLevel = 8;
        public const int Levels = LastLevel - FirstLevel + 1;

        private static readonly Dictionary<JobType, (EnhancementId A, EnhancementId B)[]> Pairs = new()
        {
            [JobType.GuardMelee] = new[]
            {
                (Toughened, ShieldTraining), (HeavyArmour, SwiftStrikes), (ShieldBash, SecondWind), (Masterwork, Bodyguard),
                (Taunt, Bloodthirst), (Cleave, LastStand), (Champion, RallyingCry),
            },
            [JobType.GuardRanged] = new[]
            {
                (SteadyAim, QuickDraw), (EagleEye, LightFeet), (FireArrows, FrostArrows), (Masterwork, Hunter),
                (Volley, Headshot), (Marksman, Skirmisher), (RainOfArrows, Elite),
            },
            [JobType.Wizard] = new[]
            {
                (Focus, Haste), (Warding, Blink), (SecondarySpell, Mender), (Masterwork, ElementalMastery),
                (Protection, EntanglingRoots), (LongReach, ChainSpell), (Archmage, RaiseDead),
            },
            [JobType.Woodcutter] = new[]
            {
                (StrongSwing, PackMule), (Forester, StrongBack), (SeedSaver, CleanCut), (Masterwork, Lumberjack),
                (FineEye, WideRange), (Timber, Swift), (MasterWoodsman, QuietWork),
            },
            [JobType.Miner] = new[]
            {
                (StrongSwing, PackMule), (DeepDigger, StrongBack), (Prospector, RockBreaker), (Masterwork, RichVeins),
                (QuietWork, Swift), (Tireless, GemEye), (MasterMiner, HeavyHauler),
            },
            [JobType.Smelter] = new[]
            {
                (Brisk, PackMule), (KeeperOfFlames, StrongBack), (Handyman, Bonesetter), (Quartermaster, TidyMind),
                (SmeltersHand, Watchman), (ErrandRunner, FermentersTouch), (MasterSteward, ShieldKeeper),
            },
            [JobType.Farmer] = new[]
            {
                (GreenThumb, PackMule), (Bountiful, SeedKeeper), (ClosePlanting, Forager), (Swift, StrongBack),
                (TenderCare, HardyStock), (BiggerFields, DoubleHarvest), (MasterFarmer, Replanter),
            },
            [JobType.Cook] = new[]
            {
                (QuickHands, PackMule), (Frugal, SpitMaster), (DoubleBatch, KeeperOfStoves), (Swift, ChefsTouch),
                (PrepChef, StrongBack), (TwoStations, PantrySense), (HeadChef, Banquet),
            },
            [JobType.Rancher] = new[]
            {
                (GentleHand, PackMule), (Butcher, HeartyFeed), (Herder, Skinner), (Swift, StrongBack),
                (Breeder, HerdGuard), (StarBreeder, BigHerd), (MasterRancher, Wrangler),
            },
        };

        public static IEnumerable<JobType> Jobs => Pairs.Keys;

        /// <summary>The two options at a level (2-8) for a job.</summary>
        public static (EnhancementId A, EnhancementId B) Pair(JobType job, int level) => Pairs[job][level - FirstLevel];

        /// <summary>The option a choice ('A' or 'B') at a level means.</summary>
        public static EnhancementId Option(JobType job, int level, char choice) =>
            choice == 'B' ? Pair(job, level).B : Pair(job, level).A;

        /// <summary>The level at which a job offers a name, or 0 when it doesn't.</summary>
        public static int LevelOf(JobType job, EnhancementId id)
        {
            (EnhancementId A, EnhancementId B)[] pairs = Pairs[job];
            for (int i = 0; i < pairs.Length; i++)
                if (pairs[i].A == id || pairs[i].B == id)
                    return i + FirstLevel;
            return 0;
        }

        public static bool Offers(JobType job, EnhancementId id) => LevelOf(job, id) > 0;

        /// <summary>Every (job, name) the catalog offers.</summary>
        public static IEnumerable<(JobType Job, EnhancementId Id)> All() =>
            Pairs.SelectMany(kv => kv.Value.SelectMany(p => new[] { (kv.Key, p.A), (kv.Key, p.B) }));
    }
}
