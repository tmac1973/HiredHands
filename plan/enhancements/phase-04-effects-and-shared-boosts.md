# Phase 04 — The effects framework and the shared number boosts

**Depends on:** 01 (catalog, numbers), 02 (the hireling's `vfh_enh` copy, the switch, the `enhance` fixture) ·
**Enables:** 05-11 (each job's behaviours use the framework and its test checks; the shared boosts are already done)

## Goal
One place every effect asks "does this hireling have X, and what's its number": a per-hireling view of its picks and
numbers, refreshed when the picks or the data change. With it, all the number boosts that several jobs share are applied:
health, armour, damage dealt and taken, attack and cast speed, move speed, cargo slots and weight, gathering speed, work
radius, and Masterwork (+1 item quality on the main weapon or tool).

## Files touched
- `src/VikingsForHire/Hirelings/Enhancements/HirelingEnhancements.cs` — new: owned by `Hireling` (`Hireling.Enh`);
  parses `vfh_enh` into `EnhancementPicks`; `Has(EnhancementId)` (false whenever `VfhConfig.Enhancements` is off);
  `Value(EnhancementId, name)` from `EnhancementNumbers`; refreshed when the ZDO string or `DataStore` revision changes.
- `src/VikingsForHire/Core/Enhancements/EnhancementMath.cs` — new pure helpers: `HealthMult`, `ArmorBonus`,
  `DamageMult`, `DamageTakenMult`, `AttackIntervalMult`, `MoveSpeedMult`, `CargoSlotsBonus`, `CargoWeightBonus`,
  `GatherMult`, `RadiusMult`, `QualityBonus`, each from a set of chosen ids and their numbers; unit-tested.
- `src/VikingsForHire/Hirelings/Hireling.cs` — the level/job poll (lines ~397-408) also re-applies on an `Enh` change:
  `ApplyLevelStats` sets max health × `HealthMult`; `Armor` adds `ArmorBonus`; `CargoSlots` and `CargoWeightLimit` add
  their bonuses (clamped to the grid); `Radius` × `RadiusMult` (capped at the board's max for the job × the multiplier).
- `src/VikingsForHire/Hirelings/DamagePatches.cs` — `DamageMultiplier` × `DamageMult`; the incoming-damage path ×
  `DamageTakenMult`.
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs` — melee, ranged and cast cooldowns × `AttackIntervalMult`.
- `src/VikingsForHire/Hirelings/HirelingAI.cs` — `ApplyRunBoost`'s base speed × `MoveSpeedMult` (and walk speed).
- `src/VikingsForHire/Hirelings/Work/GatherBehaviour.cs` — swing damage × `GatherMult`.
- `src/VikingsForHire/Hirelings/GearApplier.cs` — after equipping, the main hand item's `m_quality` += `QualityBonus`
  (capped at the item's `m_maxQuality`), durability refilled to the new maximum; re-run on an `Enh` change.
- `src/VikingsForHire/Testing/FixturesHireling.cs` — check fields `maxhealth` (exists), `armor` (exists), `cargo_slots`,
  `cargo_weight_limit` (new), `run_speed` (new), `radius` (new), `gear.right_quality` (new), `attack_interval` (new),
  `damage_mult` (new: the multiplier `DamagePatches` would apply to this hireling's next hit, optionally against a tagged
  target and distance: `damage_mult <tag>`).
- `src/VikingsForHire/Testing/FixturesCombat.cs` — the checks every behaviour phase uses: `enh_events <technique>` (times a
  technique fired since `enh_events_reset`, from a counter every technique increments), `enemy_status <tag|nearest>
  <status>` (seconds left of a status effect such as Burning or Frost, 0 when none), `enemy_staggered <tag|nearest>`,
  `enemy_target <tag|nearest>` (name of the creature's current target); fixture `strike_hireling <damage>` (an enemy-
  attributed hit on the last spawned hireling, bypassing test protection); `enemies` gains an optional last argument, a
  tag for the spawned creatures; check `enemies_damaged <radius>` (enemies within the radius below full health); fixture
  `data_set <job> <EnhancementId.number> <value>` (sets one enhancement number in the loaded data, local to the test game)
  and `data_reset` (reloads the file); check field `gather_mult` (the swing multiplier `GatherBehaviour` would use); check field `dist_to <tag>` (metres from the
  hireling to a tagged object or creature).
- `tests/VikingsForHire.Tests/EnhancementMathTests.cs` — new.
- `test/alias_vfh.yaml` — rows VFH-ENH-5, -5b, -6, -6b … -6f, -7, -8.

## Steps
1. Build `HirelingEnhancements` and hook it into `Hireling`; nothing reads it yet.
2. Implement `EnhancementMath` for these enhancements (numbers from the data file; defaults in brackets):
   - Guard: Toughened (health +0.10), Heavy armour (armor +4), Swift strikes (interval −0.15), Masterwork blade
     (quality +1), Champion (damage +0.10, health +0.10).
   - Archer: Steady aim (damage +0.10), Quick draw (interval −0.15), Light feet (speed +0.10), Masterwork bow, Elite
     (damage +0.10, health +0.10).
   - Wizard: Focus (damage +0.10), Haste (interval −0.15), Warding (taken −0.15), Masterwork staff, Archmage (damage
     +0.15, interval −0.10).
   - Woodcutter: Strong swing (gather +0.10), Pack mule (slots +2), Strong back (weight +50), Masterwork axe, Wide range
     (radius +0.20), Swift (speed +0.10), Master woodsman's swing (gather +0.15; its yield is phase 08).
   - Miner: Strong swing, Pack mule, Strong back, Masterwork pickaxe, Swift, Master miner's swing (gather +0.15).
   - Steward: Brisk (speed +0.10), Pack mule, Strong back.
   - Farmer: Pack mule, Swift, Strong back, Bigger fields (radius +0.20).
   - Cook and Rancher: Pack mule, Swift, Strong back.
   Multipliers stack multiplicatively with the level table, CombatSkill, `guardDamageMult` and the damage configs, which
   apply first.
3. Apply each where listed. Masterwork: the quality bonus is applied to the equipped main item only (not ammo or the
   sidearm).
4. Unit tests for every helper, including stacking (Champion and Toughened together: health × 1.21) and the off switch
   (empty set → all 1 / 0).
5. Rows:
   - VFH-ENH-5: a level 8 Guard (`post GuardMelee 8 20 free`): note `maxhealth`, `armor`, `gear.right_quality`; pick
     Toughened (2A), Heavy armour (3A), Masterwork blade (5A), Champion (8A): health × 1.21, armor +4, quality 2.
   - VFH-ENH-5b: a second level 8 Guard: note `attack_interval`; Swift strikes (3B): × 0.85.
   - VFH-ENH-6: a level 8 Woodcutter: Pack mule (2B), Strong back (3B), Wide range (6B), Swift (7B): `cargo_slots` +2,
     `cargo_weight_limit` +50, `radius` × 1.2, `run_speed` × 1.1; a second one with Strong swing (2A), Master woodsman (8A):
     `gather_mult` × 1.265.
   - VFH-ENH-6b: a level 8 Miner: Pack mule (2B), Strong back (3B), Masterwork pickaxe (5A), Swift (6B), Master miner (8A):
     slots +2, weight +50, `gear.right_quality` 2, speed × 1.1, `gather_mult` × 1.15.
   - VFH-ENH-6c: a level 8 Steward: Brisk (2A), Strong back (3B): speed × 1.1, weight +50; a second: Pack mule (2B): slots +2.
   - VFH-ENH-6d: a level 8 Farmer: Pack mule (2B), Swift (5A), Bigger fields (7A): slots +2, speed × 1.1, radius × 1.2.
   - VFH-ENH-6e: a level 8 Cook: Pack mule (2B), Swift (5A), Strong back (6B): slots +2, speed × 1.1, weight +50.
   - VFH-ENH-6f: a level 8 Rancher: Pack mule (2B), Swift (5A): slots +2, speed × 1.1; a second: Strong back (5B): +50.
   - VFH-ENH-7: `cfg_set Enhancements false` on the guard from VFH-ENH-5: values back to base; `true`: back again.
   - VFH-ENH-8: a level 8 Archer: `damage_mult` noted; Steady aim → × 1.10 (±0.01); Elite → × 1.21; a level 8 Wizard with
     Warding loses 0.85 × the health a plain level 8 wizard in the same row loses to `strike_hireling 100`.
   - Regression `combat1 stance4 noeffect` still passes.

## Build gate
- Compile check; `dotnet test tests/VikingsForHire.Tests`.
- In game: VFH-ENH-1..3, 5, 5b, 6, 6b-6f, 7, 8 pass; regression `vfh_test_chain combat1 combat5 stance4 stance5 block1 dodge1 noeffect work1
  work3 tier1 store1 deliver1 cargo_weight` passes.

## Test plan
Unit tests of the maths; rows reading each stat before and after picks; the off switch; combat and work regression.

## Commit
`feat(enhancements): per-hireling effects view; shared boosts (health, armour, damage, speed, cargo, gathering, radius, Masterwork quality) applied`

## Rollback
Each hook multiplies by 1 (or adds 0) with no picks or with the switch off, so reverting is safe; turning
`Enhancements` off on a server disables every effect without a new build.
