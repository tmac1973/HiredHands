# Phase 02 — Picks on the contract, choose and respec ops, the hireling's copy

**Depends on:** 01 (EnhancementPicks, EnhancementRules, catalog) · **Enables:** 03 (the panel sends these ops), 04
(effects read the hireling's copy)

## Goal
A hireling's picks live on its contract, travel wherever the contract goes (deaths, respawns, charters, joining another
board), and are copied onto the hireling's own record for the game simulating it. Two new roster ops let a player choose
an enhancement and respec, applied on the board owner's game like a promotion. A server switch turns the whole system off.

## Files touched
- `src/VikingsForHire/Core/Roster.cs` — `ContractEntry.Enhancements` (string, default `EnhancementPicks.Empty`); roster
  `FormatVersion` 3 → 4: written after `NoHomeWork`, read under `version >= 4`; `Roster.ChooseEnhancement(hid, level,
  choice)` and `Roster.ResetEnhancements(hid)` returning `OpOutcome` (NotFound, WrongState, BadLevel when
  `!EnhancementRules.CanChoose`).
- `src/VikingsForHire/Core/RosterOps.cs` — `RosterOpType.ChooseEnhancement = 14`, `RespecEnhancements = 15`;
  `RosterOp.Choice` (char as a byte, `A`/`B`) and `RosterOp.Enhancements` (string, for Adopt) appended to the end of
  `Write`/`ReadFields`; `HirelingOp.Enhancements` (string) on flag bit `524288`, written, read and in `ToString`.
- `src/VikingsForHire/Core/OpOutcome` (in `RosterOps.cs`) — `EnhancementsOff` outcome.
- `src/VikingsForHire/Board/BoardRosterOps.cs` — cases:
  - `ChooseEnhancement`: refused with `EnhancementsOff` when `VfhConfig.Enhancements` is off; else `roster.ChooseEnhancement`;
    on Ok, `SubmitHireling(hid, new HirelingOp { Enhancements = e.Enhancements })`; log `contract.enhancement`
    (board, hid, level, choice, id).
  - `RespecEnhancements`: refused when off; with no picks made it returns Ok with no charge and no change; otherwise pays `EnhancementRules.RespecCost` with
    `BoardLedger.TryPay` (refused with `InsufficientFunds` like a promotion); clears picks; pushes them; logs
    `contract.respec` (paid).
  - `Adopt`: copies `op.Enhancements` into the new entry.
- `src/VikingsForHire/Board/BoardContracts.cs` — `ChooseEnhancement(board, hid, level, choice, cb)` and
  `Respec(board, hid, cb)` building the ops (as `Promote` does).
- `src/VikingsForHire/Followers/` (the transfer that builds the Adopt op) — puts the contract's `Enhancements` in the op.
- `src/VikingsForHire/Board/ArrivalSpawner.cs` — copies `entry.Enhancements` into the snapshot so a returning or
  respawned hireling has them.
- `src/VikingsForHire/Hirelings/HirelingZdo.cs` — key `Enhancements = "vfh_enh"`; `HirelingSnapshot.cs` key list gains it.
- `src/VikingsForHire/Hirelings/HirelingOps.cs` — applies `HirelingOp.Enhancements` with `zdo.Set`.
- `src/VikingsForHire/Config/VfhConfig.cs` — `Enhancements` (Synced, "3 - Hiring", default true): "Hirelings pick an
  enhancement at each level from 2 to 8. Off: no choices and no effects; picks already made are kept for when it's back
  on."
- `src/VikingsForHire/Localization/English.json` — `vfh_op_enhancements_off`, `vfh_op_enhancement_chosen`,
  `vfh_op_respec_done`.
- `src/VikingsForHire/Testing/FixturesRoster.cs` — fixtures `enhance <level> <A|B>` (through `BoardContracts`, on the
  contract from your last post) and `respec`; check `roster_entry <last|hid> enh`.
- `tests/VikingsForHire.Tests/RosterTests.cs` — round-trips.
- `test/alias_vfh.yaml` — rows VFH-ENH-1 (choose), VFH-ENH-2 (respec), VFH-ENH-3 (persistence).

## Steps
1. Add the contract field, the format bump and the two roster methods; `Promote` leaves picks alone (the new levels are
   simply owed).
2. Add the op types and fields, appended at the end so the field order of existing fields is unchanged (everyone
   updates together for 0.10.0, but older saved op bytes never exist on disk).
3. Board-side cases as listed.
4. Copy to the hireling: on choose and respec (SubmitHireling), at arrival (ArrivalSpawner), and on Adopt (the new
   board's entry, then its arrival or SubmitHireling as Adopt already does for level).
5. Config switch, strings, fixtures and checks.
6. Unit tests: roster v4 round-trip with picks; v3 rosters load with empty picks; op round-trips with Choice and
   Enhancements; `ChooseEnhancement` refuses levels above the contract level, level 1, and a chosen level;
   `ResetEnhancements` clears.
7. Rows:
   - VFH-ENH-1: post an Archer level 5 (free), `enhance 2 A`, `enhance 5 B`: `roster_entry last enh == A--B---`,
     `hireling posted vfh_enh == A--B---`; `enhance 6 A` refused (level 5); `enhance 2 B` refused (already chosen).
   - VFH-ENH-2: as above, `stock_board 400 400`, `respec`: picks empty, and `board_storage coins` and `board_storage food`
     each down by half the level 5 hire fee's coins and food; then `enhance 2 A`, `board_clear`, `respec`: refused (`roster_entry last enh == A------` still).
   - VFH-ENH-3: a level 4 Guard with picks dies (`kill_hirelings graves`) and returns (`skip_days`, ReturnAfterDays):
     `hireling posted vfh_enh` unchanged; then `charter_pack` / `charter_apply`: unchanged on the new board; then, recruited
     with the stone (`stone 2`, `recruit_posted`) and moved to a second board (`board_far 120 2`, `transfer_posted`):
     `roster_entry` on the second board shows the same picks and so does the hireling.

## Build gate
- Compile check as phase 01; `dotnet test tests/VikingsForHire.Tests`.
- In game: rows VFH-ENH-1, -2, -3 pass; regression `vfh_test_chain con1 con2 pro1 dis1 charter1 death1 death2 death5
  home1 portal1 move1` passes.

## Test plan
Unit tests and the three rows above; the regression rows cover contracts, promotion, death, charters and transfers.

## Commit
`feat(enhancements): picks on the contract (roster v4), choose and respec ops on the board, the hireling's copy, Enhancements switch; rows VFH-ENH-1..3`

## Rollback
Reverting removes the ops and field. Rosters saved as v4 don't load in a build that only knows v3 (it throws on a newer
version): rolling back after boards were saved needs this phase's read code kept, or the boards' rosters re-saved by a
v4-aware build that writes v3. Treat 0.10.0 as one-way once deployed; roll back only before release.
