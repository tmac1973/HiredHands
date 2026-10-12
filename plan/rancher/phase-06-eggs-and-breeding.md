# Phase 06 — Eggs, and breeding problems reported

**Depends on:** 04 (`RanchState`, the reasons in Problems), 05 (`DropCollect`) · **Enables:** 07, 08

## Goal
The Rancher collects eggs while the egg order is short and leaves them to hatch once it's met (the Steward's tidy
leaves them too), and the breeding reasons are checked in game.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Ranch/EggsChore.cs` (new) — `IChore` for `ChoreKind.Eggs`.
- `src/VikingsForHire/Hirelings/Work/Steward/TidyChore.cs` — skips the catalog's egg items when the board has an Active
  Rancher.
- `src/VikingsForHire/Hirelings/HirelingAI.cs` — the Rancher's chore loop gains `EggsChore`.
- `src/VikingsForHire/Core/Chores/ChoreKind.cs`, `ChoreRules.cs`, `ChoreLoop.ServerAllows` — `Eggs` kind (key `eggs`)
  added to `ChoresFor(Rancher)`, mapped to `RancherEggs`.
- `src/VikingsForHire/Localization/English.json` — `$vfh_ranch_eggs` label.
- `test/alias_vfh.yaml` — rows VFH-RANCH-10, -11.

## Steps
1. **Eggs chore.** While `RanchState.For(board).CollectEggs`: egg items (`ItemDrop` of a catalog egg) on the ground in
   the area, not reserved, not fed-to-an-animal (`AnimalsChore.FeedKey`): one job per egg, nearest first, urgency 0.4,
   label `$vfh_ranch_eggs`; pick up into cargo; `RanchState.Invalidate`. Stops as soon as the order is met (the plan is
   re-read each survey).
2. **Tidy.** The Steward's tidy chore leaves egg items alone on a board with an Active Rancher, whatever the egg order.
3. **Breeding reasons** already show from phase 04 (the full herd goes to the planner); this phase only verifies the
   crowded case in game.

## Verification
- Row VFH-RANCH-10 (eggs): Rancher and a Steward (tidy on), `order add ChickenEgg 2 egg`, 4 eggs on the ground in the
  area (`litter ChickenEgg 4`): eventually a chest holds 2 ChickenEgg and 2 stay on the ground (after 60 s more still 2:
  neither the Rancher nor the Steward's tidy takes them).
- Row VFH-RANCH-11 (crowded): Rancher, 5 tame boars within 3 m, `order add Boar 2 herd`:
  `why has vfh_ranch_crowded|Boar == true`.
- Existing Steward tidy row still passes on a board without a Rancher.

## Done when
- [ ] Eggs are collected up to the egg order and then left to hatch; the Steward's tidy leaves them on a ranch board.
- [ ] Problems shows the breeding reasons.
- [ ] Rows VFH-RANCH-10, -11 pass.
