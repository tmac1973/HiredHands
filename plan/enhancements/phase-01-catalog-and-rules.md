# Phase 01 — Enhancement catalog, picks and rules

**Depends on:** nothing · **Enables:** 02 (contracts and ops store picks), 04 (effects read the catalog and numbers)

## Goal
The pure, game-free core: every job's seven 1-of-2 pairs as named enhancements, a compact way to store a hireling's
picks, the rules for what's owed, what may be chosen and what a respec costs, and the data file section that holds every
enhancement's numbers (server-tunable, filled in for older files). Everything here is unit-tested without the game.

## Files touched
- `src/VikingsForHire/Core/Enhancements/EnhancementId.cs` — new: `enum EnhancementId`, one value per distinct name in the
  catalog (`Toughened`, `ShieldTraining`, … `Wrangler`; about 100 names, since `PackMule`, `StrongBack`, `Swift`,
  `StrongSwing`, `QuietWork`, `Masterwork` and a few others appear in several jobs). An enhancement is identified by the
  pair (job, name): 126 enhancements (63 pairs), and the same name may sit at different levels in different jobs.
- `src/VikingsForHire/Core/Enhancements/EnhancementCatalog.cs` — new: `Pair(JobType job, int level)` →
  `(EnhancementId A, EnhancementId B)` for levels 2-8 and every job; `LevelOf(JobType job, EnhancementId id)` (or none);
  `Offers(JobType job, EnhancementId id)`.
