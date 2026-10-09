# Phase 03 — The board's tabs

**Depends on:** 01 (`LevelRules` caps, `Roster.KindCount` and `JobCount`), 02 (the refusal messages, and `cap_counts`
for checking) · **Enables:** 04 (screenshots and docs)

## Goal
Players can see both caps and the chosen job's limit:
- **Contracts tab:** one line, "Combat 1/2 · Workers 3/6 · Woodcutter 1/2": both caps, then the chosen job's count
  when it has a limit. It's red when the chosen job's kind is at or over its cap, or the job is at its limit. The Post
  button is disabled with the reason, matching phase 02's refusal.
- **Upgrade tab:** one line per cap, for example "Workers 2 → 4", or "Combat hirelings 1" when it doesn't change.

## Files touched
- `src/VikingsForHire/UI/ContractsTab.cs`:
  - the count line at -425 becomes the caps-and-job line, at the same place, so nothing else moves;
  - `post.interactable` already uses `roster.CanPost(...) == OpOutcome.Ok` (phase 01). What's new is the reason line
    below;
  - when the job limit or a cap is full, the red reason line (at -515, where the locked-job line goes) shows the same
    text as the phase 02 message.
- `src/VikingsForHire/UI/UpgradeTab.cs`: `Benefits` shows two lines, `$vfh_upgrade_combat_cap` and
  `$vfh_upgrade_worker_cap`, in place of `$vfh_upgrade_cap`.
- `src/VikingsForHire/Commands/DebugCommands.cs` (line 146, the `board L…` line of the data dump): `cap {HirelingCap}`
  becomes `combat {CombatCap}, workers {WorkerCap}`.
- `src/VikingsForHire/Localization/English.json`:
  - new: `vfh_contract_counts` ("Combat $1/$2 · Workers $3/$4"), `vfh_contract_job_count` (" · $1 $2/$3", appended to the
    counts line; $1 is the job's singular name),
    `vfh_upgrade_combat_cap` ("Combat hirelings") and `vfh_upgrade_worker_cap` ("Workers");
  - removed: `vfh_contract_count` and `vfh_upgrade_cap`.

## Steps
1. **ContractsTab.** Work out:
   - `combatN = roster.KindCount(true)` and `workerN = roster.KindCount(false)`;
   - the caps, from `rules.CombatCap(board.Level)` and `rules.WorkerCap(board.Level)`;
   - `jobN = roster.JobCount(job)` and `jobMax = rules.MaxPerBoard(job)`.

   Then draw one line at -425 (where the count is today, so the layout doesn't move): `$vfh_contract_counts`, plus
   `$vfh_contract_job_count` when `jobMax > 0`.
   - The line is red when the chosen job's kind is at or over its cap (`combatN >= combatCap` for a combat job,
     `workerN >= workerCap` for a worker), or when `jobMax > 0 && jobN >= jobMax`.
   - Otherwise it's dim. The other kind being full doesn't turn it red.

2. **The Post button** is enabled when `affordable && unlocked && canPost == Ok`. The reason line shows the job-locked
   text first, then the cap or limit message.
3. **UpgradeTab** shows the two cap lines, then max level and radius as before. At the top level it shows just the
   current values.
4. **Roster tab.** It isn't touched: it lists the contracts and shows no cap (checked: `RosterTab.cs` only tests
   `roster.Count == 0` for its empty message).

## Build gate
- `dotnet build -c Release` passes, with no new warnings.
- `dotnet test` passes (localisation coverage).

## Test plan
- **By eye** (Tim, `docs/next-release.md`):
  - a level 1 board's Contracts tab, with Woodcutter chosen, reads "Combat 0/1 · Workers 0/2 · Woodcutter 0/2";
  - choosing GuardMelee shows "Combat 0/1 · Workers 0/2" (guards have no per-job limit);
  - after posting a guard, choosing a guard shows the combat count in red, the Post button greyed out, and the reason;
  - the Upgrade tab at level 1 shows "Workers 2 → 4" and "Combat hirelings 1" (unchanged at level 2, so no arrow).
- **ModTestBridge:** the phase 02 rows still pass. They post through the fixture, not the UI.

## Commit
`feat(caps): the Contracts and Upgrade tabs show combat and worker caps and the job's limit`

## Rollback
Revert the commit, and the tabs go back to phase 01's interim single number. The rules and enforcement are unaffected.

## Implementation notes (as built)
- **The Upgrade tab shows both caps on one line** ("Workers: 2 → 4 · Combat hirelings: 1"), with a single key,
  `$vfh_upgrade_caps`, instead of two lines. The tab has no room for another line: with five upgrade materials, the
  requirement rows already reach the fixed Upgrade button. The line is green when either cap changes.
