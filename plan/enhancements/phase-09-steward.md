# Phase 09 — Steward behaviours

**Depends on:** 04 (effects framework, shared boosts: Brisk, Pack mule, Strong back) · **Enables:** 12

## Goal
The Steward's chore enhancements: Keeper of flames, Handyman, Bonesetter, Quartermaster, Tidy mind, Smelter's hand,
Watchman, Errand runner, Fermenter's touch, Master steward and Shield keeper.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/FiresChore.cs` — Keeper of flames: refill threshold
  `KeeperOfFlames.refill` (0.75) instead of `StewardFireRefillFraction` for this Steward (fires and kilns' fuel).
- `src/VikingsForHire/Hirelings/Work/Steward/RepairsChore.cs` — Handyman: repair reach `Handyman.range` (10 m, from 5 m)
  and quiet time `Handyman.quiet` (10 s, from `StewardRepairQuietSeconds`).
- `src/VikingsForHire/Hirelings/Work/Steward/Bonesetter.cs` — new: hurt hirelings within `Bonesetter.range` (10 m) of
  this Steward, not in a fight, regain `Bonesetter.rate` (0.02) of max health a second; a heal effect every few seconds;
  logs `enh.bonesetter`.
- `src/VikingsForHire/Hirelings/Work/Steward/BoardChore.cs` — Quartermaster: fill days + `Quartermaster.days` (3).
- `src/VikingsForHire/Hirelings/Work/Steward/TidyChore.cs` — Tidy mind: min age `TidyMind.seconds` (20 s) and range × 2.
- `src/VikingsForHire/Hirelings/Work/Steward/StationsChore.cs` (and mills, kilns) — Smelter's hand: twice the items per
  load trip (cargo allowing).
- `src/VikingsForHire/Core/StanceRules.cs` and `src/VikingsForHire/Hirelings/DamagePatches.cs` — Watchman: a Steward with
  it may take the Defensive stance (the panel offers it) and deals × (1 + `Watchman.damage`, 0.25).
- `src/VikingsForHire/Hirelings/Work/Steward/FermentersChore.cs` — Fermenter's touch: when it loads a fermenter, the
  fermenter's start time is moved back by `FermentersTouch.share` (0.20) of its duration, so it's ready sooner.
- `src/VikingsForHire/Hirelings/Work/Chores/ChoreLoop.cs` — Master steward: rests between chores × 0.85, station loads
  take 15% less time, and loads carry 15% more per trip (`MasterSteward.speed` 0.15); Errand runner: after finishing a
  chore, the next chore whose target is within `ErrandRunner.range` (10 m) of where it stands starts at once with no rest, up
  to `ErrandRunner.chain` (4) in a row.
- `src/VikingsForHire/Hirelings/Work/Steward/ShieldsChore.cs` — Shield keeper: reach for shield generators × 2.
- `src/VikingsForHire/Testing/FixturesWork.cs` — `shieldgen` gains an optional distance (from the board, default 7 m).
- `src/VikingsForHire/UI/HirelingPanel.cs` — the stance buttons come from `StanceRules.Allowed(job, hasWatchman)`.
- `test/alias_vfh.yaml` — rows VFH-ENH-S1 … S11.

## Steps
1. Implement each as listed, read through `Enh` so other Stewards keep the server's settings.
2. The StanceRules change: `Allowed(job)` takes an optional "has Watchman" flag; the hireling panel passes it.
3. Rows (a level 8 Steward, one pick each, fast timers): S1 keeper of flames (`fire piece_groundtorch_wood 0.7 T`: fuelled within 60 s;
   for a plain Steward, still at 0.7); S2 handyman (`damaged woodwall 0.5 W far`, with a low step in front
   (`piece wood_floor`): repaired; a plain Steward reports "can't get to"); S3 bonesetter (a hurt guard idle at the
   base heals ~2%/s near it); S4 quartermaster (board stocked to 10 days, not 7); S5 tidy mind (`litter Wood 5` then wait 30 s: tidied, while a plain Steward
   leaves it until 60 s); S6 smelter's hand (a smelter gets 2× ore in one trip: `station smelter ore` after the first load); S7 fermenter's
   touch (`fermenter_status F == Ready` in ≤ 0.85 × the time of a plain Steward's fermenter in the same row). Watchman, master steward and shield keeper:
   S8 watchman (Defensive Steward engages a greydwarf at 10 m), S9 master steward (the S11 three-torch setup is done in ≤ 0.87 ×
   the time a plain level 8 Steward takes in the same row), S10 shield keeper (`shieldgen 0 S 18` with the Steward posted at the
   board: fuelled; a plain Steward reports it out of reach); S11 errand runner (three `fire piece_groundtorch_wood 0.4` in the ring:
   all three fuelled with no rest between them, `enh_events ErrandRunner >= 2`).

## Build gate
- Compile check; unit tests (StanceRules).
- In game: rows S1-S11 pass; regression `chore_gate chore_toggle mills fires chore_urgency beehive sap animals repairs
  repairs2 fermenter shield tidy board_food board_rich limit1 limit2 kilns` passes.

## Test plan
Rows above; the Steward regression.

## Commit
`feat(enhancements): steward behaviours (keeper of flames, handyman, bonesetter, quartermaster, tidy mind, smelter's hand, watchman, errand runner, fermenter's touch, master steward, shield keeper); rows VFH-ENH-S1..S11`

## Rollback
Each change reads `Enh.Has`; without picks the Steward uses the server's settings exactly as before.
