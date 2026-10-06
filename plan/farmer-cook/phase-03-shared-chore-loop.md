# Phase 03 — One chore loop for Steward, Farmer and Cook

**Depends on:** 01 (Farmer and Cook job types) · **Enables:** 06–09 (Farmer and Cook chores plug into the loop)

## Goal
The Steward's tested chore loop (survey every few seconds, most urgent job first, claims, skips after failures, status lines, Shift+E toggles, server toggles) becomes a job-neutral loop that any job runs with its own set of chores. The Steward behaves exactly as before; the Farmer and Cook get the loop with no chores yet (they stay idle). Done as a refactor before any new behaviour, so a Steward regression points here.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/` → `src/VikingsForHire/Hirelings/Work/Chores/` for the shared parts:
  - `StewardBehaviour.cs` → `Chores/ChoreLoop.cs` (class `ChoreLoop`): takes the `JobType` and its `IChore` list; logs `<job>.survey` / `<job>.job` (`steward.*` for the Steward as now, `farmer.*`, `cook.*`).
  - `StewardContext.cs` → `Chores/WorkContext.cs`: same members; the job's `JobData` from the hireling's job; plus `Func<string,int>? ExtraReserve`, an extra per-item amount `Available` leaves in the chests (phases 07–09 set it from the farm plan's protected items).
  - `StewardSteps.cs` → `Chores/WorkSteps.cs`; `IChore.cs`, `ActivityText.cs` → `Chores/`.
  - The Steward's chores stay in `Steward/` and use the moved types.
- `src/VikingsForHire/Core/Chores/ChoreKind.cs`: appends `Harvest`, `Plant`, `Stoves`, `Craft` (keys `harvest`, `plant`, `stoves`, `craft`).
- `src/VikingsForHire/Core/Chores/ChoreRules.cs`: `ChoresFor(JobType)`: Steward → the existing ten plus Board; Farmer → Harvest, Plant; Cook → Stoves, Craft. `MinLevel` for the four new kinds is 1 (crops and recipes are gated in the planners).
- `src/VikingsForHire/Config/VfhConfig.cs`: section "11 - Farmer and Cook": `FarmerHarvest`, `FarmerPlant`, `CookStoves`, `CookCraft` (bool, true, synced); `ChoreLoop.ServerAllows` maps them.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: `Steward` property → `Chores` (`ChoreLoop?`); the `Smelter` branch builds `new ChoreLoop(JobType.Smelter, StewardChores())`; `Farmer` and `Cook` branches build a `ChoreLoop` with an empty list plus `DeliverBehaviour` with the `SmelterDeliveryPolicy` renamed `ChoreDeliveryPolicy` (it already delivers what chores collect: `DeliverPending`, outputs, leftovers).
- `src/VikingsForHire/Hirelings/Work/SmelterDeliveryPolicy.cs` → `ChoreDeliveryPolicy.cs` (rename only; `StewardOutputs` → `ChoreOutputs`).
- `src/VikingsForHire/UI/HirelingPanel.cs`: the chore list shows for any job with `ChoresFor(job)` non-empty, rows from that list.
- `src/VikingsForHire/UI/RosterTab.cs`, `Testing/FixturesWork.cs`, `Commands/*`: references renamed (`steward_chore` check keeps its name).
- `docs/`: event names `steward.*` unchanged for the Steward; `farmer.*`/`cook.*` documented.

## Steps
1. Move and rename the types (git mv, so history follows), fixing namespaces to `VikingsForHire.Hirelings.Work.Chores`.
2. `ChoreLoop` replaces `JobType.Smelter` assumptions with its `_job`: the context's `JobData`, the log prefix (`_job == Smelter ? "steward" : _job.ToString().ToLowerInvariant()`), `WhyNot` using `ChoreRules.FirstUnlock(jobData, kind)`.
3. `ChoreKind` order: new kinds after `Board`, so existing `chore:` skip entries and keys don't move.
4. Server toggles for the new kinds; per-hireling toggles work unchanged (they're `chore:<key>` skip entries).
5. Nothing else changes behaviour: same constants, same urgency scoring, same failure skips.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes (ChoreRules tests extended for `ChoresFor`).
- Steward regression in single player: `vfh_test_chain chore_gate chore_toggle fires beehive animals repairs repairs2 fermenter shield tidy board_food loot pause1 pause2` all pass.

## Test plan
- The regression chain above.
- By hand: a Steward's Shift+E chore list unchanged; a Farmer's shows Harvest and Plant, a Cook's Stoves and Craft (all "on", doing nothing yet).

## Commit
`refactor(work): the Steward's chore loop becomes a shared ChoreLoop for Steward, Farmer and Cook`

## Rollback
Revert the commit (a pure move/rename plus wiring); later phases depend on it, so revert them first.

## As built
- The base's reserves (the Steward's `keepInStorage`, e.g. Wood 50) apply to every worker's `Available`, so the Cook doesn't burn the base's last building wood in the oven. `ExtraReserve` is set for the Cook (the farm's protected items); the Farmer doesn't need it, since its planner already limits planting to seeds above the reserve.
