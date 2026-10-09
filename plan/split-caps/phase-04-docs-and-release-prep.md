# Phase 04 — Docs, regression and 0.7.0 release prep

**Depends on:** 01–03 · **Enables:** the 0.7.0 release, on Tim's go-ahead

## Goal
The README, CHANGELOG, test checklist, futures and batch tab describe the split caps. A regression run shows nothing
else broke, the version is 0.7.0, and the package is built, ready to release when Tim says so.

## Files touched
- `README.md`:
  - the `## Board levels` table's "Hirelings" column becomes "Combat" and "Workers";
  - a short paragraph on per-job limits and on existing worlds keeping their hirelings;
  - the Jobs table notes "one per board" for the Steward as well.
- `CHANGELOG.md`: `## 0.7.0` with the caps, the limits, the "existing hirelings stay" note, the data-file keys, and
  "Update everyone together".
- `docs/test-checklist.md`: rows VFH-CAP-1..4, the updated VFH-CON-2 description, and the by-hand tab check.
- `docs/next-release.md`: a new `## 0.7.0 (unreleased)` tab, with each phase's commit, the test rows, the by-hand
  checks, and the regression results.
- `plan/vikings-for-hire/futures.md`: the "Separate caps" section is marked done in 0.7.0, with a pointer to
  `plan/split-caps/`.
- Version 0.7.0 in `src/VikingsForHire/Plugin.cs`, `src/VikingsForHire/VikingsForHire.csproj` and
  `package/manifest.json`.

## Steps
1. Write the docs above in the README's existing voice, with the table:

   | Level | Combat | Workers |
   |---|---|---|
   | 1 | 1 | 2 |
   | 2 | 1 | 4 |
   | 3 | 2 | 6 |
   | 4 | 2 | 8 |
   | 5 | 3 | 8 |
   | 6 | 3 | 8 |
   | 7 | 4 | 8 |
   | 8 | 4 | 8 |

   It keeps the upgrade costs and radius columns, and adds: "Per board: 2 woodcutters, 2 miners, 1 Steward, 1 Farmer,
   1 Cook."
2. **Regression run** through ModTestBridge: every automated row that posts contracts, plus the combat and nav sets
   used for 0.6.0.
   - Rows that post contracts: `con1 con2 con3 hire3 hire4 hire5 upk1 upk2 recruit1 post1 cook_protect orders`, and
     the cap rows.
   - Any row that fails because it relied on the old shared cap (for example posting 3 workers at level 1) is
     rewritten to the new limits, by raising its `board_level` or swapping a job. Each rewrite is listed in the tab.
   - A failure of any other kind is fixed in the phase it came from.
3. **Version bump** to 0.7.0 in the three places.
4. **Build the package:** `dotnet build -c Release -t:Package src/VikingsForHire` produces
   `dist/Spronglehump-HiredHands-0.7.0.zip`.
5. **Stop.** No push, tag or release until Tim says so.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.
- The `Package` target succeeds, with no version mismatch.
- The regression run passes, with any rewritten rows passing twice.

## Test plan
- Read the README and CHANGELOG back against the behaviour seen in phases 02–03.
- Tim's batch: the by-hand tab check from phase 03, and a quick hire on the dedicated server, where the refusal
  message shows for a client too.

## Commit
`docs: split caps (README, CHANGELOG, checklist, futures); version 0.7.0`

## Rollback
Docs and version only: revert the commit. If 0.7.0 had already been released, the fix is a 0.7.1.
