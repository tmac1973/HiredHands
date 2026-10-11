# Phase 05 — What a wizard's spells may hit

**Depends on:** 01 (players), 02 (`WizardSpellsHit`, `JobType.Wizard`), 04 (wizards cast) · **Enables:** 06

## Goal
A wizard's spells hurt enemies only: never players (phase 01), hirelings, tames or buildings (already blocked for all
hirelings by `DamagePatches`). `WizardSpellsHit` decides props: `EnemiesOnly` (default) spares destructible props
(greydwarf nests, bushes and the like, and trees, logs and rocks, though those only take chop and pickaxe damage anyway);
`EnemiesAndTerrain` lets a blast damage them, as a player's staff would.

## Files touched
- `src/VikingsForHire/Core/SpellRules.cs` (new, Unity-free): `MayHitProp(JobType job, SpellsHit setting)`: wizards only
  with `EnemiesAndTerrain`; every other job always (woodcutters and miners work by hitting props). Players, hirelings,
  tames and buildings aren't decided here: phase 01 and the existing patches block them for every hireling.
- `src/VikingsForHire/Hirelings/DamagePatches.cs`: prefix patches on `Destructible.Damage`, `TreeBase.Damage`,
  `TreeLog.Damage`, `MineRock.Damage` and `MineRock5.Damage`: when the attacker is a hireling and `MayHitProp` says no,
  skip the hit (`hireling.prop_hit_blocked` at trace).
- `src/VikingsForHire/Testing/FixturesCombat.cs`: `enemies <prefab> <n> at <tag>` (spawns within 0.5 m of a tagged
  object, AI off for as long as it lives, like phase 04's `still`); fixture `blast_by_wizard <radius> at <tag>`; check
  `enemy_health_pct` (the nearest living enemy's health / max).
- `src/VikingsForHire/Testing/FixturesWork.cs`: `object <prefab> <tag> <dist>` (any prefab placed that far ahead of
  you, tagged; its `SpawnArea`, if it has one, is switched off, so a nest doesn't spawn greydwarves into the test); `at <tag>` as a last argument on `tame`, `fire` and `hirelings` (place it
  beside the tagged object instead); `player_offset <tag> <m>` (move yourself that far from a tagged object); check
  `object_health <tag>` (0..1: a character's health percentage; a destructible's, tree's or rock's `ZDOVars.s_health`
  against its max; a piece's `WearNTear` health).
- `src/VikingsForHire/Testing/FixturesHireling.cs`: field `health_pct` (health / max) on the `hireling` check.
- `test/alias_vfh.yaml`: rows VFH-WIZ-6, -7.
- `tests/VikingsForHire.Tests/WizardRulesTests.cs`: a `MayHitProp` table test.

## Steps
1. `SpellRules.MayHitProp` as above; the five patches call it with `from.Job` and `VfhConfig.WizardSpellsHit.Value`.
2. Rows (dev game, Wizardry loaded). They test the rules, not the aim: a new fixture `blast_by_wizard <radius> at
   <tag>` hits every character, piece and destructible within the radius of a tagged object once, with a `HitData`
   whose attacker is the most recently spawned hireling whose job is Wizard (blunt 30 + fire 20, the Plains staff's damage), through the same
   `Damage` calls a spell's area uses, so every patch applies. Casting itself is tested in phase 04.
   - VFH-WIZ-6 (everything within 2 m of N, the blast 3 m wide): `base; board_here; hirelings Wizard 2; stance last
     Passive` (so it doesn't fight on its own); `object Spawner_GreydwarfNest N 12`; `tame Boar B at N` (the existing
     `tame` fixture's tag argument names it B); `fire hearth 1 H at N` (the existing `fire` fixture's tag argument
     names it H); `hirelings GuardMelee 2 at N; stance last Passive`; `enemies Greydwarf 1 at N` (AI off); `protect
     off; player_offset N 1.5`; `blast_by_wizard 3 at N` → `enemy_health_pct < 1` (the greydwarf was hit),
     `object_health N == 1`, `object_health B == 1`, `object_health H == 1`, `hireling last health_pct == 1` (the guard,
     the last hireling spawned), `player_health >= 0.99`; `protect on; kill_enemies`; cleanup.
   - VFH-WIZ-7: `cfg_set WizardSpellsHit EnemiesAndTerrain`; the same layout without the tame, hearth and guard;
     `blast_by_wizard 3 at N` → `object_health N < 1`; `cfg_set WizardSpellsHit EnemiesOnly`; cleanup.
   - `object_health` returns 0 for a tagged object that no longer exists (destroyed by the blast).

## Build gate
- `dotnet build -c Release`; `dotnet test tests/VikingsForHire.Tests`.

## Test plan
- The `MayHitProp` table test; rows VFH-WIZ-6 and -7; regression for gatherers: `gather1 noise1 tree_patch`
  (woodcutters and miners still damage trees and rocks).

## Commit
`feat(wizard): WizardSpellsHit: wizard spells spare props by default (EnemiesOnly) or may damage them (EnemiesAndTerrain); never people, tames or buildings`

## Rollback
Revert the prop patches; blasts would then damage props whatever the setting.
