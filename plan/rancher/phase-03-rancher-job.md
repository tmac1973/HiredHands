# Phase 03 — The Rancher job, feeding, and the Steward stepping aside

**Depends on:** 02 (herd scan, to count what he feeds) · **Enables:** 04 (Problems view for a hireable Rancher), 05, 06

## Goal
A hireable Rancher (board level 2, 0.9x, one per board, butcher knife) that feeds the herd, using the Steward's feeding
chore, while a Steward on the same board steps aside from feeding. No slaughter yet.

## Files touched
- `src/VikingsForHire/Core/JobType.cs` — `Rancher` (worker: not `IsCombat`, not `IsGuard`).
- `src/VikingsForHire/Core/Data/DefaultData.cs` — job entry: `MaxPerBoard` 1, `CostMult` 0.9, `WorkerCombatFactor` 0.3,
  `MinBoardLevel` 2, `Gear` = `KnifeButcher` at every level, `ChoreLevels` ranch_feed/slaughter/eggs = 1.
- `src/VikingsForHire/Core/Data/VfhData.cs`, `DataDefaults` (FillMissing adds the job to older files),
  `DataValidator.cs` (accepts the job).
- `src/VikingsForHire/Core/Chores/ChoreKind.cs` — `RanchFeed` now (key `ranch_feed`; `ChoresFor(Rancher)` = it); phases
  05 and 06 add `Slaughter` (`slaughter`) and `Eggs` (`eggs`) with their chores, so the panel only ever lists chores
  that exist.
- `src/VikingsForHire/Config/VfhConfig.cs` — section `12 - Rancher`: `RancherFeed`, `RancherSlaughter`, `RancherEggs`
  (synced, default true); `ChoreLoop.ServerAllows` maps `RancherFeed` now and the other two in 05/06.
- `src/VikingsForHire/Hirelings/Work/Steward/AnimalsChore.cs` — the same chore class serves `RanchFeed` (constructor
  takes the `ChoreKind`), labels `$vfh_ranch_feed`.
- `src/VikingsForHire/Compat/StewardCompat.cs` — `RanchFeed` is covered by PetPantry like `Animals`.
- `src/VikingsForHire/Hirelings/Work/Chores/ChoreLoop.cs` — `WhyNot`: the Steward's `Animals` returns `"rancher"` when the
  hireling's board has an active Rancher contract (roster); idle text `$vfh_rancher_idle`.
- `src/VikingsForHire/Hirelings/HirelingAI.cs` — `case JobType.Rancher`: `ChoreLoop(JobType.Rancher, { AnimalsChore(RanchFeed) })`
  plus `DeliverBehaviour(ChoreDeliveryPolicy)`.
- `src/VikingsForHire/UI/HirelingPanel.cs` — a chore handled by a Rancher shows `$vfh_chore_state_rancher`.
- `src/VikingsForHire/Hirelings/HirelingChatter.cs` + `Localization/English.json` — job name, chore names, feed label,
  idle text, `rancher` chatter pool (8 lines), `vfh_chore_state_rancher`.
- `tests/VikingsForHire.Tests/` — DataDefaultsTests/LevelRulesTests/ChoreRulesTests cases for the job.
- `src/VikingsForHire/Testing/FixturesHireling.cs` — check `chore_why <job> <chore key>`: why the nearest hireling of
  that job skips that chore (`ChoreLoop.WhyNot`), or `none`.
- `test/alias_vfh.yaml` — rows VFH-RANCH-2, -3.

## Steps
1. Add the job and its data entry; older data files get it from `FillMissing`; the validator accepts it.
2. The `RanchFeed` kind, key, `ChoresFor`, the three config toggles, `ServerAllows` for `RancherFeed`.
3. Generalise `AnimalsChore` to take its `ChoreKind` (Steward: `Animals`, Rancher: `RanchFeed`); behaviour unchanged.
4. Steward steps aside: in `ChoreLoop.WhyNot`, `ChoreKind.Animals` returns `"rancher"` when the board's roster has an
   Active Rancher; the Steward's panel shows it as handled by the Rancher. PetPantry covers both feeding kinds.
5. Wire the job in `HirelingAI`; the knife comes from the gear list (vanilla item, no custom model).
6. Strings and chatter.

## Verification
- `dotnet test` passes (job data, chore rules).
- Row VFH-RANCH-2: base, board level 2, `post Rancher 1 30 free`: roster Active == 1, `hireling last gear.right ==
  KnifeButcher`.
- Row VFH-RANCH-3: chest with Raspberry, a hungry tame Boar, a Steward and a Rancher on the board:
  `fed_by_steward B == 0`, the boar's hunger clears (`animal_hungry B == false`), and `chore_why Smelter animals ==
  rancher`.
- Row VFH-RANCH-3b (PetPantry): with `cfg_set StewardIgnoreOtherMods false` and PetPantry in the dev profile, as
  VFH-CHORE-7 does: `chore_why Rancher ranch_feed == PetPantry` (the mod's name as `StewardCompat` reports it).
- Existing row VFH-CHORE-7 (Steward animals without a Rancher) still passes.

## Done when
- [ ] A Rancher can be hired at board level 2 and carries the butcher knife.
- [ ] He feeds the herd; a Steward on the same board doesn't; PetPantry makes both step aside.
- [ ] Rows VFH-RANCH-2, -3, -3b and VFH-CHORE-7 pass.
