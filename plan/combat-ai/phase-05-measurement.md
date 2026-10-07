# Phase 05 — Measuring it: balance log, before/after fights

**Depends on:** 01 (counters, `set_cfg`, `enemy_level`), 02 (blocks and parries), 03 (projectile reads and blocks), 04
(dodges) · **Enables:** 06 (release notes quote the numbers; tuning, if any, happens here first)

## Goal
Show whether block and dodge do what the overview promised, with numbers:
- **Each fight record** in the balance log carries its block, parry and dodge counts, and whether `BlockAndDodge` was
  on.
- **A repeatable set of four test fights** runs several times with the setting off, then on. The same fights double
  as the "before".
- **The report** compares, for each matchup:
  - damage taken, as a share of max health;
  - kill and death rates and fight length;
  - the read, parry and dodge rates against the chances in the levels table.

The same report reads the live server's log after release.

## Files touched
- `src/VikingsForHire/Telemetry/BalanceFights.cs`:
  - `Fight` snapshots the hireling's `DefenseStats` at `Start`;
  - `End` records the differences: `reads`, `misses`, `blocks`, `parries`, `parryRolls`, `parryWins`, `projReads`,
    `projBlocks`, `dodges`, `dodgedHits`, `dodgeRolls` and `dodgeWins`;
  - plus `defense` (`on`/`off`, the setting's value).
- `src/VikingsForHire/Hirelings/Combat/DefenseStats.cs`: `Snapshot()` and `Minus(DefenseStats)`.
- `tests/data/balance-defense-sample.jsonl` (new): two hand-written fight records, one off and one on, for the report's
  build gate.
- `scripts/balance-report.py`:
  - a "Defense" section for each (job, level, enemy and stars, defense on/off) row: n, damage taken per fight as % of
    max health, kill %, died %, fight length, read rate, parry rate (of reads, guards only), dodges per fight, and
    dodged hits as a share of dodges;
  - with `--compare-defense`, an off-against-on table per matchup, showing differences and the expected rates taken
    from the defaults table.
- `src/VikingsForHire/Testing/TestHarness.cs`: `vfh_wait_until`, which waits without asserting.
- `src/VikingsForHire/Testing/FixturesCombat.cs`: a `fight_over` check.
- `test/alias_vfh.yaml`:
  - matchup rows `VFH-DEF-M1` to `VFH-DEF-M4`;
  - a chain `defense_ab` that runs each matchup 5 times with the setting off, then 5 times on.

## Steps
1. **Fight records.** In `BalanceFights.Start`, store `h.AI.Defense.Snapshot()` on the `Fight`. In `End`, take
   `Defense.Minus(snapshot)` and append the twelve counters and `defense` to the `fight` record. Fields are added at the
   end, so older readers of the JSONL are unaffected.
2. **The matchups,** one per kind of fighter named in the overview. Each:
   - starts with `set_cfg BalanceLog true`, `kill_enemies`, and a fresh flat spot (`clear_area 30` and `flatten 20`);
   - ends with `kill_hirelings`, `kill_enemies` and `log_errors == 0`. A hireling dying is a valid outcome, so it isn't
     asserted against.

   | Row | Fighter | Enemy | Ends when |
   |---|---|---|---|
   | VFH-DEF-M1 | level 1 GuardMelee, Defensive | 3 Greydwarfs at 8 m | enemies dead, or 150 s |
   | VFH-DEF-M2 | level 3 GuardMelee, Defensive | 1 Troll at 12 m | troll dead, or the guard dead, or 180 s |
   | VFH-DEF-M3 | level 4 GuardRanged, Defensive | 2 Draugr_Ranged at 18 m | enemies dead, or 180 s |
   | VFH-DEF-M4 | level 5 Woodcutter, Defend | 1 Troll at 8 m | troll dead, or the worker dead, or 180 s |

   - **"Ends when"** uses a new harness command, `vfh_wait_until <seconds> <check> [args…] <op> <value>`. It is added
     in `src/VikingsForHire/Testing/TestHarness.cs` next to `vfh_assert_eventually`, using the same polling, but it
     neither counts as a check nor fails the row on timeout. It logs `test.wait` with `met=true/false` and the waited
     time.
     - In the rows: `vfh_wait_until 150 enemies_alive 40 == 0` (M1 and M3), and `vfh_wait_until 180 fight_over == true`
       (M2 and M4).
     - `fight_over` is a new check in `FixturesCombat.cs`: true when no enemies are alive within 40 m, or no hireling is
       alive.
3. **The `defense_ab` chain:**
   1. `set_cfg BlockAndDodge false`, then M1–M4 five times each;
   2. `set_cfg BlockAndDodge true`, then M1–M4 five times each;
   3. `set_cfg BlockAndDodge true` again, to leave it on;
   4. a marker row, `VFH-DEF-DONE` (`vfh_test_begin VFH-DEF-DONE;vfh_test_end`), whose result line means the chain is
      finished.

   That's about 40 fights: typically 45–60 minutes, at most about 2¼ hours if every fight runs to its time limit. It's
   run in the background through
   `mtb run "vfh_test_chain defense_ab" "evt=test.result row=VFH-DEF-DONE" 9000`.
4. **The report.** `scripts/balance-report.py <balance folder> --compare-defense` prints the off and on columns for each
   matchup. In single player the log is under the profile's `BepInEx/HiredHands/balance`. It shows:
   - damage taken as % of max health (mean and median);
   - kill % and died %;
   - mean fight length;
   - read rate, `reads / (reads + misses)`, against the table's `readChance`;
   - parry roll rate, `parryWins / parryRolls`, against `parryChance` (M1–M2), and parries actually landed per fight;
   - dodge roll rate, `dodgeWins / dodgeRolls`, against `dodgeChance`, plus dodges and dodged hits per fight.
5. **Acceptance.** These are the overview's success criteria, applied to the report:
   - **Damage taken** (mean % of max health per fight) is lower with the setting on in M1, M2 and M4. In M3 it's at
     most 2 points higher than with it off, since archers can't block and only projectile dodges help them.
   - **Wins:** kill % is not lower by more than 10 points in any matchup, and fight length is not over 25% longer.
   - **Rates:** each roll rate (read, parry roll, dodge roll) is within 12 points of its table value.
     - A rate is judged only for a matchup with at least 30 rolls summed over its 5 runs. Below that, the report prints
       `n/a (n=…)` and it isn't judged.
     - Each rate has to be judged somewhere: read in every matchup, parry in M1 or M2, dodge in M2 or M4.
     - If no matchup reaches 30 rolls for a rate, rerun that rate's matchup 10 times instead of 5.
     - If it still falls short, the rate is reported as unjudged, with its count, in `docs/next-release.md`. It is then
       checked by eye in Tim's batch, rather than holding up the release. With 5 fights of many
     attacks each there are enough samples. A bigger gap is a bug, to be fixed in the phase it came from before going
     on.
   - **Off really is off:** the off runs show `reads == 0`, `parryRolls == 0` and `dodges == 0` in every record.
6. **When acceptance fails:**
   - **Damage isn't lower** (M1, M2 or M4): go through that matchup's Debug log for attacks that were read but neither
     blocked nor dodged. The `defense.block_skipped` lines (`why=mid_swing` or `why=turn`, from phases 02–03),
     `defense.dodge_blocked_by_ground` and `defense.no_dodge` (phase 04) point to the phase whose step is at fault. Fix it there, then rerun `defense_ab`.
   - **Wins drop** (kill % down by more than 10 points, or fights over 25% longer): shorten the swing hold-off in
     phase 02 step 6 from 0.6 s to 0.3 s, and rerun. If wins are still down, report it to Tim with the table before
     going further.
7. **Tuning, when acceptance passes but hirelings come out far tougher.** "Far tougher" means damage taken more than
   halved in M1 or M2, or deaths in M2 and M4 falling to zero from more than half.
   - Scale `parryChance` and `dodgeChance` by 0.75 at every level in `DefaultData`, leaving read chance and cooldowns
     alone, and rerun `defense_ab` once.
   - Take both reports to Tim, who decides whether to keep the scaled numbers. This is the overview's "measure first,
     adjust after", with the levels table as the only knob.
   - The same "measure first, adjust after" rule applies to the live server once 0.6.0 is out. That's the
     `fetch-balance.sh` and `--compare-defense` path, using records from before and after the update.
8. **Batch log.** Add this phase's commit row, and the steps for checking the measurement (the defense_ab run and the report table), to the 0.6.0 tab of
   `docs/next-release.md`, as the batch-testing routine requires.

## Build gate
- `dotnet build -c Release` builds and deploys.
- `dotnet test` passes.
- `python3 scripts/balance-report.py --help` runs, and `--compare-defense` on a sample JSONL (two hand-written records,
  one off and one on, in `tests/data/balance-defense-sample.jsonl`) prints both columns.

## Test plan
- Run `defense_ab` in single player through `mtb`. Each row reports a result (it never hangs).
- Run the report and check the acceptance points above. Paste the table into `docs/next-release.md` under the 0.6.0
  combat entry.
- **Regression:** every combat, stance, retreat, tame, post, follow and pass row passes once more, with the setting on.

## Commit
`feat(telemetry): block/parry/dodge counts in fight records; defense A/B matchups and --compare-defense report`

## Rollback
Revert the commit. Fight records go back to the 0.5.0 fields. The report script ignores missing fields, so older or
newer logs read either way. The matchup rows are test-only.
