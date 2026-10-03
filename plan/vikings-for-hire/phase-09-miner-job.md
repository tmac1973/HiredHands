# Phase 09 — Miner job

**Depends on:** 02, 05, 06, 07, 08 · **Enables:** 11 (v1 release), 13 (miners mining in the field)

## Goal
Add the Miner on top of the phase 08 gathering framework. Miners break rocks, ore deposits and boulders inside the work radius, collect stone and ore, and deliver per item type. Their swings must **never** change terrain (no holes dug in the base), and they only mine what their pickaxe tier allows.

## Files touched
- `src/VikingsForHire/Hirelings/Work/MinerProfile.cs`: candidates are `MineRock5` (copper/silver/large rocks), `MineRock` (tin/obsidian/small deposits), and `Destructible` with `m_destructibleType == Default` whose `DropOnDestroyed` table contains at least one item in the miner's pickup list (small rocks, ore boulders, iron scrap piles in reach, the Ashlands' Grausten/flametal deposits).
- `src/VikingsForHire/Hirelings/Work/TerrainProtectionPatches.cs`: when `MinerProtectsTerrain` is on (default), a prefix on `Attack.SpawnOnHitTerrain` returns false (skips it) when the attacker is a hireling. That's the only path through which a pickaxe swing creates terrain ops.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: registers `MinerProfile` for `JobType.Miner`.
- `src/VikingsForHire/Hirelings/Work/MineRock5Targeting.cs`: picks the nearest intact `MineRock5` sub-area (`m_hitAreas`) to aim at, so the swing hits a remaining chunk rather than empty air.

- `src/VikingsForHire/Testing/FixturesMining.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Candidates and validity:** within the work radius of the board. Tool tier: `MineRock.m_minToolTier`/`MineRock5.m_minToolTier`/`Destructible.m_minToolTier` must be ≤ the pickaxe's `m_toolTier`. Skip deposits whose top is more than 2 m below the surrounding ground (buried deposits need digging, which miners never do). Blacklist targets with no reachable stand-off point for 5 minutes. Reservation and blacklist reuse phase 08.
2. **Harvest:** stand off 1.8 m from the targeted sub-area (MineRock5) or the collider's closest point (others), `LookAt` it, `StartAttack` with the pickaxe. Damage is scaled by `HarvestPatches` from phase 08.
3. **Terrain protection:** a hireling's swing that hits the ground spawns no `TerrainOp`, so there's no ground deformation and no "dig" VFX. With `MinerProtectsTerrain=false` the vanilla behaviour returns (documented as a risky setting).
4. **Collect:** after a `MineRock5` chunk breaks, gather drops within 8 m from the pickup list (Stone, ores, scraps). Copper deposits with many chunks are worked to completion before the next target (prefer the same `MineRock5` while it has chunks).
5. **Delivery:** reuse `CargoDelivery`. Ores and stone are delivered separately per type.
6. **Status tokens:** `$vfh_status_mining`, plus the shared delivering/no-target tokens.
7. **Weight balance:** the cargo slot limit (from level) caps the haul per trip. No extra weight system.
8. **Role (agreed 2026-10-03):** a base miner only clears the stone, boulders and copper/tin near home, and runs out quickly. The miner's main use is as a **follower** you take ore-hunting with the Command Stone (phases 12–13: Gather Nearby). So keep the base behaviour simple and put the polish into field mining. When a base miner runs out of targets, its "no targets" status is the cue to take it on a trip.
9. **Gatherer radius:** add a per-job `workRadiusMultiplier` to the job data table (Woodcutter and Miner ×2, other jobs ×1). The contract radius slider's maximum is the board level's `maxWorkRadius` × the job's multiplier. The config description notes that ground much more than 100–120 m from the nearest player isn't loaded, so a larger radius than that has no effect.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesMining.cs`. Fixtures: `deposits <prefab> <n> <radius>`, `terrain_baseline` (records the count and hash of `TerrainComp` modifications within the radius). Checks: `terrain_unchanged` (compares against the baseline), `deposit_remaining <prefab>`, plus `chest`/`drop_pile` from phase 08. Aliases added to `test/alias_vfh.yaml`: `vfh_t_work3` (copper + `terrain_unchanged == true`), `vfh_t_work4` (dedicated), plus single-player `vfh_t_tier1` (above-tier deposit untouched). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Black Forest outpost board, L2 miner (antler pickaxe), radius 30:
  - Mines copper deposits to completion, collects CopperOre and Stone, and delivers each to its matching chest. Watch it for 1 in-game day: no terrain holes anywhere (compare screenshots before and after) (row **VFH-WORK-3**).
  - Tin deposits on the shoreline get mined. Large rocks give Stone.
  - Promote to L3 (bronze pickaxe): still can't mine anything above its tier, e.g. silver in mountains (tested with a mountain outpost board at L5 that has a hired L3 miner: silver is skipped).
- `MinerProtectsTerrain=false`: ground deformation appears (confirms the toggle).
- Dedicated server: copper chunk breaks are seen by both clients, and ore counts match after delivery (row **VFH-WORK-4**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(work): Miner job with tool-tier rules and terrain protection`

## Rollback
Revert the commit. Miner hirelings fall back to idle + combat.
