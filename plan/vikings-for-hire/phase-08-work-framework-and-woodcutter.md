# Phase 08 — Work framework, cargo delivery & the Woodcutter job

**Depends on:** 02, 05, 06, 07 · **Enables:** 09 (Miner), 10 (Smelter), 13 (field gathering and deposit on return)

## Goal
Build the shared machinery for gathering jobs: target selection inside the work radius, harvesting with real tool swings, pickup into cargo, and **per-item-type delivery**. Each item type goes to a chest within the radius that already holds that item and has room. Overflow goes to a drop pile next to the board. Then ship the Woodcutter on top of it. Trees near buildings are left alone so falling trunks never wreck the base, and logs and stumps are processed fully.

## Files touched
- `src/VikingsForHire/Hirelings/Work/GatherBehaviour.cs`: priority 200. A generic state machine `FindTarget → MoveTo → Harvest → Collect → (Deliver when cargo full or nothing left) → repeat`, parameterized by an `IGatherProfile`.
- `src/VikingsForHire/Hirelings/Work/IGatherProfile.cs`: `IEnumerable<Component> Candidates(center, radius)`, `bool IsValid(Component)`, `float StandOffDistance(Component)`, `HashSet<string> PickupItems`.
- `src/VikingsForHire/Hirelings/Work/WoodcutterProfile.cs`: candidates are `TreeBase` (standing trees), `TreeLog` (fallen logs) and `Destructible` stumps (`m_destructibleType == Tree`).
- `src/VikingsForHire/Hirelings/Work/HarvestPatches.cs`: scales `HitData` from hireling attackers on `TreeBase.Damage`, `TreeLog.Damage`, `Destructible.Damage`, `MineRock.Damage`, `MineRock5.Damage`: `m_chop`/`m_pickaxe` × `gatherMult`. `m_toolTier` comes from the equipped tool's real `m_toolTier` (the vanilla rule).
- `src/VikingsForHire/Hirelings/Work/CargoDelivery.cs`: `DeliverBehaviour` (priority 300 while `NeedsDelivery`). It's registered for **every** job, including guards and the Smelter. Uses `ChestFinder` + `Core.DepositPlanner` and walks to each planned chest in turn.
- `src/VikingsForHire/Hirelings/Work/ChestFinder.cs`: finds candidate chests in the radius. Excludes the board, other hirelings' cargo, `Vagon`, `Ship`, tombstones, private chests (`Container.m_privacy == Private`) and containers in use (`IsInUse`). Reads their inventories.
- `src/VikingsForHire/Hirelings/Work/ContainerAccess.cs`: the PullMats claim-ownership pattern: `ClaimOwnership` on the chest's ZNetView when it's not in use, then `Inventory.AddItem` + `Container.Save()`. It's wrapped so a chest that becomes in-use mid-trip is skipped and planning runs again.
- `src/VikingsForHire/Hirelings/Work/DropPile.cs` (created in phase 06): reused for delivery overflow.
- `src/VikingsForHire/Hirelings/Hireling.cs`: `HirelingAI` picks the profile by job and mode (`Working`).
- `tests/VikingsForHire.Tests/DepositPlannerTests.cs`: extended for the multi-chest, partial-stack and full-chest cases.

