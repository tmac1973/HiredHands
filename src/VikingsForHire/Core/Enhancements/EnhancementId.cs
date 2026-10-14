namespace VikingsForHire.Core.Enhancements
{
    /// <summary>
    /// An enhancement's name. The same name can belong to several jobs (Pack mule, Swift, Masterwork…), at different levels:
    /// an enhancement is the pair (job, name), and <see cref="EnhancementCatalog"/> says which names each job offers where.
    /// </summary>
    public enum EnhancementId
    {
        // Shared by several jobs.
        Masterwork, PackMule, StrongBack, Swift, StrongSwing, QuietWork,

        // Guard (melee).
        Toughened, ShieldTraining, HeavyArmour, SwiftStrikes, ShieldBash, SecondWind, Bodyguard, Taunt, Bloodthirst, Cleave,
        LastStand, Champion, RallyingCry,

        // Archer.
        SteadyAim, QuickDraw, EagleEye, LightFeet, FireArrows, FrostArrows, Hunter, Volley, Headshot, Marksman, Skirmisher,
        RainOfArrows, Elite,

        // Wizard.
        Focus, Haste, Warding, Blink, SecondarySpell, Mender, ElementalMastery, Protection, EntanglingRoots, LongReach,
        ChainSpell, Archmage, RaiseDead,

        // Woodcutter.
        Forester, SeedSaver, CleanCut, Lumberjack, FineEye, WideRange, Timber, MasterWoodsman,

        // Miner.
        DeepDigger, Prospector, RockBreaker, RichVeins, Tireless, GemEye, MasterMiner, HeavyHauler,

        // Steward.
        Brisk, KeeperOfFlames, Handyman, Bonesetter, Quartermaster, TidyMind, SmeltersHand, Watchman, ErrandRunner,
        FermentersTouch, MasterSteward, ShieldKeeper,

        // Farmer.
        GreenThumb, Bountiful, SeedKeeper, ClosePlanting, Forager, TenderCare, HardyStock, BiggerFields, DoubleHarvest, MasterFarmer,
        Replanter,

        // Cook.
        QuickHands, Frugal, SpitMaster, DoubleBatch, KeeperOfStoves, ChefsTouch, PrepChef, TwoStations, PantrySense, HeadChef,
        Banquet,

        // Rancher.
        GentleHand, Butcher, HeartyFeed, Herder, Skinner, Breeder, HerdGuard, StarBreeder, BigHerd, MasterRancher, Wrangler,
    }
}