- `src/VikingsForHire/Core/Enhancements/EnhancementPicks.cs` — new: an immutable value parsed from / written to a 7-char
  string, one char per level 2-8: `A`, `B` or `-` (not chosen). `Empty = "-------"`. `Get(level)`, `With(level, choice)`,
  `Ids(job)` (the chosen `EnhancementId`s, using the job's catalog), `Has(job, EnhancementId)`. Unknown or short strings parse as empty for the
  missing levels.
- `src/VikingsForHire/Core/Enhancements/EnhancementRules.cs` — new: `Owed(picks, level)` (levels 2..level with `-`),
  `CanChoose(picks, hirelingLevel, level)` (2 ≤ level ≤ hirelingLevel and not yet chosen), `RespecCost(CostCalculator,
  job, level, fraction)` = half (by fraction) of `HireCost(job, level)` in coins and food, rounded up per part.
- `src/VikingsForHire/Core/Data/VfhData.cs` — `VfhData.Enhancements`: `Dictionary<JobType, Dictionary<string, float>>`,
  keyed `"<EnhancementId>.<number>"` (e.g. `"Toughened.health": 0.10`); `VfhData.EnhancementSettings` with
  `RespecCostFraction` (0.5).
- `src/VikingsForHire/Core/Data/DefaultData.cs` — the default numbers for all 126 enhancements, per job (the overview's catalog).
- `src/VikingsForHire/Core/Data/DataDefaults.cs` — `Migrate`: add any missing job dictionary and any missing
  `"<id>.<number>"` key from the defaults (older files, and files from a version with fewer numbers).
- `src/VikingsForHire/Core/Enhancements/EnhancementNumbers.cs` — new: `Get(data, job, id, name)` with the default as fallback.
- `src/VikingsForHire/Core/Data/DataValidator.cs` — numbers must be finite and ≥ 0; numbers whose name is `chance`,
  `share`, `slow`, `refill`, `below`, `heal`, `rate`, `oreWeight`, `interval`, `saveChance` or `extraChance` must also be
  ≤ 1; `RespecCostFraction` 0-1.
- `tests/VikingsForHire.Tests/EnhancementTests.cs` — new.
- `tests/VikingsForHire.Tests/DataDefaultsTests.cs` — a pre-enhancements file gains the full section.

## Steps
1. Define `EnhancementId` and the catalog exactly as in the overview's *Draft enhancement catalog* (as amended:
   Masterwork at level 5 for Guard, Archer, Wizard, Woodcutter, Miner; Heavy hauler, Bonesetter, Errand runner, Replanter,
   Banquet, Hearty feed, Herd guard). Job names map to `JobType`: Guard = `GuardMelee`, Archer = `GuardRanged`, Steward = `Smelter`.
2. Write `EnhancementPicks` with the 7-char codec; `With` returns a new value; a level outside 2-8 throws.
3. Write `EnhancementRules`. `Owed` lists levels in ascending order. `RespecCost` uses
   `CostCalculator.HireCost(job, level)` and multiplies each of food and coins by the fraction, rounding up.
4. Add the data section and defaults. Every number an effect in phases 04-11 reads is declared here with its default;
   the key names are fixed now, always `<EnhancementId>.<number>` (e.g. `ShieldBash.cooldown`, `RallyingCry.damage`,
   `RainOfArrows.arrows`, `SecondarySpell.cooldown`, `ElementalMastery.mult`, `EntanglingRoots.cooldown`,
   `ChainSpell.range`, `ErrandRunner.chain`, `HeavyHauler.oreWeight`); later phases use exactly these names. `EnhancementNumbers.Get(data, job, id, name)` returns the value, falling back to the default.
5. Unit tests: every job has 7 pairs with 14 distinct names within the job; `LevelOf` finds each pair's names at its level for its job and none for other jobs' names; picks codec round-trips and
   tolerates bad strings; `Owed`/`CanChoose` edge cases (level 1 owes nothing, level 5 owes 2-5, a chosen level can't be
   chosen again); respec cost for level 4 and 8 archers; every catalog id has its numbers in the defaults; an older YAML
   without `enhancements` loads with the full section.

## Data keys (complete; defaults)
Every number an effect reads, as `<Name>.<number>: default` under its job. Phases 04-11 use exactly these.
- **GuardMelee:** Toughened.health 0.10 · ShieldTraining.block 0.15 · HeavyArmour.armor 4 · SwiftStrikes.interval 0.15 ·
  ShieldBash.cooldown 12, .damage 10 · SecondWind.below 0.30, .heal 0.25 · Masterwork.quality 1 · Bodyguard.cooldown 15,
  .range 6 · Taunt.cooldown 20, .range 8, .seconds 5 · Bloodthirst.share 0.10 · Cleave.extra 2, .share 0.5, .range 2.5 ·
  LastStand.cooldown 600, .seconds 3 · Champion.damage 0.10, .health 0.10 · RallyingCry.damage 0.10, .range 15.
- **GuardRanged:** SteadyAim.damage 0.10 · QuickDraw.interval 0.15 · EagleEye.range 5 · LightFeet.speed 0.10 ·
  FireArrows.damage 10, .seconds 4 · FrostArrows.slow 0.30, .seconds 3, .damage 5 · Masterwork.quality 1 ·
  Hunter.damage 0.25, .range 20 · Volley.cooldown 15, .arrows 3 · Headshot.chance 0.10, .mult 2 · Marksman.damage 0.20,
  .range 15 · Skirmisher.range 10, .seconds 6 · RainOfArrows.cooldown 30, .arrows 8, .share 0.5, .range 4 ·
  Elite.damage 0.10, .health 0.10.
- **Wizard:** Focus.damage 0.10 · Haste.interval 0.15 · Warding.share 0.15 · Blink.trigger 4, .cooldown 10, .distance 8 ·
  SecondarySpell.cooldown 20, .nova 20, .range 5 · Mender.cooldown 15, .range 15, .heal 0.10 · Masterwork.quality 1 ·
  ElementalMastery.mult 0.30 · Protection.cooldown 60, .absorb 100 · EntanglingRoots.cooldown 30 · LongReach.range 4 ·
  ChainSpell.range 6, .share 0.5 · Archmage.damage 0.15, .interval 0.10 · RaiseDead.cooldown 120, .seconds 60.
- **Woodcutter:** StrongSwing.gather 0.10 · PackMule.slots 2 · Forester.share 0.5 (planting interval ×) ·
  StrongBack.weight 50 · SeedSaver.extra 1 · CleanCut.gather 0.30 · Masterwork.quality 1 · Lumberjack.share 0.15 ·
  FineEye.share 0.10 · WideRange.radius 0.20 · Timber.range 4, .maxHealth 100 · Swift.speed 0.10 ·
  MasterWoodsman.gather 0.15, .share 0.15 · QuietWork (no numbers).
- **Miner:** StrongSwing.gather 0.10 · PackMule.slots 2 · DeepDigger.depth 2.5 · StrongBack.weight 50 ·
  Prospector.radius 0.30 · RockBreaker.gather 0.30 · Masterwork.quality 1 · RichVeins.share 0.15 · QuietWork (none) ·
  Swift.speed 0.10 · Tireless.share 0.25 · GemEye.chance 0.10 · MasterMiner.gather 0.15, .share 0.15 ·
  HeavyHauler.oreWeight 0.5.
- **Smelter (Steward):** Brisk.speed 0.10 · PackMule.slots 2 · KeeperOfFlames.refill 0.75 · StrongBack.weight 50 ·
  Handyman.range 10, .quiet 10 · Bonesetter.range 10, .rate 0.02 · Quartermaster.days 3 · TidyMind.seconds 20,
  .rangeMult 2 · SmeltersHand.mult 2 · Watchman.damage 0.25 · ErrandRunner.range 10, .chain 4 · FermentersTouch.share
  0.20 · MasterSteward.speed 0.15 · ShieldKeeper.rangeMult 2.
- **Farmer:** GreenThumb.speed 0.10 · PackMule.slots 2 · Bountiful.chance 0.25 · SeedKeeper.chance 0.25 · Sower.extra 4
  (plants per trip) · Forager (none) · Swift.speed 0.10 · StrongBack.weight 50 · TenderCare.share 0.15 · HardyStock (none)
  · BiggerFields.radius 0.20 · DoubleHarvest.chance 0.10 · MasterFarmer.speed 0.15, .share 0.15 · Replanter (none).
- **Cook:** QuickHands.speed 0.10 · PackMule.slots 2 · Frugal.chance 0.15 · SpitMaster (none) · DoubleBatch.chance 0.20 ·
  KeeperOfStoves (none) · Swift.speed 0.10 · ChefsTouch.share 0.10 · PrepChef (none) · StrongBack.weight 50 ·
  TwoStations (none) · PantrySense.rangeMult 2 · HeadChef.speed 0.15, .saveChance 0.05, .extraChance 0.05 ·
  Banquet.share 0.25.
- **Rancher:** GentleHand.range 2 · PackMule.slots 2 · Butcher.chance 0.33 · HeartyFeed.share 0.50 · Herder.seconds 180 ·
  Skinner.chance 0.33 · Swift.speed 0.10 · StrongBack.weight 50 · Breeder.share 0.15 · HerdGuard.damage 0.25 ·
  StarBreeder.chance 0.10 · BigHerd.extra 2 · MasterRancher.speed 0.15, .chance 0.10 · Wrangler.cooldown 300.

## Build gate
- `dotnet build src/VikingsForHire/VikingsForHire.csproj -c Release -t:ResolveReferences;CoreCompile` (no deploy needed).
- `dotnet test tests/VikingsForHire.Tests` — all pass.

## Test plan
Unit tests only (above). Nothing in game changes yet.

## Commit
`feat(enhancements): catalog of 126 enhancements (7 pairs per job), picks codec, owed/choose/respec rules, data section with defaults`

## Rollback
Self-contained new files plus an additive data section; reverting the commit removes them. The data file reader rejects
unknown keys, so a data file written by this build fails validation in an older build (which then uses its defaults and
logs why): after a rollback, delete the `enhancements` and `enhancementSettings` sections from the server's file.