- `src/VikingsForHire/Testing/FixturesWork.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Target selection** (every scan interval, only when no target): candidates within the work radius of the **board**, sorted by distance to the hireling. Filters:
   - The target needs a tool tier ≤ the hireling's tool tier (`TreeBase.m_minToolTier`, `Destructible.m_minToolTier`).
   - Standing trees: skip if any player-built `Piece` lies within `TreeSafetyDistanceFromPieces` (default 6 m) of the trunk, or within the tree's height (taken from the `TreeBase` collider bounds) in any direction. Logs and stumps are exempt from the height rule but still respect 2 m from pieces.
   - It's reserved so two hirelings don't pick the same target. A static reservation map (`ZDOID → hid`, expiring after 60 s) is checked across all local hirelings. On a dedicated server, different owners could still double up, which is harmless: they just both chop.
   - It must be reachable (`Pathfinding.HavePath`). Unreachable targets are blacklisted for 5 minutes.
2. **Harvest:** move to `StandOffDistance` (1.6 m for trees), `LookAt` the trunk's hit point, and `StartAttack` with the axe on cooldown. Vanilla hit detection applies the `HitData`, scaled by `HarvestPatches`. Woodcutters always process standing tree → log → half logs → stump, because those spawn as new `TreeLog`/`Destructible` objects nearby that become the next targets (preferred within 8 m of the last target).
3. **Collect:** after each target is destroyed, and every 5 s while harvesting, gather `ItemDrop`s within 8 m whose prefab is in `PickupItems`. Walk to each one and add it to cargo through the cargo `Container` inventory. Respect the level cargo slot limit (`CargoGate`), then destroy the `ItemDrop` (claim ownership first, the vanilla `ItemDrop.Pickup` pattern via `RequestOwn`). Items not in the pickup list are ignored.
4. **Deliver trigger** (`NeedsDelivery`, mode `Working` only, cargo not empty): cargo has no free usable slot and the top stack is full; or no valid targets remain in the radius; or the hireling has carried items for 10 minutes without delivering; or the ZDO flag `vfh_deliver_pending` is true. Later phases set that flag when a follower is dismissed or returns home, and it's cleared when the cargo is empty or everything has gone to the drop pile. Delivery always runs before going idle. `NeedsDelivery` is a virtual method on an `IDeliveryPolicy` that every job registers (gather profiles implement it, guards use a default policy, and the Smelter gets `SmelterDeliveryPolicy` in phase 10). Woodcutter and Miner use the triggers above. Guards (and any job without a gather profile) use only `vfh_deliver_pending`. The Smelter overrides it in phase 10.
5. **Delivery planning:** `DepositPlanner.Plan(cargo, chests)`. For each prefab in the cargo, it picks only chests that already contain that prefab, nearest to the hireling first, filling existing stacks and then empty slots in that chest. Whatever's left goes to the drop pile. The hireling walks chest by chest in nearest-neighbour order and runs `ContainerAccess` at each (within 2.5 m). If a chest changed since planning (another player or AzuAutoStore added or removed items), it re-plans for the remaining cargo at that moment. Finally it walks to the board and drops the overflow at the drop pile.
6. **AzuAutoStore interplay:** AzuAutoStore may pull drop-pile items into chests. That's fine and expected. Gatherers never pick up items from the drop pile: `Collect` skips `ItemDrop`s tagged `vfh_pile` by `DropPile` (the tag persists with the ItemDrop ZDO through vanilla item custom data).
7. **Status tokens:** `$vfh_status_chopping`, `$vfh_status_delivering`, `$vfh_status_no_targets` (nothing harvestable in the radius; idle at the board, re-scanning every 30 s), `$vfh_status_dropping_at_board` (overflow).
8. **Combat interplay:** `CombatBehaviour`/`FleeBehaviour` (priority 900/950) preempt. `GatherBehaviour` keeps its state and target and resumes afterwards if the target is still valid.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesWork.cs`. Fixtures: `trees <prefab> <n> <radius>`, `tree_near_wall` (a beech 4 m from a fixture wall, tagged `near_wall`), `chest <tag> [item count]…` (spawns a tagged chest near the board with contents), `fill_chest <tag>`. Checks: `chest <tag> <item>`, `chest <tag> free_slots`, `drop_pile <item>`, `object_alive <tag>` (the near-wall tree must still exist), `reservations_unique`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_work1` (tree safety, assert after `skip_days 1`), `vfh_t_work2` (dedicated, open-chest re-plan split a/b), plus single-player `vfh_t_deliver1` (per-item-type routing: wood to A, seeds to B, C stays empty). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Meadows base, L1 woodcutter (stone axe), radius 30:
  - It fells beech trees, processes logs and stumps, and collects Wood and BeechSeeds. Oak (tier too high) is skipped.
  - A tree 4 m from a wall is never cut (watch for 1 in-game day; rows **VFH-WORK-1**).
  - Chest A holds Wood, chest B holds BeechSeeds, chest C is empty. Wood goes to A, seeds to B, nothing to C. Fill A completely: the Wood overflow goes to the drop pile at the board and seeds still go to B.
  - Open chest A while the woodcutter heads there: it skips A and re-plans to the pile.
  - Two woodcutters don't chop the same tree at the same time.
- Promote to L3 (bronze axe): it now cuts oaks and the chopping is visibly faster.
- AzuAutoStore enabled: drop-pile items get auto-stored by Azu. The woodcutter doesn't re-collect from the pile and doesn't loop (row **VFH-AZU-2**).
- Dedicated server with two clients: a chest client B has open is skipped and the cargo re-planned to other matching chests or the drop pile, with no item loss or duplication (count the totals before and after) (row **VFH-WORK-2**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(work): gathering framework with per-item-type chest delivery and the Woodcutter job`

## Rollback
Revert the commit. Woodcutter hirelings fall back to idle + combat. Items already delivered stay put.
