# Phase 02 — Enforcement, messages and in-game tests

**Depends on:** 01 (`Roster.CanPost`, the new `OpOutcome` values, `LevelRules` caps) · **Enables:** 03 (the tabs'
messages), 04 (docs and the regression run)

## Goal
Posting a contract obeys the split caps on the board owner's machine, and a refusal says which limit is full, with
the numbers. A test fixture can lower a cap in memory, so "already over the cap" can be tested in game. New in-game test
rows cover every case, and the existing row that tested the old cap is updated.

## Files touched
- `src/VikingsForHire/Board/BoardRosterOps.cs`: the Post case keeps phase 01's `roster.CanPost(op.Job, rules, boardLevel)`
  call, but replaces its interim mapping (every refusal to `$vfh_op_cap`) with a message for each outcome
  (below). It logs `contract.refused` with the reason and the counts.
- `src/VikingsForHire/Localization/English.json`: new keys `vfh_op_cap_combat`, `vfh_op_cap_workers` and
  `vfh_op_job_limit`. `vfh_op_cap` and `vfh_op_job_taken` are removed.
- `src/VikingsForHire/Testing/FixturesBoard.cs`: a new fixture, `caps <level> <combat> <worker> | reset`, which
  overrides one board level's caps in memory. It saves the loaded values for `reset`, as `defense_chances` does for
  the levels table.
- `src/VikingsForHire/Testing/FixturesRoster.cs`: a new check, `cap_counts`, which returns `combat/worker` counts of
  the nearest board's roster, for example `1/2`.
- `test/alias_vfh.yaml`: `VFH-CON-2` is updated, and new rows `VFH-CAP-1` to `VFH-CAP-4` are added.

## Steps
1. **Refusals.** `BoardRosterOps` Post: after the `BadLevel` and job-gate checks, call `CanPost`. It maps:
   - `CombatCapReached` → `$vfh_op_cap_combat`: "Combat hirelings: $1/$2 at this board level. Upgrade the board for
     more."
   - `WorkerCapReached` → `$vfh_op_cap_workers`: "Workers: $1/$2 at this board level. Upgrade the board for more."
   - `JobLimitReached` → `$vfh_op_job_limit`: "$1: $2/$3 per board." $1 is the job's name from `$vfh_job_<job>`,
     singular, as the job picker shows it: "Woodcutter: 2/2 per board".

   `$1`, `$2` and `$3` are filled with the job's localised name and the counts. `OpResult` already carries a
   message; the arguments go in through the existing localisation path, the way `$vfh_contract_job_locked` takes the
   level.
2. **Logging.** `VfhLog.I(LogCat.Roster, "contract.refused", ("board"), ("job"), ("why", outcome), ("combat",
   "n/cap"), ("workers", "n/cap"), ("jobCount", "n/max"))`.
3. **The `caps` fixture.** It sets `DataStore.Current.BoardLevels[level-1].CombatCap` and `.WorkerCap`, and `reset`
   puts them back. It only runs in the single-player test world, where `DataStore.Current` is this machine's own data.
4. **Test rows.** Each starts with `base` and `board_here`, and the board stocked with `stock_board 1000 0`.
   - **VFH-CON-2**, rewritten for the new caps at level 1:
     1. post Woodcutter (Pending 1), post GuardMelee (Pending 2), post Smelter (Pending 3);
     2. post Woodcutter: `last_op == WorkerCapReached`;
     3. post GuardRanged: `last_op == CombatCapReached`;
     4. `roster all == 3`.
   - **VFH-CAP-1, job limit:**
     1. `board_level 3`;
     2. post Woodcutter twice (OK);
     3. post Woodcutter: `last_op == JobLimitReached`;
     4. post Smelter (OK), post Smelter: `last_op == JobLimitReached`;
     5. `cap_counts == 0/3`.
   - **VFH-CAP-2, upgrade:**
     1. at level 1, post GuardMelee (OK), then GuardMelee: `CombatCapReached`;
     2. `board_level 3`, then post GuardMelee: OK, and `cap_counts == 2/0`.
   - **VFH-CAP-3, already over the cap:**
     1. `board_level 3`, post GuardMelee twice (OK);
     2. `caps 3 1 6` lowers the combat cap to 1;
     3. `roster all == 2` (nobody sent away), then post GuardRanged: `CombatCapReached`;
     4. post Woodcutter: OK (workers unaffected);
     5. `caps reset`.
   - **VFH-CAP-4, gates still apply:** at level 1, post Miner: `last_op == BadLevel`. The job-locked refusal already
     uses `OpOutcome.BadLevel` with the `$vfh_op_job_locked` message (`BoardRosterOps` Post), and that's unchanged.

   VFH-CAP-1 to CAP-3 start with `caps reset`, so a row that failed midway after lowering a cap can't affect the next
   one.

   Each row ends with `clear_area` and `log_errors == 0`.

## Build gate
- `dotnet build -c Release` passes, with no new warnings.
- `dotnet test` passes, including `LocalizationCoverageTests` for the new keys.

## Test plan
- In game, through ModTestBridge: `mtb restart`, then
  `mtb run "vfh_test_chain con2 cap1 cap2 cap3 cap4" "row=VFH-CAP-4 pass=" 900`. All five rows pass.
- A refused contract shows the right message on screen (seen in `mtb tail` as the board message log line).

## Commit
`feat(caps): posting obeys the combat, worker and per-job limits, with a message for each; caps test fixture and rows`

## Rollback
Revert the commit, which puts back phase 01's interim mapping to the old cap message. The test rows go with it.
