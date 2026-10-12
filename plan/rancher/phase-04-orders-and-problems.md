# Phase 04 — Ranch orders on the board and the Rancher's Problems view

**Depends on:** 01 (order kinds, planner), 02 (catalog for pickers, herd scan), 03 (the job exists) · **Enables:** 05
(slaughter reads the plan), 06 (eggs read the plan)

## Goal
Players add, edit and remove ranch orders on the board's Orders tab, and the Rancher's Shift+E Problems view lists each
ranch order with have / target and why it isn't met, from a per-board ranch plan the later chores also use.

## Files touched
- `src/VikingsForHire/UI/OrdersTab.cs` — `PickFor.Ranch`; a third add button "Add ranch order"; the picker lists the
  catalog's meats (Meat orders, default 40), eggs (Egg orders, default 20) and kinds (Herd orders, default 4; label "keep
  N <kind> breeding"); rows show the ranch group after kitchen, with the same -/+/pause/remove controls (Herd `-` stops
  at 2); the needs-worker note names the Rancher.
- `src/VikingsForHire/Hirelings/Work/Ranch/RanchState.cs` (new) — per board, at most every 10 s: herd scan + stock
  (`BoardOrders.Stock`, plus the Rancher's cargo) + orders → `RanchPlanner.Plan`; `feedingAvailable` per kind = any of the kind's
  Foods (catalog) is in the area's chests. It passes the full herd scan (positions, hunger) to the planner, so every
  phase 01 reason, breeding ones included, shows from this phase on. `Invalidate(board)` after a slaughter or egg pickup.
- `src/VikingsForHire/Hirelings/Work/Chores/ChoreLoop.cs` — `WhyText`: for a Rancher, order reasons from
  `RanchState.For(board).Why`.
- `src/VikingsForHire/UI/HirelingPanel.cs` — `WhyOrders` for a Rancher: the board's Meat, Egg and Herd orders; a Herd
  row shows "adults / pool" (unnamed adults counted by the herd scan) instead of stock; the Problems button shows for the
  Rancher like the Farmer and Cook.
- `src/VikingsForHire/Localization/English.json` — ranch order labels and every `$vfh_ranch_*` reason from phase 01.
- `src/VikingsForHire/Testing/FixturesRoster.cs` — `order add <item> <n> <meat|egg|herd>`.
- `test/alias_vfh.yaml` — rows VFH-RANCH-4, -5.

## Steps
1. Order fixture and OrdersTab picker/rows for the three kinds; `BoardOrders.Submit` unchanged (it carries the kind).
2. `RanchState` computing the plan per board on the game simulating the Rancher.
3. Problems view rows and the `WhyText` branch; reason strings.

## Verification
- Row VFH-RANCH-4 (orders): `order add RawMeat 40 meat`, `order add Boar 1 herd`: `order Boar target == 2` (clamped),
  `order RawMeat kind == Meat`; remove both; `order RawMeat target == none`.
- Row VFH-RANCH-5 (Problems): a Rancher, two tame boars, `order add RawMeat 40 meat` with no herd order:
  `why has vfh_ranch_need_pool|RawMeat == true`; add `order add Boar 2 herd`: `why has vfh_ranch_at_pool == true`; and throughout, `slaughtered_by_rancher` doesn't exist yet, so the no-slaughter
  case is asserted in phase 05 (VFH-RANCH-7b).
- By hand: the Orders tab's ranch picker and rows, and the Rancher's Problems view, look right.

## Done when
- [ ] Ranch orders can be added, edited and removed on the Orders tab, Herd orders never below 2.
- [ ] The Rancher's Problems view lists each ranch order with its reason.
- [ ] Rows VFH-RANCH-4, -5 pass.
