using System.Collections.Generic;
using System.Globalization;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>
    /// The shipped numbers for every enhancement, per job, as data-file keys "<Name>.<number>" (plan/enhancements phase 01).
    /// Shares and chances are fractions (0.10 = 10%); ranges in metres; cooldowns and durations in seconds.
    /// </summary>
    public static class EnhancementDefaults
    {
        private const string Text = @"
GuardMelee: Toughened.health 0.10, ShieldTraining.block 0.15, HeavyArmour.armor 4, SwiftStrikes.interval 0.15,
  ShieldBash.cooldown 12, ShieldBash.damage 10, SecondWind.below 0.30, SecondWind.heal 0.25, Masterwork.quality 1,
  Bodyguard.cooldown 15, Bodyguard.range 6, Taunt.cooldown 20, Taunt.range 8, Taunt.seconds 5, Bloodthirst.share 0.10,
  Cleave.extra 2, Cleave.share 0.5, Cleave.range 2.5, LastStand.cooldown 600, LastStand.seconds 3, Champion.damage 0.10,
  Champion.health 0.10, RallyingCry.damage 0.10, RallyingCry.range 15
GuardRanged: SteadyAim.damage 0.10, QuickDraw.interval 0.09, EagleEye.range 5, LightFeet.speed 0.06, FireArrows.damage 10,
  FireArrows.seconds 4, FrostArrows.slow 0.17, FrostArrows.seconds 3, FrostArrows.damage 3, Masterwork.quality 1,
  Hunter.damage 0.14, Hunter.range 20, Volley.cooldown 15, Volley.arrows 3, Headshot.chance 0.06, Headshot.mult 2,
  Marksman.damage 0.20, Marksman.range 15, Skirmisher.range 10, Skirmisher.seconds 6, RainOfArrows.cooldown 30,
  RainOfArrows.arrows 8, RainOfArrows.share 0.5, RainOfArrows.range 4, Elite.damage 0.06, Elite.health 0.06
Wizard: Focus.damage 0.09, Haste.interval 0.05, Warding.share 0.13, Blink.trigger 4, Blink.cooldown 20, Blink.distance 8,
  SecondarySpell.cooldown 20, SecondarySpell.nova 18, SecondarySpell.range 5, Mender.cooldown 15, Mender.range 15,
  Mender.heal 0.07, Masterwork.quality 1, ElementalMastery.mult 0.12, Protection.cooldown 60, Protection.absorb 90,
  EntanglingRoots.cooldown 90, LongReach.range 4, ChainSpell.range 6, ChainSpell.share 0.15, Archmage.damage 0.13,
  Archmage.interval 0.09, RaiseDead.cooldown 180, RaiseDead.seconds 20
Woodcutter: StrongSwing.gather 0.10, PackMule.slots 2, Forester.share 0.5, StrongBack.weight 50, SeedSaver.extra 1,
  CleanCut.gather 0.30, Masterwork.quality 1, Lumberjack.share 0.15, FineEye.share 0.10, WideRange.radius 0.20,
  Timber.range 4, Timber.maxHealth 100, Swift.speed 0.10, MasterWoodsman.gather 0.15, MasterWoodsman.share 0.15
Miner: StrongSwing.gather 0.10, PackMule.slots 2, DeepDigger.depth 2.5, StrongBack.weight 50, Prospector.radius 0.30,
  RockBreaker.gather 0.30, Masterwork.quality 1, RichVeins.share 0.15, Swift.speed 0.10, Tireless.share 0.25,
  GemEye.chance 0.10, MasterMiner.gather 0.15, MasterMiner.share 0.15, HeavyHauler.oreWeight 0.5
Smelter: Brisk.speed 0.10, PackMule.slots 2, KeeperOfFlames.refill 0.75, StrongBack.weight 50, Handyman.range 10,
  Handyman.quiet 10, Bonesetter.range 10, Bonesetter.rate 0.02, Quartermaster.days 3, TidyMind.seconds 20,
  TidyMind.rangeMult 2, SmeltersHand.mult 2, Watchman.damage 0.25, ErrandRunner.range 10, ErrandRunner.chain 4,
  FermentersTouch.share 0.20, MasterSteward.speed 0.15, ShieldKeeper.rangeMult 2
Farmer: GreenThumb.speed 0.10, PackMule.slots 2, Bountiful.chance 0.25, SeedKeeper.chance 0.25, ClosePlanting.space 0.15,
  Swift.speed 0.10, StrongBack.weight 50, TenderCare.share 0.15, BiggerFields.radius 0.20, DoubleHarvest.chance 0.10,
  MasterFarmer.speed 0.15, MasterFarmer.share 0.15
Cook: QuickHands.speed 0.10, PackMule.slots 2, Frugal.chance 0.15, DoubleBatch.chance 0.20, Swift.speed 0.10,
  ChefsTouch.share 0.10, StrongBack.weight 50, PantrySense.rangeMult 2, HeadChef.speed 0.15, HeadChef.saveChance 0.05,
  HeadChef.extraChance 0.05, Banquet.share 0.25
Rancher: GentleHand.range 2, PackMule.slots 2, Butcher.chance 0.33, HeartyFeed.share 0.50, Herder.seconds 180,
  Skinner.chance 0.33, Swift.speed 0.10, StrongBack.weight 50, Breeder.share 0.15, HerdGuard.damage 0.25,
  StarBreeder.chance 0.10, BigHerd.extra 2, MasterRancher.speed 0.15, MasterRancher.chance 0.10, Wrangler.cooldown 300
";

        /// <summary>Numbers that are fractions of a whole: the validator keeps them at 1 or less.</summary>
        public static readonly HashSet<string> UnitNames = new()
        {
            "chance", "share", "slow", "refill", "below", "heal", "rate", "oreWeight", "interval", "saveChance", "extraChance",
        };

        public static Dictionary<JobType, Dictionary<string, float>> Create()
        {
            var all = new Dictionary<JobType, Dictionary<string, float>>();
            Dictionary<string, float>? current = null;
            foreach (string raw in Text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                    continue;
                int colon = line.IndexOf(':');
                if (colon > 0 && char.IsUpper(line[0]) && line.Substring(0, colon).IndexOf('.') < 0 &&
                    System.Enum.TryParse(line.Substring(0, colon), out JobType job))
                {
                    current = all[job] = new Dictionary<string, float>();
                    line = line.Substring(colon + 1);
                }
                foreach (string part in line.Split(','))
                {
                    string[] kv = part.Trim().Split(' ');
                    if (kv.Length == 2 && current != null)
                        current[kv[0]] = float.Parse(kv[1], CultureInfo.InvariantCulture);
                }
            }
            return all;
        }
    }
}
