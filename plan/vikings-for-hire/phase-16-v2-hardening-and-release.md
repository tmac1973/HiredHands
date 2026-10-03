# Phase 16 — Milestone 2 hardening & 0.2.0 release

**Depends on:** 11, 12, 13, 14, 15 · **Enables:** nothing (final phase of this plan)

## Goal
Ship milestone 2, followers, as `0.2.0`. This phase runs the full checklist across both milestones, profiles performance with followers in the field, does a follower balance pass, updates the docs and packages the release. No new features.

## Files touched
- `docs/test-checklist.md`: adds the VFH-FOLLOW, VFH-TRAVEL and VFH-ORPHAN rows from phases 12–15 to the matrix.
- `README.md`: Command Stone section (recipes per quality, board requirements, caps), controls table (primary attack context orders, Shift+primary attack release all to work, G follow mode, H stance, E cargo, gamepad DPad), portals/ships/return-home behaviour, and the follower-related config keys.
- `CHANGELOG.md`: `## 0.2.0`.
- `src/VikingsForHire/Core/Data/DefaultData.cs`: stone cost or cap adjustments from step 2, only if needed and recorded in the CHANGELOG.
- `package/manifest.json`, `src/VikingsForHire/VikingsForHire.csproj`, `src/VikingsForHire/Plugin.cs`: version `0.2.0`.

## Steps
1. **Performance:** with 4 followers in Gather Nearby during a Plains fight, plus a full L8 base roster loaded on another client, `vfh_perf` stays ≤ 1.0 ms/frame average on each client for its own hirelings. Apply the phase 11 techniques (staggering, caching) to `OrphanMonitor` and `FieldGatherBehaviour` if needed.
2. **Follower balance pass:** play a Swamp trip (Q2 stone, 2 followers: miner + guard) and a Mountains silver run (Q3, 3 followers). Pass criteria: a miner follower roughly doubles ore carried home per trip compared with going solo, the guard follower doesn't solo a Fuling village at L5, and portal ore restrictions still force a walk or boat trip for metal. Adjust the costs and caps if the criteria fail.
3. **Full checklist:** add `vfh_t_m2_sp` (the milestone 2 single-player rows that need no human action, chained like `vfh_t_m1_sp`), then run every row from both milestones through its alias, in its listed modes (SP first, then D). Fix failures, then rerun the failed row plus VFH-HIRE-1, VFH-WORK-2, VFH-TRAVEL-1 and VFH-ORPHAN-4 (the data-integrity rows).
4. **Upgrade path test:** a world from 0.1.0 with a populated board, loaded on 0.2.0: the roster and hirelings stay intact (the roster serializer reads format version 1 and writes the current version). Recruiting works straight away.
5. **Package and release:** `dotnet build -c Release -t:Package` → `dist/Spronglehump-VikingsForHire-0.2.0.zip`, then a clean-profile smoke test. `git tag v0.2.0`, push, and make a GitHub release with the zip. Thunderstore upload is a manual step for the user.

## Build gate
- `dotnet build -c Release` with 0 warnings from our code, `dotnet test` all green, and the Package target succeeds with the version check passing.

## Test plan
- All checklist rows pass in their listed modes.
- The 0.1.0 → 0.2.0 world upgrade keeps all data.
- The packaged zip works in a clean Gale profile.
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`chore(release): 0.2.0 — followers milestone`

## Rollback
Revert individual tuning commits as needed. Delete the tag and withdraw the GitHub release. Worlds played on 0.2.0 with followers out should dismiss them before going back to 0.1.0 (see the phase 12–15 rollback notes).
